using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Core.Abstractions.Characters;

/// <summary>
/// Имена переменных, доступных формулам и требованиям персонажа.
///
/// Перечень собран в одном месте, чтобы подсказки редактора, проверка требований
/// и вычисления использовали одинаковые имена.
/// </summary>
public static class CharacterVariables
{
    /// <summary>Текущий уровень персонажа.</summary>
    public const string Level = "уровень";

    /// <summary>Внутреннее имя выбранной расы.</summary>
    public const string Race = "раса";

    /// <summary>Внутреннее имя выбранного класса.</summary>
    public const string Class = "класс";

    /// <summary>Внутреннее имя выбранного подкласса.</summary>
    public const string Subclass = "подкласс";

    /// <summary>Внутреннее имя выбранного происхождения.</summary>
    public const string Background = "происхождение";

    /// <summary>Значение характеристики внутри её собственной формулы модификатора.</summary>
    public const string Value = "значение";

    /// <summary>Уровень владения навыком внутри его формулы.</summary>
    public const string Proficiency = "владение";

    /// <summary>Модификатор связанной характеристики внутри формулы навыка.</summary>
    public const string LinkedModifier = "характеристика";

    /// <summary>Значение связанного навыка внутри формул оружия.</summary>
    public const string SkillValue = "навык";

    /// <summary>Выпавшее значение кости внутри формул и правил броска.</summary>
    public const string Roll = "бросок";

    /// <summary>Итог броска попадания внутри правил боя.</summary>
    public const string Attack = "попадание";

    /// <summary>
    /// Нанесённый урон: внутри формулы критического урона — обычный урон оружия,
    /// внутри правил боя — урон, который правило может изменить.
    /// </summary>
    public const string Damage = "урон";
}

/// <summary>
/// Вычисленное значение характеристики персонажа.
/// </summary>
/// <param name="Id">Идентификатор характеристики.</param>
/// <param name="Name">Название характеристики.</param>
/// <param name="SystemName">Внутреннее имя характеристики.</param>
/// <param name="BaseValue">Базовое значение, заданное пользователем.</param>
/// <param name="Value">Итоговое значение с учётом формул и правил.</param>
/// <param name="Modifier">Модификатор характеристики.</param>
/// <param name="IsDerived">Значение вычисляется формулой и не редактируется вручную.</param>
public sealed record CalculatedAttributeValue(
    Guid Id,
    string Name,
    string SystemName,
    double BaseValue,
    double Value,
    double Modifier,
    bool IsDerived);

/// <summary>
/// Вычисленное значение навыка персонажа.
/// </summary>
/// <param name="Id">Идентификатор навыка.</param>
/// <param name="Name">Название навыка.</param>
/// <param name="Value">Итоговое значение навыка.</param>
/// <param name="ProficiencyLevel">Уровень владения навыком.</param>
public sealed record CalculatedSkill(Guid Id, string Name, double Value, int ProficiencyLevel);

/// <summary>
/// Вычисленное состояние ресурса персонажа.
/// </summary>
/// <param name="Id">Идентификатор ресурса.</param>
/// <param name="Name">Название ресурса.</param>
/// <param name="Current">Начальное или текущее значение.</param>
/// <param name="Maximum">Максимальное значение.</param>
public sealed record CalculatedResource(Guid Id, string Name, double Current, double Maximum);

/// <summary>
/// Бонус, действующий на персонажа: усиление от надетого предмета,
/// а в дальнейшем — от эффекта, черты или способности.
/// </summary>
/// <param name="Id">Идентификатор описания бонуса.</param>
/// <param name="SourceId">
/// Идентификатор источника: у экипировки — запись инвентаря. Два одинаковых предмета
/// дают одинаково описанные, но разные бонусы, и различаются именно этим значением.
/// </param>
/// <param name="Source">Название источника бонуса для отображения.</param>
/// <param name="Target">Что изменяет бонус.</param>
/// <param name="TargetId">Идентификатор характеристики или ресурса.</param>
/// <param name="Name">Имя величины или признака.</param>
/// <param name="Formula">Формула величины бонуса.</param>
/// <param name="Condition">Условие, при котором бонус действует.</param>
/// <param name="Scale">
/// Множитель вычисленной величины. Единица — обычный бонус; складывающийся эффект
/// передаёт здесь количество наложений, поэтому «+2 к Силе» в трёх наложениях
/// даёт +6 без размножения записей бонуса.
/// </param>
public sealed record CharacterBonus(
    Guid Id,
    Guid SourceId,
    string Source,
    BonusTargetKind Target,
    Guid? TargetId,
    string? Name,
    string? Formula,
    string? Condition,
    double Scale = 1);

/// <summary>
/// Применённый бонус вместе с вычисленной величиной.
/// </summary>
/// <param name="Id">Идентификатор описания бонуса.</param>
/// <param name="SourceId">Идентификатор источника бонуса.</param>
/// <param name="Source">Название источника бонуса.</param>
/// <param name="Description">Что именно изменил бонус.</param>
/// <param name="Value">Вычисленная величина.</param>
/// <param name="IsApplied">Условие бонуса выполнено и он подействовал.</param>
public sealed record AppliedBonus(
    Guid Id,
    Guid SourceId,
    string Source,
    string Description,
    double Value,
    bool IsApplied);

/// <summary>
/// Набор правил, применяемых к персонажу в рамках одного события.
/// </summary>
/// <param name="Trigger">Ключ события.</param>
/// <param name="Rules">Правила события в порядке возрастания приоритета.</param>
public sealed record RuleApplication(string Trigger, IReadOnlyList<RuleDefinition> Rules);

/// <summary>
/// Исходные данные для расчёта параметров персонажа.
/// </summary>
public sealed record CharacterCalculationInput
{
    /// <summary>Характеристики игровой системы.</summary>
    public IReadOnlyList<AttributeDefinition> Attributes { get; init; } = [];

    /// <summary>Навыки, которыми владеет персонаж.</summary>
    public IReadOnlyList<Skill> Skills { get; init; } = [];

    /// <summary>Ресурсы игровой системы.</summary>
    public IReadOnlyList<GameResource> Resources { get; init; } = [];

    /// <summary>Название персонажа для отчёта о применении правил.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Уровень персонажа.</summary>
    public int Level { get; init; } = 1;

    /// <summary>Базовые значения характеристик, сопоставленные идентификатору характеристики.</summary>
    public IReadOnlyDictionary<Guid, double> BaseValues { get; init; } =
        new Dictionary<Guid, double>();

    /// <summary>
    /// Пользовательские значения вычисляемых характеристик, заменяющие результат формулы.
    /// </summary>
    public IReadOnlyDictionary<Guid, double> AttributeOverrides { get; init; } =
        new Dictionary<Guid, double>();

    /// <summary>Уровни владения навыками, сопоставленные идентификатору навыка.</summary>
    public IReadOnlyDictionary<Guid, int> SkillProficiencies { get; init; } =
        new Dictionary<Guid, int>();

    /// <summary>Текстовые переменные: выбранные раса, класс, подкласс, происхождение.</summary>
    public IReadOnlyDictionary<string, string> TextVariables { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Признаки персонажа: внутренние имена выбранных объектов и полученных черт.</summary>
    public IReadOnlyCollection<string> Tags { get; init; } = [];

    /// <summary>Наборы правил, применяемые после вычисления формул, в порядке применения.</summary>
    public IReadOnlyList<RuleApplication> RuleSets { get; init; } = [];

    /// <summary>Бонусы надетых предметов, применяемые к параметрам персонажа.</summary>
    public IReadOnlyList<CharacterBonus> Bonuses { get; init; } = [];
}

/// <summary>
/// Результат расчёта параметров персонажа.
/// </summary>
/// <param name="Attributes">Вычисленные характеристики.</param>
/// <param name="Skills">Вычисленные навыки.</param>
/// <param name="Resources">Вычисленные ресурсы.</param>
/// <param name="Issues">Замечания, найденные при вычислении.</param>
/// <param name="AppliedRules">Названия применённых правил.</param>
/// <param name="Bonuses">Бонусы надетых предметов вместе с вычисленными величинами.</param>
public sealed record CharacterCalculation(
    IReadOnlyList<CalculatedAttributeValue> Attributes,
    IReadOnlyList<CalculatedSkill> Skills,
    IReadOnlyList<CalculatedResource> Resources,
    IReadOnlyList<CharacterIssue> Issues,
    IReadOnlyList<string> AppliedRules,
    IReadOnlyList<AppliedBonus> Bonuses);

/// <summary>
/// Результат применения правил события к базовым значениям персонажа.
/// </summary>
/// <param name="BaseValues">Новые базовые значения по идентификатору характеристики.</param>
/// <param name="AppliedRules">Названия применённых правил.</param>
/// <param name="Issues">Замечания, найденные при применении.</param>
public sealed record CharacterEventResult(
    IReadOnlyDictionary<Guid, double> BaseValues,
    IReadOnlyList<string> AppliedRules,
    IReadOnlyList<CharacterIssue> Issues);

/// <summary>
/// Автоматический расчёт параметров персонажа.
///
/// STYLE_GUIDE запрещает выполнять игровые вычисления вне единого движка формул,
/// поэтому расчёт не содержит ни одной вшитой формулы: все значения берутся из
/// характеристик, навыков, ресурсов и правил, созданных пользователем.
/// </summary>
public interface ICharacterCalculator
{
    /// <summary>
    /// Пересчитывает характеристики, модификаторы, навыки и ресурсы персонажа.
    /// </summary>
    /// <param name="input">Исходные данные расчёта.</param>
    /// <returns>Результат расчёта вместе с найденными замечаниями.</returns>
    CharacterCalculation Calculate(CharacterCalculationInput input);

    /// <summary>
    /// Применяет правила разового события — создания персонажа или повышения
    /// уровня — к базовым значениям характеристик.
    ///
    /// Такие правила изменяют персонажа навсегда, поэтому их результат становится
    /// новым базовым значением: последующие пересчёты его сохраняют и не применяют
    /// правило повторно.
    /// </summary>
    /// <param name="input">Исходные данные расчёта. Наборы правил из них не используются.</param>
    /// <param name="application">Применяемый набор правил.</param>
    /// <returns>Новые базовые значения и сведения о применении.</returns>
    CharacterEventResult ApplyToBaseValues(CharacterCalculationInput input, RuleApplication application);
}
