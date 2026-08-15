using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Items;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Items;

/// <summary>
/// Экипировка персонажа: слоты, надевание предметов и их бонусы.
///
/// Служба не содержит правил ни одной игры: состав слотов задаёт пользователь,
/// вместимость слота хранится у самого слота, а усиление описывается бонусом
/// предмета и попадает в расчёт персонажа автоматически.
/// </summary>
public sealed class EquipmentService : IEquipmentService
{
    /// <summary>Количество предметов, загружаемых в список выбора за один раз.</summary>
    public const int AvailableItemPageSize = 200;

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly ICharacterBuilderService _builder;
    private readonly IEventBus _eventBus;
    private readonly ILogger<EquipmentService> _logger;

    /// <summary>
    /// Создаёт службу экипировки.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="builder">Мастер создания персонажа: расчёт и проверка требований.</param>
    /// <param name="eventBus">Шина событий приложения.</param>
    /// <param name="logger">Журналировщик.</param>
    public EquipmentService(
        IDbContextFactory<RpgDbContext> contextFactory,
        ICharacterBuilderService builder,
        IEventBus eventBus,
        ILogger<EquipmentService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _builder = Guard.NotNull(builder);
        _eventBus = Guard.NotNull(eventBus);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<EquipmentSlotState>>> GetSlotsAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var character = await LoadCharacterAsync(context, characterId, tracked: false, cancellationToken)
            .ConfigureAwait(false);

        if (character is null)
        {
            return Result.Failure<IReadOnlyList<EquipmentSlotState>>(
                "Персонаж не найден: возможно, он был удалён.");
        }

        var slots = await context.EquipmentSlots
            .AsNoTracking()
            .Where(slot => slot.GameSystemId == null || slot.GameSystemId == character.GameSystemId)
            .OrderBy(slot => slot.SortOrder)
            .ThenBy(slot => slot.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var draft = _builder.CreateDraft(character);
        var calculation = await _builder.CalculateAsync(draft, cancellationToken).ConfigureAwait(false);
        var formulaContext = await _builder.CreateContextAsync(draft, cancellationToken).ConfigureAwait(false);

        // Бонусы уже вычислены расчётом персонажа: считать их здесь заново означало бы
        // завести второй вычислитель рядом с единым.
        var bonuses = calculation.Bonuses.ToDictionary(bonus => (bonus.SourceId, bonus.Id));

        var state = slots
            .Select(slot => new EquipmentSlotState(
                slot.Id,
                slot.Name,
                slot.Description,
                Math.Max(1, slot.AllowMultiple ? slot.MaximumItems : 1),
                BuildItems(character, slot.Id, bonuses, formulaContext)))
            .ToList();

        return Result.Success<IReadOnlyList<EquipmentSlotState>>(state);
    }

    /// <inheritdoc />
    public async Task<CharacterOptionPage> GetAvailableItemsAsync(
        Guid characterId,
        Guid slotId,
        string? search,
        bool includeUnavailable,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var character = await LoadCharacterAsync(context, characterId, tracked: false, cancellationToken)
            .ConfigureAwait(false);

        if (character is null)
        {
            return new CharacterOptionPage([], 0);
        }

        var systemId = character.GameSystemId;

        var query = context.Items
            .AsNoTracking()
            .Include(item => item.Bonuses)
            .Where(item => item.EquipmentSlotId == slotId)
            .Where(item => item.GameSystemId == null || item.GameSystemId == systemId)
            .Where(item => item.OwnerCharacterId == null || item.OwnerCharacterId == characterId);

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

        var draft = _builder.CreateDraft(character);
        var formulaContext = await _builder.CreateContextAsync(draft, cancellationToken).ConfigureAwait(false);

        var options = new List<CharacterOption>(items.Count);

        foreach (var item in items)
        {
            var reason = _builder.CheckRequirement(item.Requirements, formulaContext);

            if (reason is not null && !includeUnavailable)
            {
                continue;
            }

            options.Add(new CharacterOption(
                item.Id,
                item.Name,
                item.Description,
                reason is null,
                reason,
                BuildOptionDetails(item),
                item.Image));
        }

        return new CharacterOptionPage(options, totalCount);
    }

    /// <inheritdoc />
    public async Task<Result> EquipAsync(
        Guid characterId,
        Guid itemId,
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
                return Result.Failure("Персонаж не найден: возможно, он был удалён.");
            }

            var item = await context.Items
                .Include(entity => entity.EquipmentSlot)
                .FirstOrDefaultAsync(
                    entity => entity.Id == itemId
                        && (entity.OwnerCharacterId == null || entity.OwnerCharacterId == characterId),
                    cancellationToken)
                .ConfigureAwait(false);

            if (item is null)
            {
                return Result.Failure("Предмет не найден: возможно, он был удалён.");
            }

            if (item.EquipmentSlotId is not { } slotId || item.EquipmentSlot is not { } slot)
            {
                return Result.Failure(
                    $"Предмет «{item.Name}» не надевается: у него не выбран слот экипировки.");
            }

            var draft = _builder.CreateDraft(character);
            var formulaContext = await _builder.CreateContextAsync(draft, cancellationToken)
                .ConfigureAwait(false);

            if (_builder.CheckRequirement(item.Requirements, formulaContext) is { } reason)
            {
                return Result.Failure($"Персонаж не может надеть «{item.Name}». {reason}");
            }

            var capacity = Math.Max(1, slot.AllowMultiple ? slot.MaximumItems : 1);
            var occupied = character.Equipment.Count(record => record.SlotId == slotId);

            if (occupied >= capacity)
            {
                return Result.Failure(
                    $"Слот «{slot.Name}» занят: в него помещается "
                    + $"{capacity} предмет(ов). Сначала снимите лишнее.");
            }

            // Предмет, которого у персонажа ещё нет, выдаётся вместе с надеванием:
            // отдельный шаг «получить, затем надеть» не нужен до появления инвентаря.
            var inventoryItem = character.Inventory
                .FirstOrDefault(record => record.ItemId == itemId
                    && character.Equipment.All(equipped => equipped.InventoryItemId != record.Id));

            if (inventoryItem is null)
            {
                inventoryItem = new InventoryItem
                {
                    CharacterId = character.Id,
                    ItemId = item.Id,
                    Count = 1,
                };

                character.Inventory.Add(inventoryItem);
                context.Add(inventoryItem);
            }

            var equipment = new CharacterEquipment
            {
                CharacterId = character.Id,
                SlotId = slotId,
                InventoryItemId = inventoryItem.Id,
            };

            character.Equipment.Add(equipment);
            context.Add(equipment);

            // Событие попадает в журнал той же операцией, что и само надевание:
            // запись не может появиться без изменения и наоборот.
            context.Add(HistoryEntries.ItemEquipped(character.Id, item.Name, slot.Name));

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            ItemsLog.ItemEquipped(_logger, character.Name, item.Name, slot.Name);

            await PublishChangedAsync(character.Id, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ItemsLog.EquipmentOperationFailed(_logger, exception, characterId);

            return Result.Failure($"Не удалось надеть предмет: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateLocalAndEquipAsync(
        Guid characterId,
        Guid slotId,
        LocalEquipmentDraft draft,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(draft);

        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            return Result.Failure<Guid>("Введите название экипировки.");
        }

        if (!double.IsFinite(draft.Weight) || draft.Weight < 0 ||
            !double.IsFinite(draft.Price) || draft.Price < 0)
        {
            return Result.Failure<Guid>("Вес и стоимость экипировки должны быть числами не меньше нуля.");
        }

        var invalidBonus = draft.Bonuses.FirstOrDefault(bonus =>
            (bonus.Target == BonusTargetKind.Attribute && bonus.AttributeId is null) ||
            (bonus.Target == BonusTargetKind.Resource && bonus.ResourceId is null) ||
            ((bonus.Target == BonusTargetKind.Variable || bonus.Target == BonusTargetKind.Tag) &&
                string.IsNullOrWhiteSpace(bonus.Name)) ||
            (bonus.Target != BonusTargetKind.Tag && string.IsNullOrWhiteSpace(bonus.Formula)));

        if (invalidBonus is not null)
        {
            return Result.Failure<Guid>(
                "Заполните цель каждого бонуса; для числового бонуса также нужна формула.");
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var character = await LoadCharacterAsync(context, characterId, tracked: true, cancellationToken)
                .ConfigureAwait(false);

            if (character is null)
            {
                return Result.Failure<Guid>("Персонаж не найден: возможно, он был удалён.");
            }

            var slot = await context.EquipmentSlots
                .FirstOrDefaultAsync(entity => entity.Id == slotId &&
                    (entity.GameSystemId == null || entity.GameSystemId == character.GameSystemId),
                    cancellationToken)
                .ConfigureAwait(false);

            if (slot is null)
            {
                return Result.Failure<Guid>("Слот экипировки не найден.");
            }

            var capacity = Math.Max(1, slot.AllowMultiple ? slot.MaximumItems : 1);
            if (character.Equipment.Count(record => record.SlotId == slotId) >= capacity)
            {
                return Result.Failure<Guid>($"Слот «{slot.Name}» уже заполнен.");
            }

            foreach (var bonus in draft.Bonuses)
            {
                if (bonus.AttributeId is { } attributeId &&
                    !await context.Attributes.AnyAsync(attribute => attribute.Id == attributeId,
                        cancellationToken).ConfigureAwait(false))
                {
                    return Result.Failure<Guid>("Одна из выбранных характеристик не найдена.");
                }

                if (bonus.ResourceId is { } resourceId &&
                    !await context.Resources.AnyAsync(resource => resource.Id == resourceId,
                        cancellationToken).ConfigureAwait(false))
                {
                    return Result.Failure<Guid>("Один из выбранных ресурсов не найден.");
                }
            }

            var item = new Item
            {
                OwnerCharacterId = characterId,
                GameSystemId = character.GameSystemId,
                Name = draft.Name.Trim(),
                SystemName = $"local_equipment_{characterId:N}_{Guid.NewGuid():N}",
                Source = "Авторская экипировка персонажа",
                Description = string.IsNullOrWhiteSpace(draft.Description) ? null : draft.Description.Trim(),
                ItemType = string.IsNullOrWhiteSpace(draft.ItemType) ? "Авторская экипировка" : draft.ItemType.Trim(),
                Rarity = string.IsNullOrWhiteSpace(draft.Rarity) ? null : draft.Rarity.Trim(),
                Weight = draft.Weight,
                Price = draft.Price,
                Currency = string.IsNullOrWhiteSpace(draft.Currency) ? null : draft.Currency.Trim(),
                EquipmentSlotId = slotId,
                Stackable = false,
            };

            var order = 0;
            foreach (var bonus in draft.Bonuses)
            {
                item.Bonuses.Add(new ItemBonus
                {
                    ItemId = item.Id,
                    Target = bonus.Target,
                    AttributeId = bonus.Target == BonusTargetKind.Attribute ? bonus.AttributeId : null,
                    ResourceId = bonus.Target == BonusTargetKind.Resource ? bonus.ResourceId : null,
                    Name = string.IsNullOrWhiteSpace(bonus.Name) ? null : bonus.Name.Trim(),
                    Formula = bonus.Target == BonusTargetKind.Tag || string.IsNullOrWhiteSpace(bonus.Formula)
                        ? null
                        : bonus.Formula.Trim(),
                    Condition = string.IsNullOrWhiteSpace(bonus.Condition) ? null : bonus.Condition.Trim(),
                    SortOrder = order++,
                });
            }

            var inventory = new InventoryItem
            {
                CharacterId = characterId,
                ItemId = item.Id,
                Item = item,
                Count = 1,
            };

            var equipment = new CharacterEquipment
            {
                CharacterId = characterId,
                SlotId = slotId,
                InventoryItemId = inventory.Id,
                InventoryItem = inventory,
            };

            context.Add(item);
            context.Add(inventory);
            context.Add(equipment);
            context.Add(HistoryEntries.ItemEquipped(characterId, item.Name, slot.Name));

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            ItemsLog.ItemEquipped(_logger, character.Name, item.Name, slot.Name);
            await PublishChangedAsync(characterId, cancellationToken).ConfigureAwait(false);

            return Result.Success(inventory.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ItemsLog.EquipmentOperationFailed(_logger, exception, characterId);
            return Result.Failure<Guid>($"Не удалось создать экипировку: {exception.Message}");
        }
    }
    /// <inheritdoc />
    public async Task<Result> UnequipAsync(
        Guid characterId,
        Guid inventoryItemId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            // Название предмета и слота нужны журналу: запись «снято» без имени
            // предмета не объясняет, что произошло.
            var equipment = await context.CharacterEquipment
                .Include(record => record.Slot)
                .Include(record => record.InventoryItem)
                    .ThenInclude(record => record!.Item)
                .FirstOrDefaultAsync(
                    record => record.CharacterId == characterId
                        && record.InventoryItemId == inventoryItemId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (equipment is null)
            {
                return Result.Failure("Предмет не надет: возможно, он уже снят.");
            }

            context.Remove(equipment);
            context.Add(HistoryEntries.ItemUnequipped(
                characterId,
                equipment.InventoryItem?.Item?.Name ?? "Предмет",
                equipment.Slot?.Name));

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            ItemsLog.ItemUnequipped(_logger, characterId);

            await PublishChangedAsync(characterId, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ItemsLog.EquipmentOperationFailed(_logger, exception, characterId);

            return Result.Failure($"Не удалось снять предмет: {exception.Message}");
        }
    }

    /// <summary>
    /// Загружает персонажа вместе с инвентарём, экипировкой и бонусами предметов.
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
            .Include(character => character.Race)
            .Include(character => character.Class)
            .Include(character => character.Subclass)
            .Include(character => character.Background)
            .Include(character => character.Attributes)
            .Include(character => character.Skills)
            .Include(character => character.Traits)
            .Include(character => character.Spells)
            .Include(character => character.Resources)
            .Include(character => character.Inventory)
                .ThenInclude(record => record.Item)
                .ThenInclude(item => item!.Bonuses)
            .Include(character => character.Equipment);

        return tracked
            ? query.FirstOrDefaultAsync(character => character.Id == characterId, cancellationToken)
            : query.AsNoTracking()
                .FirstOrDefaultAsync(character => character.Id == characterId, cancellationToken);
    }

    /// <summary>
    /// Собирает список предметов, надетых в слот.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="slotId">Идентификатор слота.</param>
    /// <param name="bonuses">Вычисленные бонусы по надетой записи и описанию бонуса.</param>
    /// <param name="formulaContext">Значения переменных персонажа.</param>
    /// <returns>Надетые предметы.</returns>
    private List<EquippedItem> BuildItems(
        Character character,
        Guid slotId,
        IReadOnlyDictionary<(Guid SourceId, Guid BonusId), AppliedBonus> bonuses,
        Core.Abstractions.Engine.IFormulaContext formulaContext)
    {
        var inventory = character.Inventory.ToDictionary(record => record.Id);
        var result = new List<EquippedItem>();

        foreach (var equipped in character.Equipment.Where(record => record.SlotId == slotId))
        {
            if (!inventory.TryGetValue(equipped.InventoryItemId, out var record) || record.Item is not { } item)
            {
                continue;
            }

            result.Add(new EquippedItem(
                record.Id,
                item.Id,
                item.Name,
                item.Description,
                item.ItemType,
                BuildBonuses(item, record.Id, bonuses),
                _builder.CheckRequirement(item.Requirements, formulaContext)));
        }

        return result;
    }

    /// <summary>
    /// Сопоставляет описания бонусов предмета с их вычисленными величинами.
    /// Соответствие устанавливается по идентификатору записи, поэтому два предмета
    /// с одинаковым названием не путаются между собой.
    /// </summary>
    /// <param name="item">Предмет с загруженными бонусами.</param>
    /// <param name="inventoryItemId">Идентификатор надетой записи инвентаря.</param>
    /// <param name="applied">Вычисленные бонусы по надетой записи и описанию бонуса.</param>
    /// <returns>Бонусы для отображения.</returns>
    private static List<EquipmentBonus> BuildBonuses(
        Item item,
        Guid inventoryItemId,
        IReadOnlyDictionary<(Guid SourceId, Guid BonusId), AppliedBonus> applied) =>
        item.Bonuses
            .OrderBy(bonus => bonus.SortOrder)
            .Select(bonus =>
            {
                applied.TryGetValue((inventoryItemId, bonus.Id), out var value);

                return new EquipmentBonus(
                    value?.Description ?? bonus.Name ?? "бонус",
                    value?.Value ?? 0,
                    bonus.Formula,
                    bonus.Condition,
                    value?.IsApplied ?? false);
            })
            .ToList();

    private static List<CharacterOptionDetail> BuildOptionDetails(Item item)
    {
        var details = new List<CharacterOptionDetail>();

        if (!string.IsNullOrWhiteSpace(item.ItemType))
        {
            details.Add(new CharacterOptionDetail("Тип", item.ItemType));
        }

        if (!string.IsNullOrWhiteSpace(item.Rarity))
        {
            details.Add(new CharacterOptionDetail("Редкость", item.Rarity));
        }

        if (item.Bonuses.Count > 0)
        {
            details.Add(new CharacterOptionDetail(
                "Бонусов",
                item.Bonuses.Count.ToString(System.Globalization.CultureInfo.CurrentCulture)));
        }

        return details;
    }

    /// <summary>
    /// Сообщает приложению, что параметры персонажа изменились: надетый предмет
    /// изменяет характеристики и ресурсы, поэтому лист должен перечитаться.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после публикации события.</returns>
    private Task PublishChangedAsync(Guid characterId, CancellationToken cancellationToken) =>
        _eventBus.PublishAsync(
            new CharacterChangedEvent(characterId, CharacterChangeKind.Recalculated),
            cancellationToken);
}
