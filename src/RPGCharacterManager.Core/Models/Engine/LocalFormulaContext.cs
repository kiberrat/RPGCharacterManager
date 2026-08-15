using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Core.Models.Engine;

/// <summary>
/// Источник значений переменных, дополняющий основной набор несколькими локальными.
///
/// Применяется там, где формула получает переменные, существующие только внутри неё:
/// «значение» в формуле модификатора характеристики, «владение» и «характеристика»
/// в формулах навыка и оружия, «урон» в формуле критического попадания. Основные
/// значения при этом не изменяются, поэтому вычисление одной формулы не влияет
/// на остальные.
/// </summary>
public sealed class LocalFormulaContext : IFormulaContext
{
    private readonly IFormulaContext _outer;
    private readonly Dictionary<string, FormulaValue> _local;

    /// <summary>
    /// Создаёт источник значений с локальными переменными.
    /// </summary>
    /// <param name="outer">Основной источник значений.</param>
    public LocalFormulaContext(IFormulaContext outer)
    {
        _outer = Guard.NotNull(outer);
        _local = new Dictionary<string, FormulaValue>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Задаёт локальное числовое значение.
    /// </summary>
    /// <param name="name">Имя переменной.</param>
    /// <param name="value">Значение.</param>
    /// <returns>Тот же источник значений для построения цепочки вызовов.</returns>
    public LocalFormulaContext With(string name, double value)
    {
        _local[name] = FormulaValue.FromNumber(value);
        return this;
    }

    /// <summary>
    /// Задаёт локальное текстовое значение.
    /// </summary>
    /// <param name="name">Имя переменной.</param>
    /// <param name="value">Значение.</param>
    /// <returns>Тот же источник значений для построения цепочки вызовов.</returns>
    public LocalFormulaContext With(string name, string value)
    {
        _local[name] = FormulaValue.FromText(value);
        return this;
    }

    /// <inheritdoc />
    public bool TryGetVariable(string name, out FormulaValue value) =>
        _local.TryGetValue(name, out value) || _outer.TryGetVariable(name, out value);
}
