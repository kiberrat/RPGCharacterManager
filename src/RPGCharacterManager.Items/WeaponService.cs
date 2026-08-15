using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Items;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Items;

/// <summary>
/// Оружие персонажа: расчёт боевых значений, атака и перезарядка.
///
/// Служба не знает правил ни одной конкретной игры. Кость попадания, бонус попадания,
/// урон и критический урон описаны формулами оружия; порог критического попадания,
/// расход боеприпасов и вместимость магазина заданы пользователем; всё, что происходит
/// после броска, описывается правилами событий «бой.попадание»
/// и «бой.критическое_попадание».
/// </summary>
public sealed class WeaponService : IWeaponService
{
    /// <summary>Количество вариантов оружия, загружаемых в список выбора за один раз.</summary>
    public const int AvailableWeaponPageSize = 200;

    /// <summary>
    /// Код действия, под которым атака записывается в журнал изменений.
    /// Совпадает с общим перечнем событий журнала.
    /// </summary>
    public const string AttackHistoryAction = HistoryActions.WeaponAttack;

    /// <summary>
    /// Код действия, под которым в журнал записывается критическое попадание.
    /// </summary>
    public const string CriticalHistoryAction = HistoryActions.CriticalHit;

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly ICharacterBuilderService _builder;
    private readonly IFormulaEngine _formulas;
    private readonly IRuleService _ruleService;
    private readonly IRuleEngine _ruleEngine;
    private readonly ILogger<WeaponService> _logger;

    /// <summary>
    /// Создаёт службу оружия.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="builder">Мастер создания персонажа: расчёт и проверка требований.</param>
    /// <param name="formulas">Единый движок вычислений.</param>
    /// <param name="ruleService">Хранилище игровых правил.</param>
    /// <param name="ruleEngine">Движок игровых правил.</param>
    /// <param name="logger">Журналировщик.</param>
    public WeaponService(
        IDbContextFactory<RpgDbContext> contextFactory,
        ICharacterBuilderService builder,
        IFormulaEngine formulas,
        IRuleService ruleService,
        IRuleEngine ruleEngine,
        ILogger<WeaponService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _builder = Guard.NotNull(builder);
        _formulas = Guard.NotNull(formulas);
        _ruleService = Guard.NotNull(ruleService);
        _ruleEngine = Guard.NotNull(ruleEngine);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CharacterWeapon>>> GetWeaponsAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var character = await LoadCharacterAsync(context, characterId, tracked: false, cancellationToken)
            .ConfigureAwait(false);

        if (character is null)
        {
            return Result.Failure<IReadOnlyList<CharacterWeapon>>(
                "Персонаж не найден: возможно, он был удалён.");
        }

        var records = GetWeaponRecords(character);
        var evaluation = await CreateEvaluationAsync(character, cancellationToken).ConfigureAwait(false);

        var ammunitionNames = await LoadAmmunitionNamesAsync(context, records, cancellationToken)
            .ConfigureAwait(false);

        var weapons = records
            .Select(record => BuildWeapon(character, record, evaluation, ammunitionNames))
            .ToList();

        return Result.Success<IReadOnlyList<CharacterWeapon>>(weapons);
    }

    /// <inheritdoc />
    public async Task<CharacterOptionPage> GetAvailableWeaponsAsync(
        Guid characterId,
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
            .Include(item => item.Weapon)
            .Where(item => item.Weapon != null)
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
            .Take(AvailableWeaponPageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var evaluation = await CreateEvaluationAsync(character, cancellationToken).ConfigureAwait(false);
        var options = new List<CharacterOption>(items.Count);

        foreach (var item in items)
        {
            var weapon = item.Weapon!;
            var scaling = WeaponScaling.Create(weapon, evaluation.Calculation);
            var reason = _builder.CheckRequirement(
                item.Requirements,
                scaling.CreateContext(evaluation.Character));

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
                BuildOptionDetails(item, weapon),
                item.Image));
        }

        return new CharacterOptionPage(options, totalCount);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> AddAsync(
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
                return Result.Failure<Guid>("Персонаж не найден: возможно, он был удалён.");
            }

            var item = await context.Items
                .Include(entity => entity.Weapon)
                .FirstOrDefaultAsync(
                    entity => entity.Id == itemId
                        && (entity.OwnerCharacterId == null || entity.OwnerCharacterId == characterId),
                    cancellationToken)
                .ConfigureAwait(false);

            if (item?.Weapon is not { } weapon)
            {
                return Result.Failure<Guid>("Предмет не является оружием.");
            }

            var record = new InventoryItem
            {
                CharacterId = character.Id,
                ItemId = item.Id,
                Count = 1,

                // Магазин выдаётся пустым: боеприпасы попадают в него только
                // перезарядкой, иначе запас персонажа увеличивался бы сам собой.
                LoadedAmmunition = weapon.MagazineSize is > 0 ? 0 : null,
            };

            context.Add(record);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            ItemsLog.WeaponAdded(_logger, item.Name, character.Name);

            return Result.Success(record.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ItemsLog.WeaponOperationFailed(_logger, exception, characterId);

            return Result.Failure<Guid>($"Не удалось выдать оружие: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateLocalAsync(
        Guid characterId,
        LocalWeaponDraft draft,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(draft);

        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            return Result.Failure<Guid>("Введите название оружия.");
        }

        if (!double.IsFinite(draft.Weight) || draft.Weight < 0 ||
            !double.IsFinite(draft.Price) || draft.Price < 0)
        {
            return Result.Failure<Guid>("Вес и стоимость оружия должны быть числами не меньше нуля.");
        }

        if (draft.CriticalThreshold is <= 0)
        {
            return Result.Failure<Guid>("Порог критического попадания должен быть больше нуля.");
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var character = await context.Characters
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.Id == characterId, cancellationToken)
                .ConfigureAwait(false);

            if (character is null)
            {
                return Result.Failure<Guid>("Персонаж не найден: возможно, он был удалён.");
            }

            if (draft.ScalingAttributeId is { } attributeId &&
                !await context.Attributes.AnyAsync(
                    attribute => attribute.Id == attributeId &&
                        (attribute.GameSystemId == null || attribute.GameSystemId == character.GameSystemId),
                    cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure<Guid>("Выбранная характеристика оружия не найдена.");
            }

            if (draft.ProficiencySkillId is { } skillId &&
                !await context.Skills.AnyAsync(
                    skill => skill.Id == skillId &&
                        (skill.GameSystemId == null || skill.GameSystemId == character.GameSystemId),
                    cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure<Guid>("Выбранный навык владения не найден.");
            }

            var item = new Item
            {
                OwnerCharacterId = characterId,
                GameSystemId = character.GameSystemId,
                Name = draft.Name.Trim(),
                SystemName = $"local_weapon_{characterId:N}_{Guid.NewGuid():N}",
                Source = "Авторское оружие персонажа",
                Description = string.IsNullOrWhiteSpace(draft.Description) ? null : draft.Description.Trim(),
                ItemType = string.IsNullOrWhiteSpace(draft.ItemType) ? "Авторское оружие" : draft.ItemType.Trim(),
                Weight = draft.Weight,
                Price = draft.Price,
                Currency = string.IsNullOrWhiteSpace(draft.Currency) ? null : draft.Currency.Trim(),
                Stackable = false,
                Weapon = new Weapon
                {
                    Category = string.IsNullOrWhiteSpace(draft.Category) ? null : draft.Category.Trim(),
                    Range = string.IsNullOrWhiteSpace(draft.Range) ? null : draft.Range.Trim(),
                    DamageType = string.IsNullOrWhiteSpace(draft.DamageType) ? null : draft.DamageType.Trim(),
                    Properties = string.IsNullOrWhiteSpace(draft.Properties) ? null : draft.Properties.Trim(),
                    AttackDiceFormula = string.IsNullOrWhiteSpace(draft.AttackDiceFormula)
                        ? "1к20"
                        : draft.AttackDiceFormula.Trim(),
                    AttackFormula = string.IsNullOrWhiteSpace(draft.AttackFormula)
                        ? null
                        : draft.AttackFormula.Trim(),
                    DamageFormula = string.IsNullOrWhiteSpace(draft.DamageFormula)
                        ? "1к6"
                        : draft.DamageFormula.Trim(),
                    CriticalFormula = string.IsNullOrWhiteSpace(draft.CriticalFormula)
                        ? null
                        : draft.CriticalFormula.Trim(),
                    CriticalThreshold = draft.CriticalThreshold,
                    ScalingAttributeId = draft.ScalingAttributeId,
                    ProficiencySkillId = draft.ProficiencySkillId,
                },
            };

            var inventory = new InventoryItem
            {
                CharacterId = characterId,
                ItemId = item.Id,
                Item = item,
                Count = 1,
            };

            context.Add(item);
            context.Add(inventory);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            ItemsLog.WeaponAdded(_logger, item.Name, character.Name);

            return Result.Success(inventory.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ItemsLog.WeaponOperationFailed(_logger, exception, characterId);
            return Result.Failure<Guid>($"Не удалось создать оружие: {exception.Message}");
        }
    }
    /// <inheritdoc />
    public async Task<Result> RemoveAsync(
        Guid characterId,
        Guid inventoryItemId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var record = await context.Inventory
                .FirstOrDefaultAsync(
                    item => item.Id == inventoryItemId && item.CharacterId == characterId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (record is null)
            {
                return Result.Failure("Оружие не найдено: возможно, оно уже убрано.");
            }

            context.Remove(record);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ItemsLog.WeaponOperationFailed(_logger, exception, characterId);

            return Result.Failure($"Не удалось убрать оружие: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> SetAmmunitionReserveAsync(
        Guid characterId,
        Guid inventoryItemId,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count < 0)
        {
            return Result.Failure("Количество боеприпасов не может быть отрицательным.");
        }

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

            if (FindWeapon(character, inventoryItemId) is not { } found)
            {
                return Result.Failure("Оружие не найдено: возможно, оно уже убрано.");
            }

            if (found.Weapon.AmmunitionItemId is not { } ammunitionItemId)
            {
                return Result.Failure($"Оружию «{found.Item.Name}» боеприпасы не нужны.");
            }

            AmmunitionStore.SetReserve(character, ammunitionItemId, count, context);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ItemsLog.WeaponOperationFailed(_logger, exception, characterId);

            return Result.Failure($"Не удалось изменить запас боеприпасов: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<WeaponAttackResult>> AttackAsync(
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
                return Result.Failure<WeaponAttackResult>("Персонаж не найден: возможно, он был удалён.");
            }

            if (FindWeapon(character, inventoryItemId) is not { } found)
            {
                return Result.Failure<WeaponAttackResult>("Оружие не найдено: возможно, оно уже убрано.");
            }

            var evaluation = await CreateEvaluationAsync(character, cancellationToken).ConfigureAwait(false);
            var scaling = WeaponScaling.Create(found.Weapon, evaluation.Calculation);
            var formulaContext = scaling.CreateContext(evaluation.Character);

            if (_builder.CheckRequirement(found.Item.Requirements, formulaContext) is { } reason)
            {
                return Result.Failure<WeaponAttackResult>(
                    $"Персонаж не может применить оружие «{found.Item.Name}». {reason}");
            }

            var spending = SpendAmmunition(character, found, context);

            if (spending.IsFailure)
            {
                return Result.Failure<WeaponAttackResult>(spending.Error!);
            }

            var attack = RollAttack(found.Weapon, formulaContext);
            var damage = RollDamage(found.Weapon, formulaContext, attack.IsCritical);

            var applied = await ApplyCombatRulesAsync(
                    evaluation.Character,
                    scaling,
                    found,
                    attack,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);

            // Правило боя могло изменить итог попадания, но не могло создать бросок:
            // если кость не задана, попадания как числа у оружия нет.
            var total = attack.Total is null ? (double?)null : applied.Attack;

            var result = new WeaponAttackResult(
                found.Item.Name,
                attack.Roll,
                attack.Bonus,
                total,
                attack.IsCritical,
                applied.Damage,
                found.Weapon.DamageType,
                spending.Value.Spent,
                spending.Value.Left,
                applied.Rules,
                string.Empty);

            result = result with { Description = BuildAttackDescription(result) };

            RecordAttack(context, character, found.Item, result);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            ItemsLog.WeaponAttacked(_logger, character.Name, found.Item.Name, applied.Damage);

            return Result.Success(result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ItemsLog.WeaponOperationFailed(_logger, exception, characterId);

            return Result.Failure<WeaponAttackResult>($"Не удалось выполнить атаку: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<WeaponReloadResult>> ReloadAsync(
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
                return Result.Failure<WeaponReloadResult>("Персонаж не найден: возможно, он был удалён.");
            }

            if (FindWeapon(character, inventoryItemId) is not { } found)
            {
                return Result.Failure<WeaponReloadResult>("Оружие не найдено: возможно, оно уже убрано.");
            }

            var weapon = found.Weapon;

            if (weapon.MagazineSize is not > 0)
            {
                return Result.Failure<WeaponReloadResult>(
                    $"Оружию «{found.Item.Name}» перезарядка не требуется: магазина у него нет.");
            }

            if (weapon.AmmunitionItemId is not { } ammunitionItemId)
            {
                return Result.Failure<WeaponReloadResult>(
                    $"Для оружия «{found.Item.Name}» не выбран боеприпас.");
            }

            var magazineSize = weapon.MagazineSize.Value;
            var loaded = found.Record.LoadedAmmunition ?? 0;
            var required = magazineSize - loaded;

            if (required <= 0)
            {
                return Result.Failure<WeaponReloadResult>("Магазин уже полон.");
            }

            var reserve = AmmunitionStore.CountReserve(character, ammunitionItemId);

            if (reserve <= 0)
            {
                var ammunitionName = await LoadItemNameAsync(context, ammunitionItemId, cancellationToken)
                    .ConfigureAwait(false);

                return Result.Failure<WeaponReloadResult>(
                    $"Боеприпасов «{ammunitionName}» в запасе нет.");
            }

            var taken = Math.Min(required, reserve);

            AmmunitionStore.TryConsume(character, ammunitionItemId, taken, context);
            found.Record.LoadedAmmunition = loaded + taken;

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            ItemsLog.WeaponReloaded(_logger, character.Name, found.Item.Name, taken);

            var result = new WeaponReloadResult(
                found.Item.Name,
                found.Record.LoadedAmmunition.Value,
                magazineSize,
                reserve - taken,
                weapon.ReloadTime,
                BuildReloadDescription(found.Item.Name, taken, found.Record.LoadedAmmunition.Value, magazineSize, weapon.ReloadTime));

            return Result.Success(result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ItemsLog.WeaponOperationFailed(_logger, exception, characterId);

            return Result.Failure<WeaponReloadResult>($"Не удалось перезарядить оружие: {exception.Message}");
        }
    }

    /// <summary>
    /// Загружает персонажа вместе со связанными данными и оружием инвентаря.
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
                .ThenInclude(item => item!.Weapon)
                .ThenInclude(weapon => weapon!.ScalingAttribute)
            .Include(character => character.Inventory)
                .ThenInclude(record => record.Item)
                .ThenInclude(item => item!.Weapon)
                .ThenInclude(weapon => weapon!.ProficiencySkill);

        return tracked
            ? query.FirstOrDefaultAsync(character => character.Id == characterId, cancellationToken)
            : query.AsNoTracking()
                .FirstOrDefaultAsync(character => character.Id == characterId, cancellationToken);
    }

    /// <summary>
    /// Возвращает записи инвентаря, содержащие оружие, в порядке отображения.
    /// </summary>
    /// <param name="character">Персонаж с загруженным инвентарём.</param>
    /// <returns>Записи инвентаря с оружием.</returns>
    private static List<InventoryItem> GetWeaponRecords(Character character) => character.Inventory
        .Where(record => record.Item?.Weapon is not null)
        .OrderBy(record => record.Item!.Name, StringComparer.CurrentCulture)
        .ToList();

    /// <summary>
    /// Находит оружие персонажа по записи инвентаря.
    /// </summary>
    /// <param name="character">Персонаж с загруженным инвентарём.</param>
    /// <param name="inventoryItemId">Идентификатор записи инвентаря.</param>
    /// <returns>Найденное оружие либо <see langword="null"/>.</returns>
    private static WeaponRecord? FindWeapon(Character character, Guid inventoryItemId)
    {
        var record = character.Inventory.FirstOrDefault(item => item.Id == inventoryItemId);

        return record?.Item?.Weapon is { } weapon
            ? new WeaponRecord(record, record.Item, weapon)
            : null;
    }

    /// <summary>
    /// Выполняет расчёт персонажа и создаёт объект правил, к которому обращаются
    /// формулы оружия и требования.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Данные расчёта.</returns>
    private async Task<WeaponEvaluation> CreateEvaluationAsync(
        Character character,
        CancellationToken cancellationToken)
    {
        var draft = _builder.CreateDraft(character);

        // Расчёт нужен целиком: из него берутся модификаторы характеристик и значения
        // навыков, которых нет среди переменных объекта правил.
        var calculation = await _builder.CalculateAsync(draft, cancellationToken).ConfigureAwait(false);
        var target = await _builder.CreateContextAsync(draft, cancellationToken).ConfigureAwait(false);

        return new WeaponEvaluation(target, calculation);
    }

    private static async Task<Dictionary<Guid, string>> LoadAmmunitionNamesAsync(
        RpgDbContext context,
        IReadOnlyList<InventoryItem> records,
        CancellationToken cancellationToken)
    {
        var identifiers = records
            .Select(record => record.Item!.Weapon!.AmmunitionItemId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (identifiers.Count == 0)
        {
            return [];
        }

        return await context.Items
            .AsNoTracking()
            .Where(item => identifiers.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<string> LoadItemNameAsync(
        RpgDbContext context,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var name = await context.Items
            .AsNoTracking()
            .Where(item => item.Id == itemId)
            .Select(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return name ?? "боеприпас";
    }

    /// <summary>
    /// Собирает карточку оружия: вычисляет бонус попадания, диапазон урона,
    /// состояние боеприпасов и проверяет требования.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="record">Запись инвентаря с оружием.</param>
    /// <param name="evaluation">Данные расчёта персонажа.</param>
    /// <param name="ammunitionNames">Названия боеприпасов по идентификатору.</param>
    /// <returns>Карточка оружия.</returns>
    private CharacterWeapon BuildWeapon(
        Character character,
        InventoryItem record,
        WeaponEvaluation evaluation,
        IReadOnlyDictionary<Guid, string> ammunitionNames)
    {
        var item = record.Item!;
        var weapon = item.Weapon!;

        var scaling = WeaponScaling.Create(weapon, evaluation.Calculation);
        var context = scaling.CreateContext(evaluation.Character);

        var issues = new List<string>();

        var attackBonus = EvaluateNumber(weapon.AttackFormula, context, "бонус попадания", issues);
        var damage = EvaluateRange(weapon.DamageFormula, context, "урон", issues);

        return new CharacterWeapon(
            record.Id,
            item.Id,
            item.Name,
            item.Description,
            weapon.Category,
            item.ItemType,
            weapon.Range,
            weapon.DamageType,
            WeaponProperties.Parse(weapon.Properties),
            weapon.AttackDiceFormula,
            weapon.AttackFormula,
            attackBonus,
            weapon.DamageFormula,
            damage,
            weapon.CriticalFormula,
            weapon.CriticalThreshold,
            scaling.AttributeName,
            scaling.SkillName,
            scaling.ProficiencyLevel,
            weapon.ReloadTime,
            BuildAmmunition(character, record, weapon, ammunitionNames),
            _builder.CheckRequirement(item.Requirements, context),
            issues);
    }

    private static WeaponAmmunition? BuildAmmunition(
        Character character,
        InventoryItem record,
        Weapon weapon,
        IReadOnlyDictionary<Guid, string> ammunitionNames)
    {
        if (weapon.AmmunitionItemId is not { } ammunitionItemId || weapon.AmmunitionPerShot <= 0)
        {
            return null;
        }

        var hasMagazine = weapon.MagazineSize is > 0;

        return new WeaponAmmunition(
            ammunitionItemId,
            ammunitionNames.TryGetValue(ammunitionItemId, out var name) ? name : "боеприпас",
            weapon.AmmunitionPerShot,
            hasMagazine ? weapon.MagazineSize : null,
            hasMagazine ? record.LoadedAmmunition ?? 0 : null,
            AmmunitionStore.CountReserve(character, ammunitionItemId));
    }

    private static List<CharacterOptionDetail> BuildOptionDetails(Item item, Weapon weapon)
    {
        var details = new List<CharacterOptionDetail>();

        if (!string.IsNullOrWhiteSpace(weapon.Category))
        {
            details.Add(new CharacterOptionDetail("Категория", weapon.Category));
        }

        if (!string.IsNullOrWhiteSpace(item.ItemType))
        {
            details.Add(new CharacterOptionDetail("Тип", item.ItemType));
        }

        if (!string.IsNullOrWhiteSpace(weapon.DamageFormula))
        {
            details.Add(new CharacterOptionDetail("Урон", weapon.DamageFormula));
        }

        if (!string.IsNullOrWhiteSpace(weapon.DamageType))
        {
            details.Add(new CharacterOptionDetail("Тип урона", weapon.DamageType));
        }

        if (!string.IsNullOrWhiteSpace(weapon.Range))
        {
            details.Add(new CharacterOptionDetail("Дальность", weapon.Range));
        }

        return details;
    }

    /// <summary>
    /// Расходует боеприпасы на одну атаку.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="found">Оружие персонажа.</param>
    /// <param name="context">Контекст базы данных.</param>
    /// <returns>Израсходованное количество и остаток либо причина отказа.</returns>
    private static Result<AmmunitionSpending> SpendAmmunition(
        Character character,
        WeaponRecord found,
        RpgDbContext context)
    {
        var weapon = found.Weapon;

        if (weapon.AmmunitionItemId is not { } ammunitionItemId || weapon.AmmunitionPerShot <= 0)
        {
            return Result.Success(new AmmunitionSpending(0, null));
        }

        var required = weapon.AmmunitionPerShot;

        if (weapon.MagazineSize is > 0)
        {
            var loaded = found.Record.LoadedAmmunition ?? 0;

            if (loaded < required)
            {
                return Result.Failure<AmmunitionSpending>(
                    $"Оружию «{found.Item.Name}» требуется перезарядка: в магазине "
                    + $"{Format(loaded)} из {Format(weapon.MagazineSize.Value)}.");
            }

            found.Record.LoadedAmmunition = loaded - required;

            return Result.Success(new AmmunitionSpending(required, loaded - required));
        }

        var reserve = AmmunitionStore.CountReserve(character, ammunitionItemId);

        if (reserve < required)
        {
            return Result.Failure<AmmunitionSpending>(
                $"Оружию «{found.Item.Name}» не хватает боеприпасов: "
                + $"нужно {Format(required)}, в запасе {Format(reserve)}.");
        }

        AmmunitionStore.TryConsume(character, ammunitionItemId, required, context);

        return Result.Success(new AmmunitionSpending(required, reserve - required));
    }

    /// <summary>
    /// Выполняет бросок попадания и определяет, является ли оно критическим.
    /// Критическое попадание определяется по выпавшему значению кости, а не по итогу
    /// броска: бонусы не должны превращать обычное попадание в критическое.
    /// </summary>
    /// <param name="weapon">Оружие.</param>
    /// <param name="context">Источник значений переменных.</param>
    /// <returns>Результат броска попадания.</returns>
    private AttackRoll RollAttack(Weapon weapon, IFormulaContext context)
    {
        var issues = new List<string>();

        double? roll = string.IsNullOrWhiteSpace(weapon.AttackDiceFormula)
            ? null
            : EvaluateNumber(weapon.AttackDiceFormula, context, "кость попадания", issues);

        var bonus = EvaluateNumber(weapon.AttackFormula, context, "бонус попадания", issues);

        var isCritical = weapon.CriticalThreshold is { } threshold
            && roll is { } value
            && value >= threshold;

        return new AttackRoll(roll, bonus, roll is { } total ? total + bonus : null, isCritical);
    }

    /// <summary>
    /// Вычисляет урон атаки. При критическом попадании применяется формула
    /// критического урона, получающая обычный урон в переменной «урон».
    /// </summary>
    /// <param name="weapon">Оружие.</param>
    /// <param name="context">Источник значений переменных.</param>
    /// <param name="isCritical">Попадание оказалось критическим.</param>
    /// <returns>Нанесённый урон.</returns>
    private double RollDamage(Weapon weapon, IFormulaContext context, bool isCritical)
    {
        var issues = new List<string>();
        var damage = EvaluateNumber(weapon.DamageFormula, context, "урон", issues);

        if (!isCritical || string.IsNullOrWhiteSpace(weapon.CriticalFormula))
        {
            return damage;
        }

        var criticalContext = new Core.Models.Engine.LocalFormulaContext(context)
            .With(WeaponVariables.Damage, damage);

        return EvaluateNumber(weapon.CriticalFormula, criticalContext, "критический урон", issues);
    }

    /// <summary>
    /// Применяет правила боя к результату атаки.
    ///
    /// Правило события «бой.попадание» видит бросок, итог попадания, урон, свойства
    /// оружия и все параметры персонажа, поэтому уникальные механики оружия — от
    /// «после критического попадания следующая атака сильнее» до «чем меньше здоровья,
    /// тем больше урон» — описываются правилами, а не кодом приложения.
    /// </summary>
    /// <param name="target">Объект правил персонажа.</param>
    /// <param name="scaling">Значения масштабирования оружия.</param>
    /// <param name="found">Оружие персонажа.</param>
    /// <param name="attack">Результат броска попадания.</param>
    /// <param name="damage">Вычисленный урон.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итоговые попадание и урон вместе с названиями применённых правил.</returns>
    private async Task<CombatOutcome> ApplyCombatRulesAsync(
        IRuleTarget target,
        WeaponScaling scaling,
        WeaponRecord found,
        AttackRoll attack,
        double damage,
        CancellationToken cancellationToken)
    {
        scaling.ApplyTo(target);

        target.AddTag(found.Item.SystemName);

        foreach (var property in WeaponProperties.Parse(found.Weapon.Properties))
        {
            target.AddTag(property);
        }

        target.SetVariable(WeaponVariables.Roll, FormulaValue.FromNumber(attack.Roll ?? 0));
        target.SetVariable(WeaponVariables.Attack, FormulaValue.FromNumber(attack.Total ?? attack.Bonus));
        target.SetVariable(WeaponVariables.Damage, FormulaValue.FromNumber(damage));

        var applied = new List<string>();

        await ExecuteAsync(RuleTriggers.CombatHit).ConfigureAwait(false);

        if (attack.IsCritical)
        {
            await ExecuteAsync(RuleTriggers.CombatCriticalHit).ConfigureAwait(false);
        }

        return new CombatOutcome(
            ReadNumber(target, WeaponVariables.Attack, attack.Total ?? attack.Bonus),
            ReadNumber(target, WeaponVariables.Damage, damage),
            applied);

        async Task ExecuteAsync(string trigger)
        {
            var rules = await _ruleService.GetByTriggerAsync(trigger, cancellationToken).ConfigureAwait(false);

            if (rules.Count == 0)
            {
                return;
            }

            applied.AddRange(_ruleEngine.Execute(trigger, target, rules).ExecutedRules);
        }
    }

    /// <summary>
    /// Записывает атаку в журнал бросков и в журнал изменений персонажа.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="character">Персонаж.</param>
    /// <param name="item">Предмет-оружие.</param>
    /// <param name="result">Результат атаки.</param>
    private static void RecordAttack(
        RpgDbContext context,
        Character character,
        Item item,
        WeaponAttackResult result)
    {
        context.Add(new DiceRoll
        {
            CharacterId = character.Id,
            Label = $"Атака: {item.Name}",
            Formula = item.Weapon?.DamageFormula ?? string.Empty,
            Result = result.Damage,
            DetailsJson = JsonSerializer.Serialize(new
            {
                бросок = result.Roll,
                бонус = result.AttackBonus,
                попадание = result.AttackTotal,
                критическое = result.IsCritical,
                урон = result.Damage,
                тип_урона = result.DamageType,
                боеприпасы = result.AmmunitionSpent,
            }),
        });

        context.Add(new HistoryEntry
        {
            CharacterId = character.Id,

            // Критом считается то, что признало критом оружие: порог задаёт
            // пользователь, а приложение правил игры не знает.
            Action = result.IsCritical ? CriticalHistoryAction : AttackHistoryAction,
            Subject = result.WeaponName,
            Description = result.Description,
            NewValue = Format(result.Damage),
            Amount = result.Damage,
        });
    }

    private double EvaluateNumber(
        string? formula,
        IFormulaContext context,
        string description,
        List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return 0;
        }

        var result = _formulas.Evaluate(formula, context);

        if (result.IsSuccess)
        {
            return result.Value.AsNumber();
        }

        issues.Add($"Формула «{description}»: {result.Error}");
        return 0;
    }

    private FormulaRange? EvaluateRange(
        string? formula,
        IFormulaContext context,
        string description,
        List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return null;
        }

        var result = _formulas.EvaluateRange(formula, context);

        if (result.IsSuccess)
        {
            return result.Value;
        }

        issues.Add($"Формула «{description}»: {result.Error}");
        return null;
    }

    private static double ReadNumber(IRuleTarget target, string name, double fallback) =>
        target.TryGetVariable(name, out var value) ? value.AsNumber() : fallback;

    private static string BuildAttackDescription(WeaponAttackResult result)
    {
        var parts = new List<string>();

        if (result.AttackTotal is { } total)
        {
            parts.Add($"Попадание {Format(total)} (кость {Format(result.Roll ?? 0)}, "
                + $"бонус {Format(result.AttackBonus)})");
        }
        else if (Math.Abs(result.AttackBonus) > double.Epsilon)
        {
            parts.Add($"Бонус попадания {Format(result.AttackBonus)}");
        }

        if (result.IsCritical)
        {
            parts.Add("критическое попадание");
        }

        parts.Add(string.IsNullOrWhiteSpace(result.DamageType)
            ? $"урон {Format(result.Damage)}"
            : $"урон {Format(result.Damage)} ({result.DamageType})");

        if (result.AmmunitionSpent > 0)
        {
            parts.Add(result.AmmunitionLeft is { } left
                ? $"израсходовано {Format(result.AmmunitionSpent)}, осталось {Format(left)}"
                : $"израсходовано {Format(result.AmmunitionSpent)}");
        }

        return $"{result.WeaponName}: {string.Join(", ", parts)}.";
    }

    private static string BuildReloadDescription(
        string weaponName,
        int taken,
        int loaded,
        int magazineSize,
        string? reloadTime)
    {
        var text = $"{weaponName}: заряжено {Format(taken)}, в магазине "
            + $"{Format(loaded)} из {Format(magazineSize)}.";

        return string.IsNullOrWhiteSpace(reloadTime) ? text : $"{text} Перезарядка: {reloadTime}.";
    }

    private static string Format(double value) => value.ToString("0.####", CultureInfo.CurrentCulture);

    /// <summary>Оружие персонажа вместе с записью инвентаря и описанием предмета.</summary>
    /// <param name="Record">Запись инвентаря.</param>
    /// <param name="Item">Предмет.</param>
    /// <param name="Weapon">Оружейные свойства предмета.</param>
    private sealed record WeaponRecord(InventoryItem Record, Item Item, Weapon Weapon);

    /// <summary>Данные расчёта персонажа, требуемые формулам оружия.</summary>
    /// <param name="Character">Объект правил персонажа.</param>
    /// <param name="Calculation">Результат расчёта параметров персонажа.</param>
    private sealed record WeaponEvaluation(IRuleTarget Character, CharacterCalculation Calculation);

    /// <summary>Расход боеприпасов на атаку.</summary>
    /// <param name="Spent">Израсходованное количество.</param>
    /// <param name="Left">Остаток в магазине либо в запасе.</param>
    private sealed record AmmunitionSpending(int Spent, int? Left);

    /// <summary>Результат броска попадания.</summary>
    /// <param name="Roll">Выпавшее значение кости.</param>
    /// <param name="Bonus">Бонус попадания.</param>
    /// <param name="Total">Итог броска.</param>
    /// <param name="IsCritical">Попадание критическое.</param>
    private sealed record AttackRoll(double? Roll, double Bonus, double? Total, bool IsCritical);

    /// <summary>Итог применения правил боя.</summary>
    /// <param name="Attack">Итоговое попадание.</param>
    /// <param name="Damage">Итоговый урон.</param>
    /// <param name="Rules">Названия применённых правил.</param>
    private sealed record CombatOutcome(double Attack, double Damage, IReadOnlyList<string> Rules);
}
