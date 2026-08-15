using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Items;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Items;

/// <summary>
/// Инвентарь персонажа: хранение предметов, ноша и стоимость имущества,
/// заряды, использование, вместилища, поиск и сортировка.
///
/// Служба не содержит правил ни одной игры: категории, единицы веса, валюты,
/// вместимость вместилищ, количество зарядов и действия предметов задаёт
/// пользователь, а все вычисления выполняет единый движок формул.
/// </summary>
public sealed class InventoryService : IInventoryService
{
    /// <summary>Количество предметов, загружаемых в список выбора за один раз.</summary>
    public const int AvailableItemPageSize = 200;

    /// <summary>Название итога для предметов, у которых валюта не указана.</summary>
    public const string UnnamedCurrency = "без валюты";

    /// <summary>Название размещения вне вместилищ.</summary>
    public const string OutsideContainers = "Вне вместилищ";

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly ICharacterBuilderService _builder;
    private readonly IFormulaEngine _formulas;
    private readonly IEventBus _eventBus;
    private readonly ILogger<InventoryService> _logger;

    /// <summary>
    /// Создаёт службу инвентаря.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="builder">Мастер создания персонажа: расчёт и проверка требований.</param>
    /// <param name="formulas">Единый движок вычислений.</param>
    /// <param name="eventBus">Шина событий приложения.</param>
    /// <param name="logger">Журналировщик.</param>
    public InventoryService(
        IDbContextFactory<RpgDbContext> contextFactory,
        ICharacterBuilderService builder,
        IFormulaEngine formulas,
        IEventBus eventBus,
        ILogger<InventoryService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _builder = Guard.NotNull(builder);
        _formulas = Guard.NotNull(formulas);
        _eventBus = Guard.NotNull(eventBus);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<InventoryState>> GetAsync(
        Guid characterId,
        InventoryQuery query,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(query);

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var character = await LoadCharacterAsync(context, characterId, tracked: false, cancellationToken)
            .ConfigureAwait(false);

        if (character is null)
        {
            return Result.Failure<InventoryState>("Персонаж не найден: возможно, он был удалён.");
        }

        var formulaContext = await _builder
            .CreateContextAsync(_builder.CreateDraft(character), cancellationToken)
            .ConfigureAwait(false);

        var weights = new InventoryWeights(character.Inventory);
        var equipped = character.Equipment.Select(record => record.InventoryItemId).ToHashSet();
        var categories = await LoadCategoriesAsync(context, character, cancellationToken).ConfigureAwait(false);

        var entries = BuildEntries(character, query, weights, equipped, formulaContext, categories);

        var state = new InventoryState(
            entries,
            BuildCategoryTree(character, categories),
            BuildWeight(character, weights, formulaContext),
            BuildMoney(character),
            BuildContainers(character),
            entries.Count);

        return Result.Success(state);
    }

    /// <inheritdoc />
    public async Task<CharacterOptionPage> GetAvailableItemsAsync(
        Guid characterId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var character = await context.Characters
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == characterId, cancellationToken)
            .ConfigureAwait(false);

        if (character is null)
        {
            return new CharacterOptionPage([], 0);
        }

        var systemId = character.GameSystemId;

        var query = context.Items
            .AsNoTracking()
            .Include(item => item.Category)
            .Where(item => item.GameSystemId == null || item.GameSystemId == systemId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";

            query = query.Where(item => EF.Functions.Like(item.Name, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderBy(item => item.Name)
            .Take(AvailableItemPageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var options = items
            .Select(item => new CharacterOption(
                item.Id,
                item.Name,
                item.Description,
                true,
                null,
                BuildOptionDetails(item),
                item.Image))
            .ToList();

        return new CharacterOptionPage(options, totalCount);
    }

    /// <inheritdoc />
    public async Task<Result> AddAsync(
        Guid characterId,
        Guid itemId,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return Result.Failure("Количество должно быть больше нуля.");
        }

        return await ChangeAsync(
            characterId,
            "Не удалось выдать предмет",
            async (context, character, formulaContext) =>
            {
                var item = await context.Items
                    .FirstOrDefaultAsync(entity => entity.Id == itemId, cancellationToken)
                    .ConfigureAwait(false);

                if (item is null)
                {
                    return Result.Failure("Предмет не найден: возможно, он был удалён.");
                }

                Give(character, item, count, formulaContext, context);

                ItemsLog.ItemAdded(_logger, character.Name, item.Name, count);

                return Result.Success();
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Result> ChangeCountAsync(
        Guid characterId,
        Guid inventoryItemId,
        int delta,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(
            characterId,
            "Не удалось изменить количество",
            (context, character, _) =>
            {
                if (Find(character, inventoryItemId) is not { } record)
                {
                    return Task.FromResult(
                        Result.Failure("Запись инвентаря не найдена: возможно, она была удалена."));
                }

                var count = record.Count + delta;

                if (count < 0)
                {
                    return Task.FromResult(Result.Failure(
                        $"У персонажа нет столько предметов «{record.Item?.Name}»."));
                }

                if (count == 0)
                {
                    return Task.FromResult(Discard(character, record, context));
                }

                if (record.Item is { Stackable: true, MaximumStackSize: { } maximum } && count > maximum)
                {
                    return Task.FromResult(Result.Failure(
                        $"В одну стопку помещается {maximum} предмет(ов) «{record.Item.Name}»."));
                }

                record.Count = count;

                return Task.FromResult(Result.Success());
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<Result> RemoveAsync(
        Guid characterId,
        Guid inventoryItemId,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(
            characterId,
            "Не удалось убрать предмет",
            (context, character, _) =>
            {
                if (Find(character, inventoryItemId) is not { } record)
                {
                    return Task.FromResult(
                        Result.Failure("Запись инвентаря не найдена: возможно, она была удалена."));
                }

                return Task.FromResult(Discard(character, record, context));
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<Result> MoveAsync(
        Guid characterId,
        Guid inventoryItemId,
        Guid? containerId,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(
            characterId,
            "Не удалось переложить предмет",
            (_, character, _) =>
            {
                if (Find(character, inventoryItemId) is not { } record)
                {
                    return Task.FromResult(
                        Result.Failure("Запись инвентаря не найдена: возможно, она была удалена."));
                }

                return Task.FromResult(Relocate(character, record, containerId));
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<Result<ItemUseResult>> UseAsync(
        Guid characterId,
        Guid inventoryItemId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var character = await LoadCharacterAsync(context, characterId, tracked: true, cancellationToken)
                .ConfigureAwait(false);

            if (character is null)
            {
                return Result.Failure<ItemUseResult>("Персонаж не найден: возможно, он был удалён.");
            }

            if (Find(character, inventoryItemId) is not { Item: { } item } record)
            {
                return Result.Failure<ItemUseResult>(
                    "Запись инвентаря не найдена: возможно, она была удалена.");
            }

            var formulaContext = await _builder
                .CreateContextAsync(_builder.CreateDraft(character), cancellationToken)
                .ConfigureAwait(false);

            var maximumCharges = EvaluateCharges(item, formulaContext);
            var remaining = record.RemainingCharges ?? maximumCharges;

            if (Blocked(item, record, remaining, formulaContext) is { } reason)
            {
                return Result.Failure<ItemUseResult>(reason);
            }

            var issues = new List<string>();

            // Значения ресурсов запоминаются до действия предмета: журнал должен
            // показать, с чего изменилось здоровье, а не только на сколько.
            var beforeUse = HistoryEntries.SnapshotResources(character);
            var effects = Apply(character, item, formulaContext, issues);

            var spentCharge = item.UseCost == ItemUseCost.Charge;
            var spentUnit = item.UseCost == ItemUseCost.Unit;

            if (spentCharge)
            {
                remaining = Math.Max(0, (remaining ?? 0) - 1);
                record.RemainingCharges = remaining;
            }

            var count = record.Count;

            if (spentUnit)
            {
                count -= 1;

                if (count <= 0)
                {
                    var discarded = Discard(character, record, context);

                    if (discarded.IsFailure)
                    {
                        return Result.Failure<ItemUseResult>(discarded.Error!);
                    }
                }
                else
                {
                    record.Count = count;
                }
            }

            context.Add(HistoryEntries.ItemUsed(character.Id, item.Name, Describe(effects)));
            context.AddRange(HistoryEntries.ResourceChanges(character, beforeUse, item.Name));

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            ItemsLog.ItemUsed(_logger, character.Name, item.Name);

            await PublishChangedAsync(character.Id, cancellationToken).ConfigureAwait(false);

            return Result.Success(new ItemUseResult(
                item.Name,
                effects,
                spentCharge,
                spentUnit,
                spentCharge ? remaining : record.RemainingCharges,
                Math.Max(0, count),
                issues));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ItemsLog.InventoryOperationFailed(_logger, exception, characterId);

            return Result.Failure<ItemUseResult>($"Не удалось использовать предмет: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public Task<Result> RestoreChargesAsync(
        Guid characterId,
        Guid inventoryItemId,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(
            characterId,
            "Не удалось восстановить заряды",
            (_, character, formulaContext) =>
            {
                if (Find(character, inventoryItemId) is not { Item: { } item } record)
                {
                    return Task.FromResult(
                        Result.Failure("Запись инвентаря не найдена: возможно, она была удалена."));
                }

                if (EvaluateCharges(item, formulaContext) is not { } maximum)
                {
                    return Task.FromResult(Result.Failure(
                        $"У предмета «{item.Name}» нет зарядов: формула зарядов не задана."));
                }

                record.RemainingCharges = maximum;

                return Task.FromResult(Result.Success());
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<Result> SetNoteAsync(
        Guid characterId,
        Guid inventoryItemId,
        string? note,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(
            characterId,
            "Не удалось сохранить пометку",
            (_, character, _) =>
            {
                if (Find(character, inventoryItemId) is not { } record)
                {
                    return Task.FromResult(
                        Result.Failure("Запись инвентаря не найдена: возможно, она была удалена."));
                }

                record.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

                return Task.FromResult(Result.Success());
            },
            cancellationToken);

    /// <summary>
    /// Выполняет изменение инвентаря: загружает персонажа, применяет изменение,
    /// сохраняет его и сообщает приложению о пересчёте.
    ///
    /// Порядок одинаков для всех изменений, поэтому он собран в одном месте:
    /// иначе каждая операция повторяла бы загрузку, сохранение и обработку ошибок.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="failureMessage">Начало сообщения об ошибке.</param>
    /// <param name="change">Изменение инвентаря.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат изменения.</returns>
    private async Task<Result> ChangeAsync(
        Guid characterId,
        string failureMessage,
        Func<RpgDbContext, Character, IFormulaContext, Task<Result>> change,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var character = await LoadCharacterAsync(context, characterId, tracked: true, cancellationToken)
                .ConfigureAwait(false);

            if (character is null)
            {
                return Result.Failure("Персонаж не найден: возможно, он был удалён.");
            }

            var formulaContext = await _builder
                .CreateContextAsync(_builder.CreateDraft(character), cancellationToken)
                .ConfigureAwait(false);

            var result = await change(context, character, formulaContext).ConfigureAwait(false);

            if (result.IsFailure)
            {
                return result;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await PublishChangedAsync(characterId, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ItemsLog.InventoryOperationFailed(_logger, exception, characterId);

            return Result.Failure($"{failureMessage}: {exception.Message}");
        }
    }

    /// <summary>
    /// Выдаёт персонажу предметы, дополняя уже имеющиеся стопки.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="item">Выдаваемый предмет.</param>
    /// <param name="count">Количество.</param>
    /// <param name="formulaContext">Значения переменных персонажа.</param>
    /// <param name="context">Контекст базы данных.</param>
    private void Give(
        Character character,
        Item item,
        int count,
        IFormulaContext formulaContext,
        RpgDbContext context)
    {
        var remaining = count;

        if (item.Stackable)
        {
            var limit = item.MaximumStackSize ?? int.MaxValue;

            foreach (var stack in character.Inventory
                         .Where(record => record.ItemId == item.Id && record.Count < limit)
                         .OrderByDescending(record => record.Count)
                         .ToList())
            {
                if (remaining <= 0)
                {
                    break;
                }

                var added = Math.Min(limit - stack.Count, remaining);

                stack.Count += added;
                remaining -= added;
            }
        }

        while (remaining > 0)
        {
            var portion = item.Stackable
                ? Math.Min(item.MaximumStackSize ?? remaining, remaining)
                : 1;

            var created = new InventoryItem
            {
                CharacterId = character.Id,
                ItemId = item.Id,
                Count = portion,
                RemainingCharges = EvaluateCharges(item, formulaContext),
            };

            character.Inventory.Add(created);

            // Запись создаётся с уже заданным идентификатором, поэтому передаётся
            // контексту явно: иначе она была бы принята за изменение (решение Р-28).
            context.Add(created);

            remaining -= portion;
        }
    }

    /// <summary>
    /// Убирает запись инвентаря: снимает надетый предмет и перекладывает
    /// содержимое вместилища на его место.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="record">Убираемая запись.</param>
    /// <param name="context">Контекст базы данных.</param>
    /// <returns>Результат удаления.</returns>
    private static Result Discard(Character character, InventoryItem record, RpgDbContext context)
    {
        // Содержимое сумки не исчезает вместе с нею: предметы остаются у персонажа
        // там, где лежала сама сумка.
        foreach (var nested in character.Inventory.Where(entry => entry.ContainerId == record.Id).ToList())
        {
            nested.ContainerId = record.ContainerId;
        }

        foreach (var equipped in character.Equipment
                     .Where(entry => entry.InventoryItemId == record.Id)
                     .ToList())
        {
            character.Equipment.Remove(equipped);
            context.Remove(equipped);
        }

        character.Inventory.Remove(record);
        context.Remove(record);

        return Result.Success();
    }

    /// <summary>
    /// Перекладывает запись инвентаря во вместилище.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="record">Перекладываемая запись.</param>
    /// <param name="containerId">Идентификатор записи вместилища либо <see langword="null"/>.</param>
    /// <returns>Результат перемещения.</returns>
    private static Result Relocate(Character character, InventoryItem record, Guid? containerId)
    {
        if (containerId is not { } targetId)
        {
            record.ContainerId = null;
            return Result.Success();
        }

        if (targetId == record.Id)
        {
            return Result.Failure("Предмет нельзя положить внутрь самого себя.");
        }

        if (character.Inventory.FirstOrDefault(entry => entry.Id == targetId) is not { } container)
        {
            return Result.Failure("Вместилище не найдено: возможно, оно было убрано.");
        }

        if (container.Item is not { IsContainer: true })
        {
            return Result.Failure($"Предмет «{container.Item?.Name}» не вмещает другие предметы.");
        }

        if (IsInside(character, container, record.Id))
        {
            return Result.Failure(
                $"Вместилище «{container.Item.Name}» само лежит внутри перекладываемого предмета.");
        }

        var weights = new InventoryWeights(character.Inventory);

        if (container.Item.Capacity is { } capacity)
        {
            var free = capacity - weights.Content(container);
            var incoming = weights.Carried(record);

            if (incoming > free)
            {
                return Result.Failure(
                    $"Во вместилище «{container.Item.Name}» осталось "
                    + $"{Format(free)} из {Format(capacity)}, а предмет весит {Format(incoming)}.");
            }
        }

        record.ContainerId = targetId;

        return Result.Success();
    }

    /// <summary>
    /// Проверяет, лежит ли запись внутри указанного вместилища на любой глубине.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="record">Проверяемая запись.</param>
    /// <param name="containerId">Идентификатор вместилища.</param>
    /// <returns><see langword="true"/>, если запись вложена во вместилище.</returns>
    private static bool IsInside(Character character, InventoryItem record, Guid containerId)
    {
        var visited = new HashSet<Guid>();
        var current = record;

        while (current.ContainerId is { } parentId && visited.Add(parentId))
        {
            if (parentId == containerId)
            {
                return true;
            }

            current = character.Inventory.FirstOrDefault(entry => entry.Id == parentId);

            if (current is null)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Возвращает причину, по которой предмет нельзя использовать.
    /// </summary>
    /// <param name="item">Предмет.</param>
    /// <param name="record">Запись инвентаря.</param>
    /// <param name="remainingCharges">Оставшиеся заряды.</param>
    /// <param name="formulaContext">Значения переменных персонажа.</param>
    /// <returns>Причина отказа либо <see langword="null"/>.</returns>
    private string? Blocked(
        Item item,
        InventoryItem record,
        int? remainingCharges,
        IFormulaContext formulaContext)
    {
        if (item.UseCost == ItemUseCost.None && item.UseEffects.Count == 0)
        {
            return $"Предмет «{item.Name}» не используется: у него не задано ни одного действия.";
        }

        if (_builder.CheckRequirement(item.Requirements, formulaContext) is { } reason)
        {
            return $"Персонаж не может использовать «{item.Name}». {reason}";
        }

        if (item.UseCost == ItemUseCost.Charge && remainingCharges is not > 0)
        {
            return $"У предмета «{item.Name}» не осталось зарядов.";
        }

        if (item.UseCost == ItemUseCost.Unit && record.Count <= 0)
        {
            return $"Предметов «{item.Name}» не осталось.";
        }

        return null;
    }

    /// <summary>
    /// Выполняет действия предмета: изменяет ресурсы персонажа на вычисленные величины.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="item">Используемый предмет.</param>
    /// <param name="formulaContext">Значения переменных персонажа.</param>
    /// <param name="issues">Замечания вычисления.</param>
    /// <returns>Что произошло.</returns>
    private List<InventoryUseEffect> Apply(
        Character character,
        Item item,
        IFormulaContext formulaContext,
        List<string> issues)
    {
        var effects = new List<InventoryUseEffect>();

        foreach (var effect in item.UseEffects.OrderBy(entry => entry.SortOrder))
        {
            var value = EvaluateNumber(effect.Formula, formulaContext, effect.Name ?? item.Name, issues);
            var resource = effect.ResourceId is { } resourceId
                ? character.Resources.FirstOrDefault(entry => entry.ResourceId == resourceId)
                : null;

            if (resource is null)
            {
                // Действие без ресурса описывает то, что игроки отыгрывают сами:
                // приложение показывает его, но не выполняет.
                effects.Add(new InventoryUseEffect(
                    effect.Name ?? "действие предмета",
                    value,
                    false));

                continue;
            }

            var before = resource.Current;

            resource.Current = Math.Clamp(resource.Current + value, 0, resource.Maximum);

            effects.Add(new InventoryUseEffect(
                effect.Name ?? effect.Resource?.Name ?? "ресурс",
                resource.Current - before,
                true));
        }

        return effects;
    }

    /// <summary>
    /// Собирает записи инвентаря к показу.
    ///
    /// Без отбора записи выстроены деревом вместилищ, а с отбором — плоским списком:
    /// найденный предмет должен быть виден сразу, а не спрятан внутри свёрнутой сумки.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="query">Отбор предметов.</param>
    /// <param name="weights">Ноша персонажа.</param>
    /// <param name="equipped">Идентификаторы надетых записей.</param>
    /// <param name="formulaContext">Значения переменных персонажа.</param>
    /// <param name="categories">Категории предметов игровой системы.</param>
    /// <returns>Записи в выбранном порядке.</returns>
    private List<InventoryEntry> BuildEntries(
        Character character,
        InventoryQuery query,
        InventoryWeights weights,
        IReadOnlySet<Guid> equipped,
        IFormulaContext formulaContext,
        IReadOnlyList<ItemCategory> categories)
    {
        var entries = new List<InventoryEntry>();
        var hasSearch = !string.IsNullOrWhiteSpace(query.Search);

        if (!hasSearch && query.CategoryId is null)
        {
            Append(weights.Roots, 0);

            return entries;
        }

        var allowed = query.CategoryId is { } categoryId
            ? Descendants(categories, categoryId)
            : null;

        var matching = character.Inventory
            .Where(record => Matches(record, query.Search, allowed))
            .Select(record => Build(record, 0))
            .ToList();

        return Order(matching, query).ToList();

        void Append(IReadOnlyList<InventoryItem> siblings, int depth)
        {
            var built = siblings.Select(record => Build(record, depth)).ToList();

            foreach (var entry in Order(built, query))
            {
                entries.Add(entry);
                Append(weights.ChildrenOf(entry.InventoryItemId), depth + 1);
            }
        }

        InventoryEntry Build(InventoryItem record, int depth) =>
            BuildEntry(record, depth, weights, equipped, formulaContext);
    }

    /// <summary>
    /// Собирает запись инвентаря к показу.
    /// </summary>
    /// <param name="record">Запись инвентаря.</param>
    /// <param name="depth">Глубина вложенности во вместилищах.</param>
    /// <param name="weights">Ноша персонажа.</param>
    /// <param name="equipped">Идентификаторы надетых записей.</param>
    /// <param name="formulaContext">Значения переменных персонажа.</param>
    /// <returns>Запись к показу.</returns>
    private InventoryEntry BuildEntry(
        InventoryItem record,
        int depth,
        InventoryWeights weights,
        IReadOnlySet<Guid> equipped,
        IFormulaContext formulaContext)
    {
        var item = record.Item;
        var maximumCharges = item is null ? null : EvaluateCharges(item, formulaContext);
        var remaining = record.RemainingCharges ?? maximumCharges;

        var reason = item is null
            ? "Предмет не найден."
            : Blocked(item, record, remaining, formulaContext);

        var isUsable = item is not null
            && (item.UseCost != ItemUseCost.None || item.UseEffects.Count > 0);

        return new InventoryEntry(
            record.Id,
            record.ItemId,
            item?.Name ?? "Предмет удалён",
            item?.Description,
            item?.Category?.Name,
            item?.ItemType,
            item?.Rarity,
            record.Count,
            item?.Weight ?? 0,
            weights.Carried(record),
            item?.Price ?? 0,
            (item?.Price ?? 0) * record.Count,
            item?.Currency,
            remaining,
            maximumCharges,
            item is null ? null : UseCostDescription(item.UseCost),
            isUsable && reason is null,
            isUsable ? reason : null,
            item?.IsContainer ?? false,
            item?.Capacity,
            weights.Content(record),
            record.ContainerId,
            depth,
            equipped.Contains(record.Id),
            record.Note);
    }

    /// <summary>
    /// Проверяет, подходит ли запись под отбор.
    /// </summary>
    /// <param name="record">Запись инвентаря.</param>
    /// <param name="search">Строка поиска.</param>
    /// <param name="categories">Допустимые категории либо <see langword="null"/>.</param>
    /// <returns><see langword="true"/>, если запись подходит.</returns>
    private static bool Matches(InventoryItem record, string? search, HashSet<Guid>? categories)
    {
        if (categories is not null
            && (record.Item?.CategoryId is not { } categoryId || !categories.Contains(categoryId)))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var needle = search.Trim();

        return Contains(record.Item?.Name, needle)
            || Contains(record.Item?.ItemType, needle)
            || Contains(record.Item?.Rarity, needle)
            || Contains(record.Item?.Category?.Name, needle)
            || Contains(record.Item?.Description, needle)
            || Contains(record.Note, needle);
    }

    private static bool Contains(string? value, string needle) =>
        value is not null && value.Contains(needle, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    /// Упорядочивает записи выбранным способом.
    /// </summary>
    /// <param name="entries">Записи одного уровня вложенности.</param>
    /// <param name="query">Отбор предметов.</param>
    /// <returns>Упорядоченные записи.</returns>
    private static IEnumerable<InventoryEntry> Order(List<InventoryEntry> entries, InventoryQuery query)
    {
        // Название используется вторым ключом всегда: при равных весе, стоимости
        // или количестве порядок не должен зависеть от порядка чтения из базы.
        IOrderedEnumerable<InventoryEntry> ordered = query.Sort switch
        {
            InventorySort.Weight => Sort(entries, entry => entry.Weight, query.Descending),
            InventorySort.Price => Sort(entries, entry => entry.Price, query.Descending),
            InventorySort.Count => Sort(entries, entry => entry.Count, query.Descending),
            InventorySort.Rarity => Sort(entries, entry => entry.Rarity ?? string.Empty, query.Descending),
            InventorySort.Added => Sort(entries, entry => entry.InventoryItemId, query.Descending),
            _ => Sort(entries, entry => entry.Name, query.Descending),
        };

        return ordered.ThenBy(entry => entry.Name, StringComparer.CurrentCulture);
    }

    private static IOrderedEnumerable<InventoryEntry> Sort<TKey>(
        List<InventoryEntry> entries,
        Func<InventoryEntry, TKey> key,
        bool descending) =>
        descending ? entries.OrderByDescending(key) : entries.OrderBy(key);

    /// <summary>
    /// Собирает дерево категорий, показывая только те, в которых у персонажа
    /// есть предметы, вместе с вышестоящими категориями.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="categories">Категории предметов игровой системы.</param>
    /// <returns>Разделы дерева в порядке показа.</returns>
    private static List<InventoryCategoryNode> BuildCategoryTree(
        Character character,
        IReadOnlyList<ItemCategory> categories)
    {
        var counts = new Dictionary<Guid, int>();

        foreach (var record in character.Inventory)
        {
            if (record.Item?.CategoryId is not { } categoryId)
            {
                continue;
            }

            // Предмет учитывается и во всех вышестоящих категориях: раздел «Снаряжение»
            // показывает то, что лежит в его подразделах.
            foreach (var id in Ancestors(categories, categoryId))
            {
                counts[id] = counts.GetValueOrDefault(id) + 1;
            }
        }

        var nodes = new List<InventoryCategoryNode>
        {
            new(null, "Все предметы", 0, character.Inventory.Count),
        };

        Append(null, 1);

        return nodes;

        void Append(Guid? parentId, int depth)
        {
            foreach (var category in categories
                         .Where(entry => entry.ParentId == parentId)
                         .OrderBy(entry => entry.SortOrder)
                         .ThenBy(entry => entry.Name, StringComparer.CurrentCulture))
            {
                if (!counts.TryGetValue(category.Id, out var count))
                {
                    continue;
                }

                nodes.Add(new InventoryCategoryNode(category.Id, category.Name, depth, count));
                Append(category.Id, depth + 1);
            }
        }
    }

    /// <summary>
    /// Возвращает категорию вместе со всеми вышестоящими.
    /// </summary>
    /// <param name="categories">Категории предметов.</param>
    /// <param name="categoryId">Идентификатор категории.</param>
    /// <returns>Идентификаторы категории и её предков.</returns>
    private static IEnumerable<Guid> Ancestors(IReadOnlyList<ItemCategory> categories, Guid categoryId)
    {
        var visited = new HashSet<Guid>();
        var current = categories.FirstOrDefault(entry => entry.Id == categoryId);

        while (current is not null && visited.Add(current.Id))
        {
            yield return current.Id;

            current = current.ParentId is { } parentId
                ? categories.FirstOrDefault(entry => entry.Id == parentId)
                : null;
        }
    }

    /// <summary>
    /// Возвращает категорию вместе со всеми вложенными.
    /// </summary>
    /// <param name="categories">Категории предметов.</param>
    /// <param name="categoryId">Идентификатор категории.</param>
    /// <returns>Идентификаторы категории и её потомков.</returns>
    private static HashSet<Guid> Descendants(IReadOnlyList<ItemCategory> categories, Guid categoryId)
    {
        var result = new HashSet<Guid> { categoryId };
        var added = true;

        while (added)
        {
            added = false;

            foreach (var category in categories)
            {
                if (category.ParentId is { } parentId
                    && result.Contains(parentId)
                    && result.Add(category.Id))
                {
                    added = true;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Считает ношу персонажа и переносимый вес его игровой системы.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="weights">Ноша персонажа.</param>
    /// <param name="formulaContext">Значения переменных персонажа.</param>
    /// <returns>Ноша персонажа.</returns>
    private InventoryWeight BuildWeight(
        Character character,
        InventoryWeights weights,
        IFormulaContext formulaContext)
    {
        var formula = character.GameSystem?.CarryCapacityFormula;
        double? capacity = null;

        if (!string.IsNullOrWhiteSpace(formula))
        {
            var result = _formulas.Evaluate(formula, formulaContext);

            if (result.IsSuccess)
            {
                capacity = result.Value.AsNumber();
            }
        }

        return new InventoryWeight(weights.Total, capacity, character.GameSystem?.WeightUnit);
    }

    /// <summary>
    /// Считает стоимость имущества по валютам.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <returns>Итоги по валютам.</returns>
    private static List<InventoryCurrencyTotal> BuildMoney(Character character) =>
        character.Inventory
            .Where(record => record.Item is not null && record.Item.Price != 0)
            .GroupBy(record => string.IsNullOrWhiteSpace(record.Item!.Currency)
                ? UnnamedCurrency
                : record.Item.Currency!.Trim())
            .Select(group => new InventoryCurrencyTotal(
                group.Key,
                group.Sum(record => record.Item!.Price * record.Count)))
            .OrderBy(total => total.Currency, StringComparer.CurrentCulture)
            .ToList();

    /// <summary>
    /// Собирает вместилища, в которые можно переложить предмет.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <returns>Вместилища вместе с размещением вне них.</returns>
    private static List<InventoryContainerOption> BuildContainers(Character character)
    {
        var options = new List<InventoryContainerOption> { new(null, OutsideContainers) };

        options.AddRange(character.Inventory
            .Where(record => record.Item?.IsContainer == true)
            .OrderBy(record => record.Item!.Name, StringComparer.CurrentCulture)
            .Select(record => new InventoryContainerOption(record.Id, record.Item!.Name)));

        return options;
    }

    /// <summary>
    /// Вычисляет наибольшее количество зарядов предмета.
    /// </summary>
    /// <param name="item">Предмет.</param>
    /// <param name="formulaContext">Значения переменных персонажа.</param>
    /// <returns>Количество зарядов либо <see langword="null"/>, если зарядов нет.</returns>
    private int? EvaluateCharges(Item item, IFormulaContext formulaContext)
    {
        if (string.IsNullOrWhiteSpace(item.ChargesFormula))
        {
            return null;
        }

        var result = _formulas.Evaluate(item.ChargesFormula, formulaContext);

        return result.IsSuccess
            ? Math.Max(0, (int)Math.Round(result.Value.AsNumber(), MidpointRounding.AwayFromZero))
            : null;
    }

    private double EvaluateNumber(
        string? formula,
        IFormulaContext formulaContext,
        string description,
        List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return 0;
        }

        var result = _formulas.Evaluate(formula, formulaContext);

        if (result.IsSuccess)
        {
            return result.Value.AsNumber();
        }

        issues.Add($"Формула «{description}»: {result.Error}");

        return 0;
    }

    private static string? UseCostDescription(ItemUseCost cost) => cost switch
    {
        ItemUseCost.Charge => "заряд",
        ItemUseCost.Unit => "единицу предмета",
        _ => null,
    };

    private static string Format(double value) =>
        value.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>
    /// Описывает действия предмета одной строкой для журнала.
    /// </summary>
    /// <param name="effects">Действия, выполненные предметом.</param>
    /// <returns>Описание действий.</returns>
    private static string Describe(IReadOnlyList<InventoryUseEffect> effects) =>
        string.Join(
            ", ",
            effects.Select(effect => Math.Abs(effect.Value) < double.Epsilon
                ? effect.Description
                : $"{effect.Description} {(effect.Value > 0 ? "+" : "−")}{Format(Math.Abs(effect.Value))}"));

    private static InventoryItem? Find(Character character, Guid inventoryItemId) =>
        character.Inventory.FirstOrDefault(record => record.Id == inventoryItemId);

    private static List<CharacterOptionDetail> BuildOptionDetails(Item item)
    {
        var details = new List<CharacterOptionDetail>();

        if (item.Category is { } category)
        {
            details.Add(new CharacterOptionDetail("Категория", category.Name));
        }

        if (!string.IsNullOrWhiteSpace(item.ItemType))
        {
            details.Add(new CharacterOptionDetail("Тип", item.ItemType));
        }

        if (item.Weight != 0)
        {
            details.Add(new CharacterOptionDetail("Вес", Format(item.Weight)));
        }

        if (item.Price != 0)
        {
            details.Add(new CharacterOptionDetail(
                "Стоимость",
                $"{Format(item.Price)} {item.Currency}".TrimEnd()));
        }

        return details;
    }

    /// <summary>
    /// Загружает категории предметов, доступные игровой системе персонажа.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="character">Персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Категории предметов.</returns>
    private static async Task<IReadOnlyList<ItemCategory>> LoadCategoriesAsync(
        RpgDbContext context,
        Character character,
        CancellationToken cancellationToken)
    {
        var systemId = character.GameSystemId;

        return await context.ItemCategories
            .AsNoTracking()
            .Where(category => category.GameSystemId == null || category.GameSystemId == systemId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Загружает персонажа вместе с инвентарём, предметами, их действиями и ресурсами.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="tracked">Изменения персонажа будут сохранены.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Персонаж либо <see langword="null"/>.</returns>
    private static Task<Character?> LoadCharacterAsync(
        RpgDbContext context,
        Guid characterId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = context.Characters
            .Include(character => character.GameSystem)
            .Include(character => character.Race)
            .Include(character => character.Class)
            .Include(character => character.Subclass)
            .Include(character => character.Background)
            .Include(character => character.Attributes)
            .Include(character => character.Skills)
            .Include(character => character.Traits)
            .Include(character => character.Spells)
            .Include(character => character.Resources)
                .ThenInclude(record => record.Resource)
            .Include(character => character.Inventory)
                .ThenInclude(record => record.Item)
                .ThenInclude(item => item!.Category)
            .Include(character => character.Inventory)
                .ThenInclude(record => record.Item)
                .ThenInclude(item => item!.UseEffects)
                .ThenInclude(effect => effect.Resource)
            .Include(character => character.Equipment);

        return tracked
            ? query.FirstOrDefaultAsync(character => character.Id == characterId, cancellationToken)
            : query.AsNoTracking()
                .FirstOrDefaultAsync(character => character.Id == characterId, cancellationToken);
    }

    /// <summary>
    /// Сообщает приложению, что состояние персонажа изменилось: расход ресурса,
    /// снятие предмета и изменение ноши должны попасть на лист персонажа.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после публикации события.</returns>
    private Task PublishChangedAsync(Guid characterId, CancellationToken cancellationToken) =>
        _eventBus.PublishAsync(
            new CharacterChangedEvent(characterId, CharacterChangeKind.Recalculated),
            cancellationToken);
}
