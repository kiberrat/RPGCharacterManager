using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.GameRules;

/// <summary>
/// Движок выполнения игровых правил.
///
/// Движок не содержит ни одного правила конкретной игровой системы: он лишь проверяет
/// условия и передаёт выполнение зарегистрированным обработчикам действий. Любая
/// механика описывается данными и создаётся пользователем в редакторе правил.
/// </summary>
public sealed class RuleEngine : IRuleEngine, IRuleActionServices
{
    /// <summary>
    /// Предельная глубина вложенности дерева условий.
    /// Ограничение защищает от повреждённых данных, способных вызвать переполнение стека.
    /// </summary>
    public const int MaximumConditionDepth = 32;

    private readonly Dictionary<string, IRuleActionHandler> _handlers;

    /// <summary>
    /// Создаёт движок правил.
    /// </summary>
    /// <param name="formulas">Единый движок вычислений.</param>
    /// <param name="handlers">Зарегистрированные обработчики действий.</param>
    public RuleEngine(IFormulaEngine formulas, IEnumerable<IRuleActionHandler> handlers)
    {
        Formulas = Guard.NotNull(formulas);
        Guard.NotNull(handlers);

        // Последняя регистрация замещает предыдущую: игровая система или плагин
        // может переопределить поведение встроенного действия.
        var map = new Dictionary<string, IRuleActionHandler>(StringComparer.OrdinalIgnoreCase);

        foreach (var handler in handlers)
        {
            map[handler.Kind] = handler;
        }

        _handlers = map;
    }

    /// <inheritdoc />
    public IFormulaEngine Formulas { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<IRuleActionHandler> ActionHandlers => _handlers.Values;

    /// <inheritdoc />
    public bool EvaluateCondition(RuleCondition? condition, IRuleTarget target)
    {
        Guard.NotNull(target);

        // Правило без условий выполняется всегда.
        return condition is null || Evaluate(condition, target, depth: 0);
    }

    /// <inheritdoc />
    public RuleExecutionReport Execute(
        string trigger,
        IRuleTarget target,
        IEnumerable<RuleDefinition> rules)
    {
        Guard.NotNullOrWhiteSpace(trigger);
        Guard.NotNull(target);
        Guard.NotNull(rules);

        var executed = new List<string>();
        var skipped = new List<string>();
        var outcomes = new List<RuleActionOutcome>();

        var applicable = rules
            .Where(rule => rule.Enabled)
            .Where(rule => string.Equals(rule.Trigger, trigger, StringComparison.OrdinalIgnoreCase))
            // Правило с большим приоритетом выполняется позже и переопределяет
            // результат предыдущих, как описано в документе 019.
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Name, StringComparer.CurrentCulture);

        foreach (var rule in applicable)
        {
            if (!EvaluateCondition(rule.Condition, target))
            {
                skipped.Add(rule.Name);
                continue;
            }

            executed.Add(rule.Name);

            foreach (var action in rule.Actions)
            {
                outcomes.Add(ExecuteAction(action, target, rule.Name));
            }
        }

        return new RuleExecutionReport(trigger, executed, skipped, outcomes);
    }

    private RuleActionOutcome ExecuteAction(RuleAction action, IRuleTarget target, string ruleName)
    {
        if (!_handlers.TryGetValue(action.Kind, out var handler))
        {
            return new RuleActionOutcome(
                ruleName,
                action.Kind,
                $"Неизвестное действие «{action.Kind}»",
                Succeeded: false);
        }

        try
        {
            return handler.Execute(action, target, ruleName, this);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Ошибка пользовательского правила не должна нарушать работу приложения:
            // она отражается в отчёте и не прерывает выполнение остальных правил.
            return new RuleActionOutcome(
                ruleName,
                action.Kind,
                $"Ошибка выполнения: {exception.Message}",
                Succeeded: false);
        }
    }

    private bool Evaluate(RuleCondition condition, IRuleTarget target, int depth)
    {
        if (depth > MaximumConditionDepth)
        {
            throw new InvalidOperationException(
                $"Превышена допустимая глубина вложенности условий ({MaximumConditionDepth}).");
        }

        return condition switch
        {
            RuleConditionGroup group => EvaluateGroup(group, target, depth),
            RuleComparison comparison => EvaluateComparison(comparison, target),
            _ => false,
        };
    }

    private bool EvaluateGroup(RuleConditionGroup group, IRuleTarget target, int depth)
    {
        // Пустая группа не ограничивает выполнение правила.
        if (group.Children.Count == 0)
        {
            return !group.IsNegated;
        }

        var result = group.Operator == RuleLogicalOperator.And
            ? group.Children.All(child => Evaluate(child, target, depth + 1))
            : group.Children.Any(child => Evaluate(child, target, depth + 1));

        return group.IsNegated ? !result : result;
    }

    private bool EvaluateComparison(RuleComparison comparison, IRuleTarget target)
    {
        // Операторы наличия работают с признаками объекта, а не с выражениями:
        // правая часть является названием эффекта, черты или владения.
        if (comparison.Operator is RuleComparisonOperator.Has or RuleComparisonOperator.HasNot)
        {
            var hasTag = target.HasTag(comparison.Right);
            return comparison.Operator == RuleComparisonOperator.Has ? hasTag : !hasTag;
        }

        var left = Formulas.Evaluate(comparison.Left, target);
        if (left.IsFailure)
        {
            return false;
        }

        var right = Formulas.Evaluate(comparison.Right, target);
        if (right.IsFailure)
        {
            return false;
        }

        return Compare(left.Value, comparison.Operator, right.Value);
    }

    private static bool Compare(FormulaValue left, RuleComparisonOperator comparison, FormulaValue right) =>
        comparison switch
        {
            RuleComparisonOperator.Equal => left == right,
            RuleComparisonOperator.NotEqual => left != right,
            RuleComparisonOperator.Less => left.AsNumber() < right.AsNumber(),
            RuleComparisonOperator.Greater => left.AsNumber() > right.AsNumber(),
            RuleComparisonOperator.LessOrEqual => left.AsNumber() <= right.AsNumber(),
            RuleComparisonOperator.GreaterOrEqual => left.AsNumber() >= right.AsNumber(),
            RuleComparisonOperator.Contains => left.AsText()
                .Contains(right.AsText(), StringComparison.CurrentCultureIgnoreCase),
            _ => false,
        };
}
