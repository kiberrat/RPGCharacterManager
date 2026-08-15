using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Engine.Parsing;

namespace RPGCharacterManager.Engine.Functions;

/// <summary>
/// Базовый класс встроенной функции формул.
/// </summary>
/// <param name="name">Имя функции в выражении.</param>
/// <param name="description">Описание для редактора формул.</param>
/// <param name="minimumArgumentCount">Минимальное количество аргументов.</param>
/// <param name="maximumArgumentCount">Максимальное количество аргументов или <see langword="null"/>.</param>
public abstract class FormulaFunctionBase(
    string name,
    string description,
    int minimumArgumentCount,
    int? maximumArgumentCount) : IFormulaFunction
{
    /// <inheritdoc />
    public string Name { get; } = name;

    /// <inheritdoc />
    public string Description { get; } = description;

    /// <inheritdoc />
    public int MinimumArgumentCount { get; } = minimumArgumentCount;

    /// <inheritdoc />
    public int? MaximumArgumentCount { get; } = maximumArgumentCount;

    /// <inheritdoc />
    public abstract FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments);
}

/// <summary>Наименьшее из значений.</summary>
public sealed class MinimumFunction() : FormulaFunctionBase(
    "Минимум",
    "Возвращает наименьшее из переданных значений.",
    1,
    null)
{
    /// <inheritdoc />
    public override FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments) =>
        FormulaValue.FromNumber(arguments.Min(value => value.AsNumber()));
}

/// <summary>Наибольшее из значений.</summary>
public sealed class MaximumFunction() : FormulaFunctionBase(
    "Максимум",
    "Возвращает наибольшее из переданных значений.",
    1,
    null)
{
    /// <inheritdoc />
    public override FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments) =>
        FormulaValue.FromNumber(arguments.Max(value => value.AsNumber()));
}

/// <summary>Сумма значений.</summary>
public sealed class SumFunction() : FormulaFunctionBase(
    "Сумма",
    "Возвращает сумму переданных значений.",
    1,
    null)
{
    /// <inheritdoc />
    public override FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments) =>
        FormulaValue.FromNumber(arguments.Sum(value => value.AsNumber()));
}

/// <summary>Среднее арифметическое значений.</summary>
public sealed class AverageFunction() : FormulaFunctionBase(
    "Среднее",
    "Возвращает среднее арифметическое переданных значений.",
    1,
    null)
{
    /// <inheritdoc />
    public override FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments) =>
        FormulaValue.FromNumber(arguments.Average(value => value.AsNumber()));
}

/// <summary>Количество переданных значений.</summary>
public sealed class CountFunction() : FormulaFunctionBase(
    "Количество",
    "Возвращает количество переданных значений.",
    0,
    null)
{
    /// <inheritdoc />
    public override FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments) =>
        FormulaValue.FromNumber(arguments.Count);
}

/// <summary>Округление до ближайшего целого либо до указанного числа знаков.</summary>
public sealed class RoundFunction() : FormulaFunctionBase(
    "Округлить",
    "Округляет значение до ближайшего целого или до указанного числа знаков после запятой.",
    1,
    2)
{
    /// <inheritdoc />
    public override FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments)
    {
        var digits = arguments.Count > 1 ? (int)arguments[1].AsNumber() : 0;

        // MidpointRounding.AwayFromZero соответствует ожиданиям настольных игр:
        // 2,5 округляется до 3, а не до 2.
        return FormulaValue.FromNumber(
            Math.Round(arguments[0].AsNumber(), Math.Clamp(digits, 0, 15), MidpointRounding.AwayFromZero));
    }
}

/// <summary>Округление вниз.</summary>
public sealed class FloorFunction() : FormulaFunctionBase(
    "ОкруглитьВниз",
    "Округляет значение в меньшую сторону.",
    1,
    1)
{
    /// <inheritdoc />
    public override FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments) =>
        FormulaValue.FromNumber(Math.Floor(arguments[0].AsNumber()));
}

/// <summary>Округление вверх.</summary>
public sealed class CeilingFunction() : FormulaFunctionBase(
    "ОкруглитьВверх",
    "Округляет значение в большую сторону.",
    1,
    1)
{
    /// <inheritdoc />
    public override FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments) =>
        FormulaValue.FromNumber(Math.Ceiling(arguments[0].AsNumber()));
}

/// <summary>Модуль числа.</summary>
public sealed class AbsoluteFunction() : FormulaFunctionBase(
    "Модуль",
    "Возвращает абсолютное значение числа.",
    1,
    1)
{
    /// <inheritdoc />
    public override FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments) =>
        FormulaValue.FromNumber(Math.Abs(arguments[0].AsNumber()));
}

/// <summary>Ограничение значения диапазоном.</summary>
public sealed class ClampFunction() : FormulaFunctionBase(
    "Ограничить",
    "Ограничивает значение указанными наименьшей и наибольшей границами.",
    3,
    3)
{
    /// <inheritdoc />
    public override FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments)
    {
        var minimum = arguments[1].AsNumber();
        var maximum = arguments[2].AsNumber();

        // Границы, заданные пользователем в обратном порядке, не должны приводить к ошибке.
        if (minimum > maximum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        return FormulaValue.FromNumber(Math.Clamp(arguments[0].AsNumber(), minimum, maximum));
    }
}

/// <summary>Условный выбор значения.</summary>
public sealed class IfFunction() : FormulaFunctionBase(
    "Если",
    "Возвращает второй аргумент, если первый истинен, иначе третий.",
    2,
    3), IConditionalFunction
{
    /// <inheritdoc />
    public override FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments) =>
        arguments[0].AsBoolean()
            ? arguments[1]
            : arguments.Count > 2 ? arguments[2] : FormulaValue.FromNumber(0);
}

/// <summary>Бросок кубиков, заданный аргументами.</summary>
public sealed class DiceFunction : FormulaFunctionBase, IRandomAwareFunction
{
    private readonly IRandomSource _random;

    /// <summary>
    /// Создаёт функцию броска кубиков.
    /// </summary>
    /// <param name="random">Источник случайных значений.</param>
    public DiceFunction(IRandomSource random)
        : base("Кубик", "Выполняет бросок кубиков: Кубик(количество; грани).", 1, 2) =>
        _random = random;

    /// <inheritdoc />
    public IFormulaFunction WithRandom(IRandomSource random) => new DiceFunction(random);

    /// <inheritdoc />
    public override FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments)
    {
        var count = arguments.Count > 1 ? (int)arguments[0].AsNumber() : 1;
        var sides = arguments.Count > 1 ? (int)arguments[1].AsNumber() : (int)arguments[0].AsNumber();

        // Проверка диапазонов выполняется тем же узлом, что и запись вида 2d6,
        // поэтому правила одинаковы для обеих форм записи.
        var node = new DiceNode(count, sides);
        return node.Evaluate(null, new EvaluationServices(
            new Dictionary<string, IFormulaFunction>(StringComparer.OrdinalIgnoreCase),
            _random));
    }
}

/// <summary>Случайное целое число в диапазоне.</summary>
public sealed class RandomFunction : FormulaFunctionBase, IRandomAwareFunction
{
    private readonly IRandomSource _random;

    /// <summary>
    /// Создаёт функцию получения случайного числа.
    /// </summary>
    /// <param name="random">Источник случайных значений.</param>
    public RandomFunction(IRandomSource random)
        : base("Случайное", "Возвращает случайное целое число из диапазона включительно.", 2, 2) =>
        _random = random;

    /// <inheritdoc />
    public IFormulaFunction WithRandom(IRandomSource random) => new RandomFunction(random);

    /// <inheritdoc />
    public override FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments)
    {
        var minimum = (int)arguments[0].AsNumber();
        var maximum = (int)arguments[1].AsNumber();

        if (minimum > maximum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        return FormulaValue.FromNumber(_random.Next(minimum, maximum));
    }
}
