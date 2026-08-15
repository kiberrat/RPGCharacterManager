using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Items;

/// <summary>
/// Бонус надетого предмета, показанный пользователю.
/// </summary>
/// <param name="Description">Что изменяет бонус.</param>
/// <param name="Value">Вычисленная величина бонуса.</param>
/// <param name="Formula">Формула бонуса.</param>
/// <param name="Condition">Условие действия бонуса.</param>
/// <param name="IsApplied">Условие выполнено и бонус действует.</param>
public sealed record EquipmentBonus(
    string Description,
    double Value,
    string? Formula,
    string? Condition,
    bool IsApplied);

/// <summary>
/// Предмет, надетый персонажем.
/// </summary>
/// <param name="InventoryItemId">Идентификатор записи инвентаря.</param>
/// <param name="ItemId">Идентификатор предмета.</param>
/// <param name="Name">Название предмета.</param>
/// <param name="Description">Описание предмета.</param>
/// <param name="ItemType">Тип предмета.</param>
/// <param name="Bonuses">Бонусы предмета вместе с вычисленными величинами.</param>
/// <param name="UnavailableReason">Причина, по которой требования предмета не выполнены.</param>
public sealed record EquippedItem(
    Guid InventoryItemId,
    Guid ItemId,
    string Name,
    string? Description,
    string? ItemType,
    IReadOnlyList<EquipmentBonus> Bonuses,
    string? UnavailableReason)
{
    /// <summary>Требования предмета выполнены.</summary>
    public bool IsAvailable => UnavailableReason is null;

    /// <summary>Предмет даёт бонусы.</summary>
    public bool HasBonuses => Bonuses.Count > 0;
}

/// <summary>
/// Слот экипировки персонажа вместе с надетыми в него предметами.
/// </summary>
/// <param name="SlotId">Идентификатор слота.</param>
/// <param name="Name">Название слота.</param>
/// <param name="Description">Пояснение к слоту.</param>
/// <param name="MaximumItems">Сколько предметов помещается в слот.</param>
/// <param name="Items">Надетые предметы.</param>
public sealed record EquipmentSlotState(
    Guid SlotId,
    string Name,
    string? Description,
    int MaximumItems,
    IReadOnlyList<EquippedItem> Items)
{
    /// <summary>В слоте есть свободное место.</summary>
    public bool HasFreeSpace => Items.Count < MaximumItems;

    /// <summary>Слот пуст.</summary>
    public bool IsEmpty => Items.Count == 0;
}

/// <summary>Один настраиваемый бонус авторской экипировки.</summary>
/// <param name="Target">Вид цели бонуса.</param>
/// <param name="AttributeId">Характеристика, если выбрана характеристика.</param>
/// <param name="ResourceId">Ресурс, если выбран максимум ресурса.</param>
/// <param name="Name">Имя переменной, тега или подпись бонуса.</param>
/// <param name="Formula">Формула величины.</param>
/// <param name="Condition">Необязательное условие.</param>
public sealed record LocalEquipmentBonusDraft(
    BonusTargetKind Target,
    Guid? AttributeId,
    Guid? ResourceId,
    string? Name,
    string? Formula,
    string? Condition);

/// <summary>Параметры авторской экипировки одного персонажа.</summary>
/// <param name="Name">Название предмета.</param>
/// <param name="Description">Описание предмета.</param>
/// <param name="ItemType">Тип предмета.</param>
/// <param name="Rarity">Редкость.</param>
/// <param name="Weight">Вес.</param>
/// <param name="Price">Стоимость.</param>
/// <param name="Currency">Валюта стоимости.</param>
/// <param name="Bonuses">Бонусы при надевании.</param>
public sealed record LocalEquipmentDraft(
    string Name,
    string? Description,
    string? ItemType,
    string? Rarity,
    double Weight,
    double Price,
    string? Currency,
    IReadOnlyList<LocalEquipmentBonusDraft> Bonuses);
/// <summary>
/// Экипировка персонажа: слоты, надевание предметов и их бонусы.
///
/// Подсистема не знает ни одного слота и ни одного вида усиления заранее: слоты
/// создаёт пользователь, а бонус описывается целью и формулой у самого предмета.
/// Все бонусы попадают в расчёт персонажа автоматически, поэтому надевание предмета
/// сразу изменяет характеристики, максимумы ресурсов и производные значения.
/// </summary>
public interface IEquipmentService
{
    /// <summary>
    /// Возвращает слоты экипировки персонажа вместе с надетыми предметами
    /// и вычисленными бонусами.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Состояние слотов либо описание ошибки.</returns>
    Task<Result<IReadOnlyList<EquipmentSlotState>>> GetSlotsAsync(
        Guid characterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает предметы, которые персонаж может надеть в указанный слот.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="slotId">Идентификатор слота.</param>
    /// <param name="search">Строка поиска по названию.</param>
    /// <param name="includeUnavailable">Показывать предметы с невыполненными требованиями.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница вариантов выбора.</returns>
    Task<CharacterOptionPage> GetAvailableItemsAsync(
        Guid characterId,
        Guid slotId,
        string? search,
        bool includeUnavailable,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Надевает предмет: выдаёт его персонажу, если у того предмета ещё нет,
    /// и занимает им свободное место в слоте.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="itemId">Идентификатор предмета.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат надевания.</returns>
    Task<Result> EquipAsync(
        Guid characterId,
        Guid itemId,
        CancellationToken cancellationToken = default);

    /// <summary>Создаёт авторскую экипировку только для персонажа и сразу надевает её.</summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="slotId">Слот экипировки.</param>
    /// <param name="draft">Параметры предмета и бонусов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор записи инвентаря либо ошибка.</returns>
    Task<Result<Guid>> CreateLocalAndEquipAsync(
        Guid characterId,
        Guid slotId,
        LocalEquipmentDraft draft,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Снимает предмет со слота экипировки. Сам предмет остаётся у персонажа.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="inventoryItemId">Идентификатор записи инвентаря.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат снятия.</returns>
    Task<Result> UnequipAsync(
        Guid characterId,
        Guid inventoryItemId,
        CancellationToken cancellationToken = default);
}
