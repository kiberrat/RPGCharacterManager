using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Engine;

/// <summary>
/// Источник значений переменных на основе словаря.
///
/// Применяется для проверки формул в редакторе, тестирования правил на пробном
/// персонаже и вычислений, не связанных с базой данных.
/// </summary>
public sealed class DictionaryFormulaContext : IFormulaContext
{
    private readonly Dictionary<string, FormulaValue> _values;
    private readonly IFormulaContext? _fallback;

    /// <summary>
    /// Создаёт источник значений.
    /// </summary>
    /// <param name="fallback">
    /// Источник, к которому выполняется обращение, если переменная не найдена в словаре.
    /// Позволяет дополнять значения персонажа временными переменными правила.
    /// </param>
    public DictionaryFormulaContext(IFormulaContext? fallback = null)
    {
        _values = new Dictionary<string, FormulaValue>(StringComparer.OrdinalIgnoreCase);
        _fallback = fallback;
    }

    /// <summary>
    /// Задаёт числовое значение переменной.
    /// </summary>
    /// <param name="name">Имя переменной.</param>
    /// <param name="value">Значение.</param>
    /// <returns>Тот же источник для построения цепочки вызовов.</returns>
    public DictionaryFormulaContext Set(string name, double value) =>
        Set(name, FormulaValue.FromNumber(value));

    /// <summary>
    /// Задаёт логическое значение переменной.
    /// </summary>
    /// <param name="name">Имя переменной.</param>
    /// <param name="value">Значение.</param>
    /// <returns>Тот же источник для построения цепочки вызовов.</returns>
    public DictionaryFormulaContext Set(string name, bool value) =>
        Set(name, FormulaValue.FromBoolean(value));

    /// <summary>
    /// Задаёт строковое значение переменной.
    /// </summary>
    /// <param name="name">Имя переменной.</param>
    /// <param name="value">Значение.</param>
    /// <returns>Тот же источник для построения цепочки вызовов.</returns>
    public DictionaryFormulaContext Set(string name, string value) =>
        Set(name, FormulaValue.FromText(value));

    /// <summary>
    /// Задаёт значение переменной.
    /// </summary>
    /// <param name="name">Имя переменной.</param>
    /// <param name="value">Значение.</param>
    /// <returns>Тот же источник для построения цепочки вызовов.</returns>
    public DictionaryFormulaContext Set(string name, FormulaValue value)
    {
        Guard.NotNullOrWhiteSpace(name);

        _values[name] = value;
        return this;
    }

    /// <inheritdoc />
    public bool TryGetVariable(string name, out FormulaValue value)
    {
        if (_values.TryGetValue(name, out value))
        {
            return true;
        }

        return _fallback is not null && _fallback.TryGetVariable(name, out value);
    }
}

/// <summary>
/// Источник случайных значений на основе <see cref="Random"/>.
/// </summary>
public sealed class SystemRandomSource : IRandomSource
{
    /// <inheritdoc />
    public int Next(int minimumInclusive, int maximumInclusive) =>
        Random.Shared.Next(minimumInclusive, maximumInclusive + 1);
}

/// <summary>
/// Источник значений с заранее известным исходом броска.
///
/// Применяется при вычислении границ выражения: наименьшее значение формулы урона
/// получается, когда каждый кубик показывает единицу, наибольшее — когда все кубики
/// показывают максимум.
/// </summary>
public sealed class FixedRandomSource : IRandomSource
{
    private readonly bool _takeMaximum;

    private FixedRandomSource(bool takeMaximum) => _takeMaximum = takeMaximum;

    /// <summary>Источник, всегда возвращающий наименьшее значение диапазона.</summary>
    public static FixedRandomSource Minimum { get; } = new(takeMaximum: false);

    /// <summary>Источник, всегда возвращающий наибольшее значение диапазона.</summary>
    public static FixedRandomSource Maximum { get; } = new(takeMaximum: true);

    /// <inheritdoc />
    public int Next(int minimumInclusive, int maximumInclusive) =>
        _takeMaximum ? maximumInclusive : minimumInclusive;
}
