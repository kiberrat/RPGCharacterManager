using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Core.Models.Rules;

/// <summary>
/// Объект правил, хранящий параметры и признаки в памяти.
///
/// Используется окном тестирования правил как «пробный персонаж», а также любой
/// подсистемой, которой требуется применить правила к временному набору значений.
/// Реальные персонажи получат собственную реализацию <see cref="IRuleTarget"/>
/// на этапе разработки листа персонажа.
/// </summary>
public sealed class RuleTarget : IRuleTarget
{
    private readonly Dictionary<string, FormulaValue> _variables =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _tags = new(StringComparer.CurrentCultureIgnoreCase);

    /// <summary>
    /// Создаёт объект правил.
    /// </summary>
    /// <param name="displayName">Название объекта, отображаемое в отчёте.</param>
    public RuleTarget(string displayName) => DisplayName = Guard.NotNullOrWhiteSpace(displayName);

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<string> VariableNames => _variables.Keys;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Tags => _tags;

    /// <summary>
    /// Задаёт числовое значение параметра.
    /// </summary>
    /// <param name="name">Имя параметра.</param>
    /// <param name="value">Значение.</param>
    /// <returns>Тот же объект для построения цепочки вызовов.</returns>
    public RuleTarget WithVariable(string name, double value)
    {
        SetVariable(name, FormulaValue.FromNumber(value));
        return this;
    }

    /// <summary>
    /// Добавляет объекту признак.
    /// </summary>
    /// <param name="tag">Название признака.</param>
    /// <returns>Тот же объект для построения цепочки вызовов.</returns>
    public RuleTarget WithTag(string tag)
    {
        AddTag(tag);
        return this;
    }

    /// <inheritdoc />
    public bool TryGetVariable(string name, out FormulaValue value) =>
        _variables.TryGetValue(name, out value);

    /// <inheritdoc />
    public void SetVariable(string name, FormulaValue value)
    {
        Guard.NotNullOrWhiteSpace(name);
        _variables[name] = value;
    }

    /// <inheritdoc />
    public bool HasTag(string tag) => !string.IsNullOrWhiteSpace(tag) && _tags.Contains(tag);

    /// <inheritdoc />
    public bool AddTag(string tag) => !string.IsNullOrWhiteSpace(tag) && _tags.Add(tag);

    /// <inheritdoc />
    public bool RemoveTag(string tag) => !string.IsNullOrWhiteSpace(tag) && _tags.Remove(tag);

    /// <summary>
    /// Создаёт независимую копию объекта.
    /// Окно тестирования применяет правила к копии, чтобы исходные значения
    /// пробного персонажа сохранялись между запусками проверки.
    /// </summary>
    /// <returns>Копия объекта.</returns>
    public RuleTarget Clone()
    {
        var copy = new RuleTarget(DisplayName);

        foreach (var pair in _variables)
        {
            copy._variables[pair.Key] = pair.Value;
        }

        foreach (var tag in _tags)
        {
            copy._tags.Add(tag);
        }

        return copy;
    }
}
