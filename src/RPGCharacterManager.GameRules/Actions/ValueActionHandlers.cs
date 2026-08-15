using System.Globalization;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Rules;

namespace RPGCharacterManager.GameRules.Actions;

/// <summary>
/// Имена параметров, общие для встроенных обработчиков действий.
/// </summary>
public static class RuleActionParameterNames
{
    /// <summary>Имя изменяемого параметра объекта.</summary>
    public const string Target = "параметр";

    /// <summary>Выражение, задающее величину изменения.</summary>
    public const string Value = "значение";

    /// <summary>Название признака: эффекта, черты, владения.</summary>
    public const string Tag = "признак";

    /// <summary>Имя ресурса.</summary>
    public const string Resource = "ресурс";

    /// <summary>Формула броска.</summary>
    public const string Formula = "формула";

    /// <summary>Пояснение, отображаемое в отчёте.</summary>
    public const string Label = "описание";
}

/// <summary>
/// Базовый класс обработчика действия правила.
/// </summary>
/// <param name="kind">Ключ вида действия.</param>
/// <param name="displayName">Отображаемое название.</param>
/// <param name="description">Пояснение для редактора.</param>
/// <param name="parameters">Описание параметров.</param>
public abstract class RuleActionHandlerBase(
    string kind,
    string displayName,
    string description,
    IReadOnlyList<RuleActionParameter> parameters) : IRuleActionHandler
{
    /// <inheritdoc />
    public string Kind { get; } = kind;

    /// <inheritdoc />
    public string DisplayName { get; } = displayName;

    /// <inheritdoc />
    public string Description { get; } = description;

    /// <inheritdoc />
    public IReadOnlyList<RuleActionParameter> Parameters { get; } = parameters;

    /// <inheritdoc />
    public abstract RuleActionOutcome Execute(
        RuleAction action,
        IRuleTarget target,
        string ruleName,
        IRuleActionServices services);

    /// <summary>
    /// Вычисляет выражение параметра действия единым движком формул.
    /// </summary>
    /// <param name="action">Выполняемое действие.</param>
    /// <param name="parameterName">Имя параметра.</param>
    /// <param name="target">Объект, предоставляющий значения переменных.</param>
    /// <param name="services">Службы движка.</param>
    /// <param name="value">Вычисленное значение.</param>
    /// <param name="error">Описание ошибки вычисления.</param>
    /// <returns><see langword="true"/>, если выражение вычислено успешно.</returns>
    protected static bool TryEvaluate(
        RuleAction action,
        string parameterName,
        IRuleTarget target,
        IRuleActionServices services,
        out FormulaValue value,
        out string? error)
    {
        var expression = action.GetParameter(parameterName);
        var result = services.Formulas.Evaluate(expression, target);

        if (result.IsSuccess)
        {
            value = result.Value;
            error = null;
            return true;
        }

        value = default;
        error = $"Не удалось вычислить «{expression}»: {result.Error}";
        return false;
    }

    /// <summary>
    /// Формирует числовое значение в виде текста для отчёта.
    /// </summary>
    /// <param name="value">Число.</param>
    /// <returns>Текстовое представление.</returns>
    protected static string FormatNumber(double value) =>
        value.ToString("0.####", CultureInfo.CurrentCulture);
}

/// <summary>Устанавливает параметру объекта заданное значение.</summary>
public sealed class SetValueActionHandler() : RuleActionHandlerBase(
    "установить_значение",
    "Установить значение",
    "Присваивает параметру объекта результат вычисления выражения.",
    [
        new RuleActionParameter(RuleActionParameterNames.Target, "Параметр", RuleParameterKind.VariableName),
        new RuleActionParameter(RuleActionParameterNames.Value, "Новое значение", RuleParameterKind.Expression),
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

        var name = action.GetParameter(RuleActionParameterNames.Target);

        if (string.IsNullOrWhiteSpace(name))
        {
            return new RuleActionOutcome(ruleName, Kind, "Не указан изменяемый параметр", false);
        }

        if (!TryEvaluate(action, RuleActionParameterNames.Value, target, services, out var value, out var error))
        {
            return new RuleActionOutcome(ruleName, Kind, error!, false);
        }

        target.SetVariable(name, value);

        return new RuleActionOutcome(
            ruleName,
            Kind,
            $"{name} = {value.AsText()}",
            Succeeded: true);
    }
}

/// <summary>Изменяет параметр объекта на указанную величину.</summary>
public sealed class AdjustValueActionHandler() : RuleActionHandlerBase(
    "изменить_значение",
    "Изменить значение",
    "Прибавляет к параметру результат выражения. Отрицательное значение уменьшает параметр.",
    [
        new RuleActionParameter(RuleActionParameterNames.Target, "Параметр", RuleParameterKind.VariableName),
        new RuleActionParameter(RuleActionParameterNames.Value, "Изменение", RuleParameterKind.Expression),
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

        var name = action.GetParameter(RuleActionParameterNames.Target);

        if (string.IsNullOrWhiteSpace(name))
        {
            return new RuleActionOutcome(ruleName, Kind, "Не указан изменяемый параметр", false);
        }

        if (!TryEvaluate(action, RuleActionParameterNames.Value, target, services, out var delta, out var error))
        {
            return new RuleActionOutcome(ruleName, Kind, error!, false);
        }

        var current = target.TryGetVariable(name, out var existing) ? existing.AsNumber() : 0;
        var updated = current + delta.AsNumber();

        target.SetVariable(name, FormulaValue.FromNumber(updated));

        return new RuleActionOutcome(
            ruleName,
            Kind,
            $"{name}: {FormatNumber(current)} → {FormatNumber(updated)}",
            Succeeded: true);
    }
}

/// <summary>Добавляет объекту признак: эффект, черту или владение.</summary>
public sealed class AddTagActionHandler() : RuleActionHandlerBase(
    "добавить_эффект",
    "Добавить эффект",
    "Добавляет объекту признак: эффект, черту, владение или иную пометку.",
    [
        new RuleActionParameter(RuleActionParameterNames.Tag, "Название", RuleParameterKind.TagName),
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

        var tag = action.GetParameter(RuleActionParameterNames.Tag);

        if (string.IsNullOrWhiteSpace(tag))
        {
            return new RuleActionOutcome(ruleName, Kind, "Не указано название эффекта", false);
        }

        var added = target.AddTag(tag);

        return new RuleActionOutcome(
            ruleName,
            Kind,
            added ? $"Добавлен эффект «{tag}»" : $"Эффект «{tag}» уже присутствует",
            Succeeded: true);
    }
}

/// <summary>Удаляет признак объекта.</summary>
public sealed class RemoveTagActionHandler() : RuleActionHandlerBase(
    "удалить_эффект",
    "Удалить эффект",
    "Удаляет у объекта ранее добавленный признак.",
    [
        new RuleActionParameter(RuleActionParameterNames.Tag, "Название", RuleParameterKind.TagName),
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

        var tag = action.GetParameter(RuleActionParameterNames.Tag);

        if (string.IsNullOrWhiteSpace(tag))
        {
            return new RuleActionOutcome(ruleName, Kind, "Не указано название эффекта", false);
        }

        var removed = target.RemoveTag(tag);

        return new RuleActionOutcome(
            ruleName,
            Kind,
            removed ? $"Удалён эффект «{tag}»" : $"Эффект «{tag}» отсутствовал",
            Succeeded: true);
    }
}
