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
                .FirstOrDefaultAsync(entity => entity.Id == itemId, cancellationToken)
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
