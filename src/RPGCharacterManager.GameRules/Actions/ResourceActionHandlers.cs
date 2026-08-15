using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Rules;

namespace RPGCharacterManager.GameRules.Actions;

/// <summary>
/// Общая логика изменения ресурса.
///
/// Ресурс представлен параметром объекта: приложение не знает, что именно является
/// ресурсом в конкретной игровой системе — здоровье, мана, ярость, патроны или
/// произвольный пользовательский счётчик.
/// </summary>
/// <param name="kind">Ключ вида действия.</param>
/// <param name="displayName">Отображаемое название.</param>
/// <param name="description">Пояснение для редактора.</param>
public abstract class ResourceActionHandlerBase(string kind, string displayName, string description)
    : RuleActionHandlerBase(
        kind,
        displayName,
        description,
        [
            new RuleActionParameter(RuleActionParameterNames.Resource, "Ресурс", RuleParameterKind.VariableName),
            new RuleActionParameter(RuleActionParameterNames.Value, "Количество", RuleParameterKind.Expression),
        ])
{
    /// <summary>
    /// Суффикс имени параметра, хранящего верхнюю границу ресурса.
    /// Используется, чтобы восстановление не превышало максимум, если он задан.
    /// </summary>
    protected const string MaximumSuffix = ".Максимум";

    /// <summary>Знак изменения ресурса: -1 для расхода, +1 для восстановления.</summary>
    protected abstract int Sign { get; }

    /// <summary>Глагол, используемый в описании результата.</summary>
    protected abstract string ActionVerb { get; }

    /// <inheritdoc />
    public override RuleActionOutcome Execute(
        RuleAction action,
        IRuleTarget target,
        string ruleName,
        IRuleActionServices services)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(target);

        var resource = action.GetParameter(RuleActionParameterNames.Resource);

        if (string.IsNullOrWhiteSpace(resource))
        {
            return new RuleActionOutcome(ruleName, Kind, "Не указан ресурс", false);
        }

        if (!TryEvaluate(action, RuleActionParameterNames.Value, target, services, out var amount, out var error))
        {
            return new RuleActionOutcome(ruleName, Kind, error!, false);
        }

        var current = target.TryGetVariable(resource, out var existing) ? existing.AsNumber() : 0;

        // Отрицательное количество не должно превращать расход в восстановление.
        var change = Math.Abs(amount.AsNumber()) * Sign;
        var updated = current + change;

        // Ресурс не может опуститься ниже нуля.
        updated = Math.Max(0, updated);

        if (target.TryGetVariable(resource + MaximumSuffix, out var maximum))
        {
            updated = Math.Min(updated, maximum.AsNumber());
        }

        target.SetVariable(resource, FormulaValue.FromNumber(updated));

        var actualChange = Math.Abs(updated - current);

        return new RuleActionOutcome(
            ruleName,
            Kind,
            $"{ActionVerb} {resource}: {FormatNumber(actualChange)} ({FormatNumber(current)} → {FormatNumber(updated)})",
            Succeeded: true);
    }
}

/// <summary>Расходует ресурс объекта.</summary>
public sealed class SpendResourceActionHandler() : ResourceActionHandlerBase(
    "расход_ресурса",
    "Израсходовать ресурс",
    "Уменьшает ресурс на указанную величину, не опускаясь ниже нуля.")
{
    /// <inheritdoc />
    protected override int Sign => -1;

    /// <inheritdoc />
    protected override string ActionVerb => "Израсходовано";
}

/// <summary>Восстанавливает ресурс объекта.</summary>
public sealed class RestoreResourceActionHandler() : ResourceActionHandlerBase(
    "восстановить_ресурс",
    "Восстановить ресурс",
    "Увеличивает ресурс на указанную величину, не превышая максимум, если он задан.")
{
    /// <inheritdoc />
    protected override int Sign => 1;

    /// <inheritdoc />
    protected override string ActionVerb => "Восстановлено";
}

/// <summary>Выполняет бросок кубиков и записывает результат.</summary>
public sealed class RollActionHandler() : RuleActionHandlerBase(
    "бросок",
    "Выполнить бросок",
    "Вычисляет формулу броска и, если указан параметр, сохраняет результат в него.",
    [
        new RuleActionParameter(RuleActionParameterNames.Formula, "Формула броска", RuleParameterKind.Expression),
        new RuleActionParameter(
            RuleActionParameterNames.Target,
            "Куда записать результат",
            RuleParameterKind.VariableName,
            IsRequired: false),
        new RuleActionParameter(
            RuleActionParameterNames.Label,
            "Описание броска",
            RuleParameterKind.Text,
            IsRequired: false),
    ])
{
    /// <inheritdoc />
    public override RuleActionOutcome Execute(
        RuleAction action,
        IRuleTarget target,
        string ruleName,
        IRuleActionServices services)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(target);

        if (!TryEvaluate(action, RuleActionParameterNames.Formula, target, services, out var value, out var error))
        {
            return new RuleActionOutcome(ruleName, Kind, error!, false);
        }

        var destination = action.GetParameter(RuleActionParameterNames.Target);

        if (!string.IsNullOrWhiteSpace(destination))
        {
            target.SetVariable(destination, value);
        }

        var label = action.GetParameter(RuleActionParameterNames.Label);
        var formula = action.GetParameter(RuleActionParameterNames.Formula);
        var prefix = string.IsNullOrWhiteSpace(label) ? "Бросок" : label;

        var description = string.IsNullOrWhiteSpace(destination)
            ? $"{prefix}: {formula} = {value.AsText()}"
            : $"{prefix}: {formula} = {value.AsText()} → {destination}";

        return new RuleActionOutcome(ruleName, Kind, description, Succeeded: true);
    }
}
