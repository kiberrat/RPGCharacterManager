namespace RPGCharacterManager.Core.Models.Entities;

/// <summary>
/// Тип данных игрового значения.
/// Используется характеристиками и пользовательскими свойствами.
/// </summary>
public enum GameValueType
{
    /// <summary>Целое число.</summary>
    WholeNumber = 0,

    /// <summary>Дробное число.</summary>
    FractionalNumber = 1,

    /// <summary>Процент.</summary>
    Percent = 2,

    /// <summary>Логическое значение.</summary>
    Boolean = 3,

    /// <summary>Строка.</summary>
    Text = 4,

    /// <summary>Многострочный текст.</summary>
    LongText = 5,

    /// <summary>Разметка Markdown.</summary>
    Markdown = 6,

    /// <summary>Формула, вычисляемая движком.</summary>
    Formula = 7,

    /// <summary>Формула броска кубиков.</summary>
    DiceFormula = 8,

    /// <summary>Дата.</summary>
    Date = 9,

    /// <summary>Время.</summary>
    Time = 10,

    /// <summary>Цвет.</summary>
    Color = 11,

    /// <summary>Изображение.</summary>
    Image = 12,

    /// <summary>Документ JSON.</summary>
    Json = 13,

    /// <summary>Список значений.</summary>
    List = 14,

    /// <summary>Выбор одного варианта из перечисления.</summary>
    Enumeration = 15,

    /// <summary>Выбор нескольких вариантов.</summary>
    MultipleChoice = 16,

    /// <summary>Ссылка на другой игровой объект.</summary>
    ObjectReference = 17,
}

/// <summary>
/// Характеристика игровой системы.
///
/// Характеристикой является любой числовой, логический или вычисляемый параметр:
/// Сила, Ловкость, Удача, Репутация, Радиация и любой пользовательский параметр.
/// Система характеристик не привязана к какой-либо конкретной игре.
/// </summary>
public class AttributeDefinition : ContentEntity
{
    /// <summary>Категория характеристики, задаваемая пользователем.</summary>
    public string? Category { get; set; }

    /// <summary>Тип значения характеристики.</summary>
    public GameValueType ValueType { get; set; } = GameValueType.WholeNumber;

    /// <summary>Значение по умолчанию для нового персонажа.</summary>
    public double DefaultValue { get; set; }

    /// <summary>Минимально допустимое значение.</summary>
    public double? MinimumValue { get; set; }

    /// <summary>Максимально допустимое значение.</summary>
    public double? MaximumValue { get; set; }

    /// <summary>
    /// Формула вычисления значения. Если задана, характеристика является производной
    /// и не редактируется вручную.
    /// </summary>
    public string? Formula { get; set; }

    /// <summary>
    /// Формула вычисления модификатора характеристики.
    /// Позволяет каждой игровой системе задавать собственное правило, например
    /// <c>округлитьВниз((значение - 10) / 2)</c>.
    /// </summary>
    public string? ModifierFormula { get; set; }

    /// <summary>Характеристика используется только формулами и не отображается пользователю.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Цвет карточки характеристики в интерфейсе.</summary>
    public string? Color { get; set; }

    /// <summary>Порядок отображения среди характеристик.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Навык игровой системы.
/// </summary>
public class Skill : ContentEntity
{
    /// <summary>Категория навыка.</summary>
    public string? Category { get; set; }

    /// <summary>Идентификатор связанной характеристики.</summary>
    public Guid? LinkedAttributeId { get; set; }

    /// <summary>Связанная характеристика.</summary>
    public AttributeDefinition? LinkedAttribute { get; set; }

    /// <summary>
    /// Формула вычисления итогового значения навыка.
    /// Если задана, имеет приоритет над связанной характеристикой.
    /// </summary>
    public string? Formula { get; set; }

    /// <summary>Требования к получению навыка в виде выражения.</summary>
    public string? Requirements { get; set; }

    /// <summary>Максимальный уровень владения навыком.</summary>
    public int? MaximumLevel { get; set; }

    /// <summary>Цвет карточки навыка в интерфейсе.</summary>
    public string? Color { get; set; }

    /// <summary>Порядок отображения среди навыков.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Значение характеристики конкретного персонажа.
/// </summary>
public class CharacterAttributeValue : EntityBase
{
    /// <summary>Идентификатор персонажа.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Персонаж.</summary>
    public Character? Character { get; set; }

    /// <summary>Идентификатор характеристики.</summary>
    public Guid AttributeId { get; set; }

    /// <summary>Характеристика.</summary>
    public AttributeDefinition? Attribute { get; set; }

    /// <summary>Базовое значение, заданное пользователем.</summary>
    public double BaseValue { get; set; }

    /// <summary>Итоговое значение с учётом всех бонусов. Вычисляется движком.</summary>
    public double CurrentValue { get; set; }

    /// <summary>Модификатор характеристики. Вычисляется движком.</summary>
    public double Modifier { get; set; }

    /// <summary>Сумма временных бонусов, действующих в данный момент.</summary>
    public double TemporaryBonus { get; set; }

    /// <summary>
    /// Пользовательское значение вычисляемой характеристики.
    /// <see langword="null"/> означает, что значение определяется формулой игровой системы.
    /// </summary>
    public double? OverrideValue { get; set; }
}

/// <summary>
/// Владение навыком конкретного персонажа.
/// </summary>
public class CharacterSkill : EntityBase
{
    /// <summary>Идентификатор персонажа.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Персонаж.</summary>
    public Character? Character { get; set; }

    /// <summary>Идентификатор навыка.</summary>
    public Guid SkillId { get; set; }

    /// <summary>Навык.</summary>
    public Skill? Skill { get; set; }

    /// <summary>
    /// Уровень владения. Значения определяются игровой системой:
    /// 0 — нет владения, 1 — владение, 2 — экспертность и так далее.
    /// </summary>
    public int ProficiencyLevel { get; set; }

    /// <summary>Дополнительный бонус к навыку, заданный пользователем.</summary>
    public double Bonus { get; set; }

    /// <summary>Итоговое значение навыка. Вычисляется движком.</summary>
    public double CurrentValue { get; set; }
}
