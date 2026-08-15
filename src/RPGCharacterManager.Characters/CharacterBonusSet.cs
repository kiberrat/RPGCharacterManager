using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.Rules;

namespace RPGCharacterManager.Characters;

/// <summary>
/// Вычисленные бонусы надетых предметов.
///
/// Все формулы бонусов вычисляются один раз — по параметрам персонажа без учёта
/// надетых предметов. Благодаря этому результат не зависит от порядка надевания,
/// а бонус к характеристике не может сослаться сам на себя.
/// </summary>
internal sealed class CharacterBonusSet
{
    private readonly Dictionary<Guid, double> _attributes = [];
    private readonly Dictionary<Guid, double> _resources = [];
    private readonly Dictionary<string, double> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _tags = [];
    private readonly List<AppliedBonus> _applied = [];

    private CharacterBonusSet()
    {
    }

    /// <summary>Набор без единого бонуса.</summary>
    public static CharacterBonusSet Empty { get; } = new();

    /// <summary>Применённые бонусы вместе с вычисленными величинами.</summary>
    public IReadOnlyList<AppliedBonus> Applied => _applied;

    /// <summary>Признаки, выданные бонусами.</summary>
    public IReadOnlyList<string> Tags => _tags;

    /// <summary>Именованные величины, заданные бонусами.</summary>
    public IReadOnlyDictionary<string, double> Variables => _variables;

    /// <summary>
    /// Вычисляет бонусы персонажа.
    /// </summary>
    /// <param name="input">Исходные данные расчёта.</param>
    /// <param name="context">Значения переменных персонажа без учёта бонусов.</param>
    /// <param name="formulas">Единый движок вычислений.</param>
    /// <param name="issues">Список замечаний.</param>
    /// <returns>Набор вычисленных бонусов.</returns>
    public static CharacterBonusSet Create(
        CharacterCalculationInput input,
        IFormulaContext context,
        IFormulaEngine formulas,
        List<CharacterIssue> issues)
    {
        if (input.Bonuses.Count == 0)
        {
            return Empty;
        }

        var set = new CharacterBonusSet();
        var attributeNames = input.Attributes.ToDictionary(item => item.Id, item => item.Name);
        var resourceNames = input.Resources.ToDictionary(item => item.Id, item => item.Name);

        foreach (var bonus in input.Bonuses)
        {
            set.Add(bonus, context, formulas, issues, attributeNames, resourceNames);
        }

        return set;
    }

    /// <summary>
    /// Возвращает суммарный бонус к характеристике.
    /// </summary>
    /// <param name="attributeId">Идентификатор характеристики.</param>
    /// <returns>Величина бонуса.</returns>
    public double ForAttribute(Guid attributeId) =>
        _attributes.TryGetValue(attributeId, out var value) ? value : 0;

    /// <summary>
    /// Возвращает суммарный бонус к максимуму ресурса.
    /// </summary>
    /// <param name="resourceId">Идентификатор ресурса.</param>
    /// <returns>Величина бонуса.</returns>
    public double ForResource(Guid resourceId) =>
        _resources.TryGetValue(resourceId, out var value) ? value : 0;

    /// <summary>
    /// Записывает величины и признаки бонусов в объект правил, чтобы формулы,
    /// требования и правила могли к ним обращаться.
    /// </summary>
    /// <param name="target">Объект расчёта.</param>
    public void ApplyTo(RuleTarget target)
    {
        foreach (var pair in _variables)
        {
            target.WithVariable(pair.Key, pair.Value);
        }

        foreach (var tag in _tags)
        {
            target.AddTag(tag);
        }
    }

    private void Add(
        CharacterBonus bonus,
        IFormulaContext context,
        IFormulaEngine formulas,
        List<CharacterIssue> issues,
        IReadOnlyDictionary<Guid, string> attributeNames,
        IReadOnlyDictionary<Guid, string> resourceNames)
    {
        if (!IsActive(bonus, context, formulas, issues))
        {
            _applied.Add(new AppliedBonus(
                bonus.Id,
                bonus.SourceId,
                bonus.Source,
                Describe(bonus, attributeNames, resourceNames),
                0,
                false));

            return;
        }

        if (bonus.Target == BonusTargetKind.Tag)
        {
            if (!string.IsNullOrWhiteSpace(bonus.Name))
            {
                _tags.Add(bonus.Name);
                _applied.Add(new AppliedBonus(bonus.Id, bonus.SourceId, bonus.Source, $"признак «{bonus.Name}»", 0, true));
            }

            return;
        }

        if (!TryEvaluate(bonus, context, formulas, issues, out var value))
        {
            return;
        }

        switch (bonus.Target)
        {
            case BonusTargetKind.Attribute when bonus.TargetId is { } attributeId:
                Accumulate(_attributes, attributeId, value);
                break;

            case BonusTargetKind.Resource when bonus.TargetId is { } resourceId:
                Accumulate(_resources, resourceId, value);
                break;

            case BonusTargetKind.Variable when !string.IsNullOrWhiteSpace(bonus.Name):
                Accumulate(_variables, bonus.Name, value);
                break;

            default:
                issues.Add(new CharacterIssue(
                    CharacterIssueSeverity.Warning,
                    CharacterStepIds.Summary,
                    $"Бонус предмета «{bonus.Source}» не указывает, что именно он изменяет."));

                return;
        }

        _applied.Add(new AppliedBonus(
            bonus.Id,
            bonus.SourceId,
            bonus.Source,
            Describe(bonus, attributeNames, resourceNames),
            value,
            true));
    }

    private static bool IsActive(
        CharacterBonus bonus,
        IFormulaContext context,
        IFormulaEngine formulas,
        List<CharacterIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(bonus.Condition))
        {
            return true;
        }

        var result = formulas.Evaluate(bonus.Condition, context);

        if (result.IsSuccess)
        {
            return result.Value.AsBoolean();
        }

        issues.Add(new CharacterIssue(
            CharacterIssueSeverity.Warning,
            CharacterStepIds.Summary,
            $"Условие бонуса предмета «{bonus.Source}»: {result.Error}"));

        return false;
    }

    private static bool TryEvaluate(
        CharacterBonus bonus,
        IFormulaContext context,
        IFormulaEngine formulas,
        List<CharacterIssue> issues,
        out double value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(bonus.Formula))
        {
            return true;
        }

        var result = formulas.Evaluate(bonus.Formula, context);

        if (result.IsSuccess)
        {
            // Множитель применяется к вычисленной величине, а не к формуле:
            // три наложения «+2 к Силе» дают +6, и формула остаётся прежней.
            value = result.Value.AsNumber() * bonus.Scale;
            return true;
        }

        issues.Add(new CharacterIssue(
            CharacterIssueSeverity.Warning,
            CharacterStepIds.Summary,
            $"Бонус «{bonus.Source}»: {result.Error}"));

        return false;
    }

    private static void Accumulate<TKey>(Dictionary<TKey, double> target, TKey key, double value)
        where TKey : notnull =>
        target[key] = target.TryGetValue(key, out var current) ? current + value : value;

    private static string Describe(
        CharacterBonus bonus,
        IReadOnlyDictionary<Guid, string> attributeNames,
        IReadOnlyDictionary<Guid, string> resourceNames) => bonus.Target switch
    {
        BonusTargetKind.Attribute => bonus.TargetId is { } id && attributeNames.TryGetValue(id, out var attribute)
            ? attribute
            : "характеристика",
        BonusTargetKind.Resource => bonus.TargetId is { } id && resourceNames.TryGetValue(id, out var resource)
            ? $"максимум ресурса «{resource}»"
            : "максимум ресурса",
        BonusTargetKind.Tag => $"признак «{bonus.Name}»",
        _ => bonus.Name ?? "величина",
    };
}
