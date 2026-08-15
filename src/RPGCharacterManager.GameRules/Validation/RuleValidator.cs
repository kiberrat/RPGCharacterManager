using System.Globalization;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.GameRules.Actions;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.GameRules.Validation;

/// <summary>
/// Проверка правил на ошибки и взаимные конфликты.
///
/// Документ 019_Редактор_правил.md требует находить противоречащие правила,
/// неправильные формулы и невозможные условия.
/// </summary>
public sealed class RuleValidator : IRuleValidator
{
    private readonly IFormulaEngine _formulas;
    private readonly IRuleEngine _ruleEngine;
    private readonly IRuleTriggerCatalog _triggers;

    /// <summary>
    /// Создаёт службу проверки правил.
    /// </summary>
    /// <param name="formulas">Единый движок вычислений.</param>
    /// <param name="ruleEngine">Движок правил, предоставляющий обработчики действий.</param>
    /// <param name="triggers">Перечень известных событий.</param>
    public RuleValidator(IFormulaEngine formulas, IRuleEngine ruleEngine, IRuleTriggerCatalog triggers)
    {
        _formulas = Guard.NotNull(formulas);
        _ruleEngine = Guard.NotNull(ruleEngine);
        _triggers = Guard.NotNull(triggers);
    }

    /// <inheritdoc />
    public IReadOnlyList<RuleIssue> Validate(RuleDefinition rule)
    {
        Guard.NotNull(rule);

        var issues = new List<RuleIssue>();

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            issues.Add(new RuleIssue(RuleIssueSeverity.Error, rule.Name, "Не задано название правила."));
        }

        ValidateTrigger(rule, issues);
        ValidateCondition(rule, rule.Condition, issues);
        ValidateActions(rule, issues);

        if (rule.Actions.Count == 0)
        {
            issues.Add(new RuleIssue(
                RuleIssueSeverity.Warning,
                rule.Name,
                "Правило не выполняет ни одного действия."));
        }

        return issues;
    }

    /// <inheritdoc />
    public IReadOnlyList<RuleIssue> ValidateSet(IReadOnlyList<RuleDefinition> rules)
    {
        Guard.NotNull(rules);

        var issues = new List<RuleIssue>();

        foreach (var rule in rules)
        {
            issues.AddRange(Validate(rule));
        }

        issues.AddRange(FindDuplicateNames(rules));
        issues.AddRange(FindPriorityConflicts(rules));

        return issues;
    }

    private void ValidateTrigger(RuleDefinition rule, List<RuleIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(rule.Trigger))
        {
            issues.Add(new RuleIssue(RuleIssueSeverity.Error, rule.Name, "Не выбрано событие правила."));
            return;
        }

        if (_triggers.Find(rule.Trigger) is null)
        {
            issues.Add(new RuleIssue(
                RuleIssueSeverity.Warning,
                rule.Name,
                $"Событие «{rule.Trigger}» неизвестно приложению — правило не будет вызвано."));
        }
    }

    private void ValidateCondition(RuleDefinition rule, RuleCondition? condition, List<RuleIssue> issues)
    {
        switch (condition)
        {
            case null:
                return;

            case RuleConditionGroup group:
                foreach (var child in group.Children)
                {
                    ValidateCondition(rule, child, issues);
                }

                CheckContradictions(rule, group, issues);
                return;

            case RuleComparison comparison:
                ValidateComparison(rule, comparison, issues);
                return;

            default:
                return;
        }
    }

    private void ValidateComparison(RuleDefinition rule, RuleComparison comparison, List<RuleIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(comparison.Left))
        {
            issues.Add(new RuleIssue(RuleIssueSeverity.Error, rule.Name, "В условии не заполнена левая часть."));
            return;
        }

        // Операторы наличия сравнивают признак по названию, а не вычисляют выражение.
        if (comparison.Operator is RuleComparisonOperator.Has or RuleComparisonOperator.HasNot)
        {
            if (string.IsNullOrWhiteSpace(comparison.Right))
            {
                issues.Add(new RuleIssue(
                    RuleIssueSeverity.Error,
                    rule.Name,
                    "Не указано название признака в условии наличия."));
            }

            return;
        }

        AddFormulaIssue(rule, comparison.Left, "левой части условия", issues);
        AddFormulaIssue(rule, comparison.Right, "правой части условия", issues);
    }

    private void ValidateActions(RuleDefinition rule, List<RuleIssue> issues)
    {
        var handlers = _ruleEngine.ActionHandlers
            .ToDictionary(handler => handler.Kind, StringComparer.OrdinalIgnoreCase);

        foreach (var action in rule.Actions)
        {
            if (!handlers.TryGetValue(action.Kind, out var handler))
            {
                issues.Add(new RuleIssue(
                    RuleIssueSeverity.Error,
                    rule.Name,
                    $"Неизвестное действие «{action.Kind}»."));
                continue;
            }

            foreach (var parameter in handler.Parameters)
            {
                var value = action.GetParameter(parameter.Name);

                if (parameter.IsRequired && string.IsNullOrWhiteSpace(value))
                {
                    issues.Add(new RuleIssue(
                        RuleIssueSeverity.Error,
                        rule.Name,
                        $"Действие «{handler.DisplayName}»: не заполнен параметр «{parameter.DisplayName}»."));
                    continue;
                }

                if (parameter.Kind == RuleParameterKind.Expression && !string.IsNullOrWhiteSpace(value))
                {
                    AddFormulaIssue(rule, value, $"параметре «{parameter.DisplayName}»", issues);
                }
            }
        }
    }

    private void AddFormulaIssue(RuleDefinition rule, string expression, string location, List<RuleIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            issues.Add(new RuleIssue(
                RuleIssueSeverity.Error,
                rule.Name,
                $"Пустое выражение в {location}."));
            return;
        }

        var result = _formulas.Validate(expression);

        if (result.IsFailure)
        {
            issues.Add(new RuleIssue(
                RuleIssueSeverity.Error,
                rule.Name,
                $"Ошибка в {location}: {result.Error}"));
        }
    }

    /// <summary>
    /// Ищет заведомо невыполнимые условия внутри группы «И»: числовые сравнения
    /// одного параметра с постоянными значениями, которые не могут выполниться вместе,
    /// например <c>Уровень &gt; 10</c> и <c>Уровень &lt; 5</c>.
    /// </summary>
    /// <param name="rule">Проверяемое правило.</param>
    /// <param name="group">Проверяемая группа условий.</param>
    /// <param name="issues">Список, в который добавляются замечания.</param>
    private static void CheckContradictions(
        RuleDefinition rule,
        RuleConditionGroup group,
        List<RuleIssue> issues)
    {
        if (group.Operator != RuleLogicalOperator.And || group.IsNegated)
        {
            return;
        }

        var bounds = new Dictionary<string, (double? Minimum, double? Maximum)>(StringComparer.OrdinalIgnoreCase);

        foreach (var comparison in group.Children.OfType<RuleComparison>())
        {
            if (!TryReadConstant(comparison.Right, out var limit))
            {
                continue;
            }

            var name = comparison.Left.Trim();
            bounds.TryGetValue(name, out var current);

            switch (comparison.Operator)
            {
                case RuleComparisonOperator.Greater:
                case RuleComparisonOperator.GreaterOrEqual:
                    current.Minimum = Math.Max(current.Minimum ?? double.MinValue, limit);
                    break;

                case RuleComparisonOperator.Less:
                case RuleComparisonOperator.LessOrEqual:
                    current.Maximum = Math.Min(current.Maximum ?? double.MaxValue, limit);
                    break;

                default:
                    continue;
            }

            bounds[name] = current;
        }

        foreach (var pair in bounds)
        {
            if (pair.Value is { Minimum: { } minimum, Maximum: { } maximum } && minimum > maximum)
            {
                issues.Add(new RuleIssue(
                    RuleIssueSeverity.Warning,
                    rule.Name,
                    $"Условие невыполнимо: «{pair.Key}» одновременно больше {FormatNumber(minimum)} " +
                    $"и меньше {FormatNumber(maximum)}."));
            }
        }
    }

    /// <summary>
    /// Ищет правила, изменяющие один и тот же параметр при одном событии
    /// с одинаковым приоритетом: порядок их применения не определён.
    /// </summary>
    /// <param name="rules">Проверяемый набор правил.</param>
    /// <returns>Найденные замечания.</returns>
    private static IEnumerable<RuleIssue> FindPriorityConflicts(IReadOnlyList<RuleDefinition> rules)
    {
        var writes = rules
            .Where(rule => rule.Enabled)
            .SelectMany(rule => rule.Actions
                .Where(action => IsWritingAction(action.Kind))
                .Select(action => new
                {
                    Rule = rule,
                    Parameter = action.GetParameter(RuleActionParameterNames.Target) is { Length: > 0 } target
                        ? target
                        : action.GetParameter(RuleActionParameterNames.Resource),
                }))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Parameter));

        var groups = writes
            .GroupBy(
                entry => (entry.Rule.Trigger, entry.Parameter, entry.Rule.Priority),
                StringTupleComparer.Instance)
            .Where(group => group.Select(entry => entry.Rule.Id).Distinct().Count() > 1);

        foreach (var group in groups)
        {
            var names = group.Select(entry => entry.Rule.Name).Distinct(StringComparer.CurrentCulture);

            yield return new RuleIssue(
                RuleIssueSeverity.Warning,
                string.Join(", ", names),
                $"Параметр «{group.Key.Parameter}» изменяется несколькими правилами события " +
                $"«{group.Key.Trigger}» с одинаковым приоритетом {group.Key.Priority}. " +
                "Задайте разные приоритеты, чтобы порядок применения был определён.");
        }
    }

    private static IEnumerable<RuleIssue> FindDuplicateNames(IReadOnlyList<RuleDefinition> rules) =>
        rules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Name))
            .GroupBy(rule => rule.Name, StringComparer.CurrentCultureIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new RuleIssue(
                RuleIssueSeverity.Warning,
                group.Key,
                $"Название «{group.Key}» используется несколькими правилами ({group.Count()})."));

    private static bool IsWritingAction(string kind) => kind is
        "установить_значение" or "изменить_значение" or "расход_ресурса" or "восстановить_ресурс";

    private static bool TryReadConstant(string value, out double number) =>
        double.TryParse(
            value?.Trim().Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out number);

    private static string FormatNumber(double value) =>
        value.ToString("0.####", CultureInfo.CurrentCulture);

    /// <summary>
    /// Сравнение ключей группировки с учётом правил сравнения строк.
    /// </summary>
    private sealed class StringTupleComparer : IEqualityComparer<(string Trigger, string Parameter, int Priority)>
    {
        public static StringTupleComparer Instance { get; } = new();

        public bool Equals(
            (string Trigger, string Parameter, int Priority) x,
            (string Trigger, string Parameter, int Priority) y) =>
            string.Equals(x.Trigger, y.Trigger, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Parameter, y.Parameter, StringComparison.OrdinalIgnoreCase)
            && x.Priority == y.Priority;

        public int GetHashCode((string Trigger, string Parameter, int Priority) obj) => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Trigger ?? string.Empty),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Parameter ?? string.Empty),
            obj.Priority);
    }
}
