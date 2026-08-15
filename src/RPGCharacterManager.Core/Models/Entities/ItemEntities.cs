namespace RPGCharacterManager.Core.Models.Entities;

/// <summary>
/// Категория предметов инвентаря.
///
/// Категории образуют дерево произвольной глубины: «Снаряжение → Броня → Шлемы».
/// Приложение не содержит ни одной готовой категории — их состав целиком
/// определяет пользователь, поэтому одинаково описываются и меч, и микросхема.
/// </summary>
public class ItemCategory : ContentEntity
{
    /// <summary>
    /// Идентификатор вышестоящей категории.
    /// Значение <see langword="null"/> означает категорию верхнего уровня.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>Вышестоящая категория.</summary>
    public ItemCategory? Parent { get; set; }

    /// <summary>Вложенные категории.</summary>
    public ICollection<ItemCategory> Children { get; set; } = [];

    /// <summary>Порядок отображения категории среди соседних.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Что расходуется при использовании предмета.
/// </summary>
public enum ItemUseCost
{
    /// <summary>Ничего: предмет можно использовать сколько угодно раз.</summary>
    None = 0,

    /// <summary>Один заряд предмета.</summary>
    Charge = 1,

    /// <summary>Одна единица предмета: он расходуется.</summary>
    Unit = 2,
}

/// <summary>
/// Действие, происходящее при использовании предмета.
///
/// Одна запись описывает любое применение: «восстановить 2к4+2 здоровья»,
/// «потратить 1 ману», «дать 5 выносливости». Перечня возможных действий в
/// приложении нет: пользователь выбирает ресурс и записывает формулу изменения.
/// </summary>
public class ItemUseEffect : EntityBase
{
    /// <summary>Идентификатор предмета.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Предмет.</summary>
    public Item? Item { get; set; }

    /// <summary>Идентификатор изменяемого ресурса.</summary>
    public Guid? ResourceId { get; set; }

    /// <summary>Изменяемый ресурс.</summary>
    public GameResource? Resource { get; set; }

    /// <summary>
    /// Пояснение к действию. Показывается, когда ресурс не выбран:
    /// так описывается применение, которое приложение ещё не умеет выполнять само.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Формула изменения ресурса. Положительная величина восстанавливает ресурс,
    /// отрицательная — расходует. Вычисляется по параметрам персонажа.
    /// </summary>
    public string? Formula { get; set; }

    /// <summary>Порядок применения действия.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Предмет: любой объект инвентаря.
/// </summary>
public class Item : ContentEntity
{
    /// <summary>Тип предмета, задаваемый игровой системой.</summary>
    public string? ItemType { get; set; }

    /// <summary>Идентификатор категории предмета.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Категория предмета.</summary>
    public ItemCategory? Category { get; set; }

    /// <summary>Редкость предмета.</summary>
    public string? Rarity { get; set; }

    /// <summary>Вес единицы предмета.</summary>
    public double Weight { get; set; }

    /// <summary>Стоимость единицы предмета.</summary>
    public double Price { get; set; }

    /// <summary>Наименование валюты стоимости.</summary>
    public string? Currency { get; set; }

    /// <summary>Предметы складываются в стопку.</summary>
    public bool Stackable { get; set; }

    /// <summary>Максимальный размер стопки.</summary>
    public int? MaximumStackSize { get; set; }

    /// <summary>Требования к использованию предмета в виде выражения.</summary>
    public string? Requirements { get; set; }

    /// <summary>Формула количества зарядов предмета.</summary>
    public string? ChargesFormula { get; set; }

    /// <summary>Что расходуется при использовании предмета.</summary>
    public ItemUseCost UseCost { get; set; }

    /// <summary>
    /// Что происходит при использовании предмета.
    /// Пустой список вместе с расходом единицы описывает предмет,
    /// который просто тратится, — факел, паёк или заряд взрывчатки.
    /// </summary>
    public ICollection<ItemUseEffect> UseEffects { get; set; } = [];

    /// <summary>Предмет вмещает другие предметы: сумка, сундук, ящик, отсек.</summary>
    public bool IsContainer { get; set; }

    /// <summary>
    /// Вместимость контейнера, выраженная в единицах веса.
    /// Значение <see langword="null"/> означает, что вместимость не ограничена.
    /// </summary>
    public double? Capacity { get; set; }

    /// <summary>
    /// Доля веса содержимого, которую контейнер передаёт носителю.
    /// Единица — обычный мешок, ноль — безразмерная сумка хранения,
    /// половина — магический контейнер, облегчающий ношу.
    /// </summary>
    public double ContentWeightFactor { get; set; } = 1;

    /// <summary>
    /// Идентификатор слота экипировки, в который надевается предмет.
    /// Значение <see langword="null"/> означает, что предмет не надевается.
    /// </summary>
    public Guid? EquipmentSlotId { get; set; }

    /// <summary>Слот экипировки, в который надевается предмет.</summary>
    public EquipmentSlot? EquipmentSlot { get; set; }

    /// <summary>Оружейные свойства предмета, если предмет является оружием.</summary>
    public Weapon? Weapon { get; set; }

    /// <summary>
    /// Бонусы, которые предмет даёт персонажу, пока он надет.
    /// Броня, кольцо, имплант и артефакт описываются одним и тем же списком бонусов.
    /// </summary>
    public ICollection<ItemBonus> Bonuses { get; set; } = [];
}

/// <summary>
/// Что изменяет бонус экипировки.
/// </summary>
public enum BonusTargetKind
{
    /// <summary>Значение характеристики персонажа.</summary>
    Attribute = 0,

    /// <summary>Максимум ресурса персонажа: здоровья, маны, зарядов.</summary>
    Resource = 1,

    /// <summary>Именованная величина, которую использует игровая система.</summary>
    Variable = 2,

    /// <summary>Признак объекта: правила и требования могут его проверять.</summary>
    Tag = 3,
}

/// <summary>
/// Бонус, который предмет даёт персонажу, пока он надет.
///
/// Одна запись описывает любое усиление: «+2 к Силе», «+10 к максимуму здоровья»,
/// «защита = 12 + модификатор Ловкости», «признак: сопротивление огню». Приложение
/// не содержит перечня возможных бонусов — его составляет пользователь.
/// </summary>
public class ItemBonus : EntityBase
{
    /// <summary>Идентификатор предмета.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Предмет.</summary>
    public Item? Item { get; set; }

    /// <summary>Что изменяет бонус.</summary>
    public BonusTargetKind Target { get; set; }

    /// <summary>Идентификатор изменяемой характеристики.</summary>
    public Guid? AttributeId { get; set; }

    /// <summary>Изменяемая характеристика.</summary>
    public AttributeDefinition? Attribute { get; set; }

    /// <summary>Идентификатор изменяемого ресурса.</summary>
    public Guid? ResourceId { get; set; }

    /// <summary>Изменяемый ресурс.</summary>
    public GameResource? Resource { get; set; }

    /// <summary>Имя величины или признака, если бонус не привязан к объекту контента.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Формула величины бонуса. Признаку формула не нужна.
    /// Вычисляется по параметрам персонажа без учёта надетых предметов.
    /// </summary>
    public string? Formula { get; set; }

    /// <summary>
    /// Условие, при котором бонус действует.
    /// Пустое значение означает, что бонус действует всегда.
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>Порядок отображения бонуса.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Оружейные свойства предмета.
///
/// Ни одно поле не описывает механику конкретной игры: бросок попадания, урон,
/// критическое попадание и расход боеприпасов задаются формулами и ссылками на
/// объекты, созданные пользователем.
/// </summary>
public class Weapon : EntityBase
{
    /// <summary>Идентификатор предмета.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Предмет.</summary>
    public Item? Item { get; set; }

    /// <summary>Категория оружия: ближнее, дальнобойное, магическое или любая своя.</summary>
    public string? Category { get; set; }

    /// <summary>
    /// Формула броска попадания: кость, по которой определяется критическое попадание.
    /// Например <c>1d20</c> или <c>1d100</c>.
    /// </summary>
    public string? AttackDiceFormula { get; set; }

    /// <summary>
    /// Формула бонуса попадания, прибавляемого к броску кости.
    /// Например <c>характеристика + владение</c>.
    /// </summary>
    public string? AttackFormula { get; set; }

    /// <summary>Формула броска урона.</summary>
    public string? DamageFormula { get; set; }

    /// <summary>Тип наносимого урона.</summary>
    public string? DamageType { get; set; }

    /// <summary>
    /// Формула урона при критическом попадании.
    /// Получает обычный урон в переменной «урон».
    /// </summary>
    public string? CriticalFormula { get; set; }

    /// <summary>
    /// Значение кости попадания, начиная с которого попадание считается критическим.
    /// Значение <see langword="null"/> означает, что критических попаданий нет.
    /// </summary>
    public int? CriticalThreshold { get; set; }

    /// <summary>Дальность применения.</summary>
    public string? Range { get; set; }

    /// <summary>
    /// Идентификатор характеристики, от которой зависят формулы оружия.
    /// Её значение доступно формулам в переменной «значение», модификатор — в «характеристика».
    /// </summary>
    public Guid? ScalingAttributeId { get; set; }

    /// <summary>Характеристика масштабирования.</summary>
    public AttributeDefinition? ScalingAttribute { get; set; }

    /// <summary>
    /// Идентификатор навыка, выражающего владение оружием.
    /// Уровень владения доступен формулам в переменной «владение», значение навыка — в «навык».
    /// </summary>
    public Guid? ProficiencySkillId { get; set; }

    /// <summary>Навык владения оружием.</summary>
    public Skill? ProficiencySkill { get; set; }

    /// <summary>Идентификатор предмета, используемого как боеприпас.</summary>
    public Guid? AmmunitionItemId { get; set; }

    /// <summary>Количество боеприпасов, расходуемое одной атакой.</summary>
    public int AmmunitionPerShot { get; set; } = 1;

    /// <summary>
    /// Вместимость магазина. Значение <see langword="null"/> означает, что оружие
    /// расходует боеприпасы напрямую из запаса и не требует перезарядки.
    /// </summary>
    public int? MagazineSize { get; set; }

    /// <summary>Время перезарядки, выраженное правилами игровой системы.</summary>
    public string? ReloadTime { get; set; }

    /// <summary>
    /// Свойства оружия — названия, разделённые запятыми или переводами строк:
    /// «острое, тяжёлое, пробивающее». Каждое свойство становится признаком объекта
    /// правил, поэтому условие правила может опираться на него.
    /// </summary>
    public string? Properties { get; set; }
}

/// <summary>
/// Слот экипировки. Пользователь может создавать собственные слоты.
///
/// Приложение не знает ни одного слота заранее: голова, руки, оба кольца, слот
/// импланта или ячейка модуля костюма создаются одинаково.
/// </summary>
public class EquipmentSlot : ContentEntity
{
    /// <summary>В слот можно поместить несколько предметов одновременно.</summary>
    public bool AllowMultiple { get; set; }

    /// <summary>Максимальное количество предметов в слоте.</summary>
    public int MaximumItems { get; set; } = 1;

    /// <summary>Порядок отображения слота.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Запись инвентаря персонажа.
/// </summary>
public class InventoryItem : EntityBase
{
    /// <summary>Идентификатор персонажа.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Персонаж.</summary>
    public Character? Character { get; set; }

    /// <summary>Идентификатор предмета.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Предмет.</summary>
    public Item? Item { get; set; }

    /// <summary>Количество предметов.</summary>
    public int Count { get; set; } = 1;

    /// <summary>Оставшиеся заряды предмета.</summary>
    public int? RemainingCharges { get; set; }

    /// <summary>
    /// Количество боеприпасов в магазине оружия.
    /// Заполняется только для записей, содержащих оружие с магазином.
    /// </summary>
    public int? LoadedAmmunition { get; set; }

    /// <summary>Прочность предмета.</summary>
    public double? Durability { get; set; }

    /// <summary>
    /// Идентификатор записи инвентаря, выступающей контейнером.
    /// Позволяет складывать предметы в сумки и другие вместилища.
    /// </summary>
    public Guid? ContainerId { get; set; }

    /// <summary>Запись инвентаря, выступающая контейнером.</summary>
    public InventoryItem? Container { get; set; }

    /// <summary>Пользовательская пометка предмета.</summary>
    public string? Note { get; set; }
}

/// <summary>
/// Предмет, экипированный персонажем в конкретный слот.
/// </summary>
public class CharacterEquipment : EntityBase
{
    /// <summary>Идентификатор персонажа.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Персонаж.</summary>
    public Character? Character { get; set; }

    /// <summary>Идентификатор слота экипировки.</summary>
    public Guid SlotId { get; set; }

    /// <summary>Слот экипировки.</summary>
    public EquipmentSlot? Slot { get; set; }

    /// <summary>Идентификатор записи инвентаря с экипированным предметом.</summary>
    public Guid InventoryItemId { get; set; }

    /// <summary>Запись инвентаря с экипированным предметом.</summary>
    public InventoryItem? InventoryItem { get; set; }
}
