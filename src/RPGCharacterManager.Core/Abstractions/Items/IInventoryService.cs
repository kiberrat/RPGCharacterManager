using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Items;

/// <summary>
/// Порядок, в котором показываются предметы инвентаря.
/// </summary>
public enum InventorySort
{
    /// <summary>По названию.</summary>
    Name = 0,

    /// <summary>По весу занимаемой ноши.</summary>
    Weight = 1,

    /// <summary>По стоимости.</summary>
    Price = 2,

    /// <summary>По количеству.</summary>
    Count = 3,

    /// <summary>По редкости.</summary>
    Rarity = 4,

    /// <summary>По времени получения.</summary>
    Added = 5,
}

/// <summary>
/// Отбор предметов инвентаря: поиск, категория и порядок показа.
/// </summary>
/// <param name="Search">Строка поиска по названию, типу, категории и пометке.</param>
/// <param name="CategoryId">
/// Категория, предметы которой показываются, вместе с вложенными категориями.
/// Значение <see langword="null"/> означает «все предметы».
/// </param>
/// <param name="Sort">Порядок показа.</param>
/// <param name="Descending">Обратный порядок.</param>
public sealed record InventoryQuery(
    string? Search = null,
    Guid? CategoryId = null,
    InventorySort Sort = InventorySort.Name,
    bool Descending = false);

/// <summary>
/// Раздел дерева категорий вместе с количеством предметов в нём.
/// </summary>
/// <param name="CategoryId">
/// Идентификатор категории либо <see langword="null"/> для раздела «Все предметы»
/// и раздела предметов, которым категория не назначена.
/// </param>
/// <param name="Name">Название раздела.</param>
/// <param name="Depth">Глубина вложенности: используется для отступа в списке.</param>
/// <param name="Count">Количество предметов в разделе с учётом вложенных категорий.</param>
public sealed record InventoryCategoryNode(Guid? CategoryId, string Name, int Depth, int Count);

/// <summary>
/// Действие, происходящее при использовании предмета.
/// </summary>
/// <param name="Description">Что происходит.</param>
/// <param name="Value">Вычисленная величина изменения.</param>
/// <param name="IsApplied">Приложение выполнило действие само.</param>
public sealed record InventoryUseEffect(string Description, double Value, bool IsApplied);

/// <summary>
/// Запись инвентаря персонажа, подготовленная к показу.
/// </summary>
/// <param name="InventoryItemId">Идентификатор записи инвентаря.</param>
/// <param name="ItemId">Идентификатор предмета.</param>
/// <param name="Name">Название предмета.</param>
/// <param name="Description">Описание предмета.</param>
/// <param name="CategoryName">Название категории предмета.</param>
/// <param name="ItemType">Тип предмета.</param>
/// <param name="Rarity">Редкость предмета.</param>
/// <param name="Count">Количество предметов в записи.</param>
/// <param name="UnitWeight">Вес единицы предмета.</param>
/// <param name="Weight">Вес записи вместе с содержимым, отнесённый на носителя.</param>
/// <param name="UnitPrice">Стоимость единицы предмета.</param>
/// <param name="Price">Стоимость записи.</param>
/// <param name="Currency">Валюта стоимости.</param>
/// <param name="RemainingCharges">Оставшиеся заряды предмета.</param>
/// <param name="MaximumCharges">Наибольшее количество зарядов предмета.</param>
/// <param name="UseCostDescription">Что расходует использование предмета.</param>
/// <param name="CanUse">Предмет можно использовать прямо сейчас.</param>
/// <param name="UnusableReason">Причина, по которой предмет нельзя использовать.</param>
/// <param name="IsContainer">Предмет вмещает другие предметы.</param>
/// <param name="Capacity">Вместимость вместилища.</param>
/// <param name="ContentWeight">Вес содержимого вместилища до его облегчения.</param>
/// <param name="ContainerId">Запись инвентаря, в которой лежит предмет.</param>
/// <param name="Depth">Глубина вложенности во вместилищах.</param>
/// <param name="IsEquipped">Предмет надет и потому не может быть убран.</param>
/// <param name="Note">Пометка пользователя.</param>
public sealed record InventoryEntry(
    Guid InventoryItemId,
    Guid ItemId,
    string Name,
    string? Description,
    string? CategoryName,
    string? ItemType,
    string? Rarity,
    int Count,
    double UnitWeight,
    double Weight,
    double UnitPrice,
    double Price,
    string? Currency,
    int? RemainingCharges,
    int? MaximumCharges,
    string? UseCostDescription,
    bool CanUse,
    string? UnusableReason,
    bool IsContainer,
    double? Capacity,
    double ContentWeight,
    Guid? ContainerId,
    int Depth,
    bool IsEquipped,
    string? Note)
{
    /// <summary>У предмета есть заряды.</summary>
    public bool HasCharges => MaximumCharges is > 0;

    /// <summary>Вместилище переполнено: в нём лежит больше, чем оно вмещает.</summary>
    public bool IsOverfilled => Capacity is { } capacity && ContentWeight > capacity;
}

/// <summary>
/// Итог по одной валюте: сколько стоит всё имущество персонажа в ней.
/// </summary>
/// <param name="Currency">Название валюты.</param>
/// <param name="Amount">Суммарная стоимость.</param>
public sealed record InventoryCurrencyTotal(string Currency, double Amount);

/// <summary>
/// Ноша персонажа.
/// </summary>
/// <param name="Total">Суммарный вес имущества.</param>
/// <param name="Capacity">
/// Переносимый вес по формуле игровой системы
/// либо <see langword="null"/>, если система его не ограничивает.
/// </param>
/// <param name="Unit">Единица измерения веса.</param>
public sealed record InventoryWeight(double Total, double? Capacity, string? Unit)
{
    /// <summary>Ноша превышает переносимый вес.</summary>
    public bool IsOverloaded => Capacity is { } capacity && Total > capacity;
}

/// <summary>
/// Инвентарь персонажа, подготовленный к показу.
/// </summary>
/// <param name="Entries">Записи инвентаря в выбранном порядке.</param>
/// <param name="Categories">Разделы дерева категорий вместе с количеством предметов.</param>
/// <param name="Weight">Ноша персонажа.</param>
/// <param name="Money">Стоимость имущества по валютам.</param>
/// <param name="Containers">Вместилища, в которые можно переложить предмет.</param>
/// <param name="TotalCount">Количество записей, подошедших под отбор.</param>
public sealed record InventoryState(
    IReadOnlyList<InventoryEntry> Entries,
    IReadOnlyList<InventoryCategoryNode> Categories,
    InventoryWeight Weight,
    IReadOnlyList<InventoryCurrencyTotal> Money,
    IReadOnlyList<InventoryContainerOption> Containers,
    int TotalCount);

/// <summary>
/// Вместилище, в которое можно переложить предмет.
/// </summary>
/// <param name="InventoryItemId">
/// Идентификатор записи вместилища
/// либо <see langword="null"/> для размещения вне вместилищ.
/// </param>
/// <param name="Name">Название вместилища.</param>
public sealed record InventoryContainerOption(Guid? InventoryItemId, string Name);

/// <summary>
/// Данные локального предмета, принадлежащего только одному персонажу.
/// </summary>
/// <param name="Name">Название.</param>
/// <param name="Description">Описание.</param>
/// <param name="ItemType">Пользовательский тип предмета.</param>
/// <param name="Weight">Вес одной единицы.</param>
/// <param name="Price">Стоимость одной единицы.</param>
/// <param name="Currency">Валюта стоимости.</param>
/// <param name="IsWeapon">Предмет является оружием.</param>
/// <param name="DamageFormula">Формула урона оружия.</param>
/// <param name="DamageType">Тип урона оружия.</param>
public sealed record LocalInventoryItemDraft(
    string Name,
    string? Description,
    string? ItemType,
    double Weight,
    double Price,
    string? Currency,
    bool IsWeapon,
    string? DamageFormula,
    string? DamageType);

/// <summary>
/// Итог использования предмета.
/// </summary>
/// <param name="ItemName">Название использованного предмета.</param>
/// <param name="Effects">Что произошло.</param>
/// <param name="SpentCharge">Израсходован заряд предмета.</param>
/// <param name="SpentUnit">Израсходована единица предмета.</param>
/// <param name="RemainingCharges">Оставшиеся заряды предмета.</param>
/// <param name="RemainingCount">Оставшееся количество предметов.</param>
/// <param name="Issues">Замечания вычисления формул.</param>
public sealed record ItemUseResult(
    string ItemName,
    IReadOnlyList<InventoryUseEffect> Effects,
    bool SpentCharge,
    bool SpentUnit,
    int? RemainingCharges,
    int RemainingCount,
    IReadOnlyList<string> Issues);

/// <summary>
/// Инвентарь персонажа: хранение предметов, их вес и стоимость, заряды,
/// использование, вместилища, поиск и сортировка.
///
/// Подсистема не знает правил ни одной игры. Категории, единицы веса, валюты,
/// вместимость вместилищ и действия предметов создаёт пользователь, а все
/// вычисления выполняет единый движок формул.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Возвращает инвентарь персонажа: отобранные записи, дерево категорий,
    /// ношу и стоимость имущества.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="query">Отбор предметов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Состояние инвентаря либо описание ошибки.</returns>
    Task<Result<InventoryState>> GetAsync(
        Guid characterId,
        InventoryQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает предметы, которые персонаж может получить.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="search">Строка поиска по названию.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница вариантов выбора.</returns>
    Task<CharacterOptionPage> GetAvailableItemsAsync(
        Guid characterId,
        string? search,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Выдаёт персонажу предмет. Складывающийся предмет добавляется
    /// к уже имеющейся стопке, пока та не достигнет предельного размера.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="itemId">Идентификатор предмета.</param>
    /// <param name="count">Количество.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат выдачи.</returns>
    Task<Result> AddAsync(
        Guid characterId,
        Guid itemId,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт предмет, доступный только указанному персонажу, и сразу
    /// добавляет его в инвентарь.
    /// </summary>
    Task<Result> CreateLocalAsync(
        Guid characterId,
        LocalInventoryItemDraft draft,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Изменяет количество предметов в записи инвентаря.
    /// Запись, оставшаяся без предметов, удаляется.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="inventoryItemId">Идентификатор записи инвентаря.</param>
    /// <param name="delta">Изменение количества.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат изменения.</returns>
    Task<Result> ChangeCountAsync(
        Guid characterId,
        Guid inventoryItemId,
        int delta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Убирает запись инвентаря целиком. Надетый предмет предварительно снимается,
    /// а содержимое вместилища перекладывается на его место.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="inventoryItemId">Идентификатор записи инвентаря.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат удаления.</returns>
    Task<Result> RemoveAsync(
        Guid characterId,
        Guid inventoryItemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Перекладывает предмет во вместилище или вынимает его наружу.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="inventoryItemId">Идентификатор перекладываемой записи.</param>
    /// <param name="containerId">
    /// Идентификатор записи вместилища
    /// либо <see langword="null"/> для размещения вне вместилищ.
    /// </param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат перемещения.</returns>
    Task<Result> MoveAsync(
        Guid characterId,
        Guid inventoryItemId,
        Guid? containerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Использует предмет: выполняет его действия и списывает израсходованное.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="inventoryItemId">Идентификатор записи инвентаря.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итог использования либо описание ошибки.</returns>
    Task<Result<ItemUseResult>> UseAsync(
        Guid characterId,
        Guid inventoryItemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Восстанавливает заряды предмета до наибольшего количества.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="inventoryItemId">Идентификатор записи инвентаря.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат восстановления.</returns>
    Task<Result> RestoreChargesAsync(
        Guid characterId,
        Guid inventoryItemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Записывает пометку пользователя к записи инвентаря.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="inventoryItemId">Идентификатор записи инвентаря.</param>
    /// <param name="note">Пометка либо <see langword="null"/>.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат сохранения.</returns>
    Task<Result> SetNoteAsync(
        Guid characterId,
        Guid inventoryItemId,
        string? note,
        CancellationToken cancellationToken = default);
}
