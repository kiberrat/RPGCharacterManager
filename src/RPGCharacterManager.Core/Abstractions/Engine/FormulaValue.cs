using System.Globalization;

namespace RPGCharacterManager.Core.Abstractions.Engine;

/// <summary>
/// Тип значения, участвующего в вычислениях.
/// </summary>
public enum FormulaValueKind
{
    /// <summary>Число.</summary>
    Number = 0,

    /// <summary>Логическое значение.</summary>
    Boolean = 1,

    /// <summary>Строка.</summary>
    Text = 2,
}

/// <summary>
/// Значение формулы.
///
/// Движок работает с единым типом значения, поэтому одно и то же выражение может
/// возвращать число, логический признак или строку в зависимости от игровой механики.
/// </summary>
public readonly struct FormulaValue : IEquatable<FormulaValue>
{
    private readonly double _number;
    private readonly string? _text;

    private FormulaValue(FormulaValueKind kind, double number, string? text)
    {
        Kind = kind;
        _number = number;
        _text = text;
    }

    /// <summary>Логическое значение «истина», представленное числом.</summary>
    public const double True = 1.0;

    /// <summary>Логическое значение «ложь», представленное числом.</summary>
    public const double False = 0.0;

    /// <summary>Тип значения.</summary>
    public FormulaValueKind Kind { get; }

    /// <summary>
    /// Создаёт числовое значение.
    /// </summary>
    /// <param name="value">Число.</param>
    /// <returns>Значение формулы.</returns>
    public static FormulaValue FromNumber(double value) => new(FormulaValueKind.Number, value, null);

    /// <summary>
    /// Создаёт логическое значение.
    /// </summary>
    /// <param name="value">Логическое значение.</param>
    /// <returns>Значение формулы.</returns>
    public static FormulaValue FromBoolean(bool value) =>
        new(FormulaValueKind.Boolean, value ? True : False, null);

    /// <summary>
    /// Создаёт строковое значение.
    /// </summary>
    /// <param name="value">Строка.</param>
    /// <returns>Значение формулы.</returns>
    public static FormulaValue FromText(string value) => new(FormulaValueKind.Text, 0, value);

    /// <summary>
    /// Возвращает числовое представление значения.
    /// Логическое значение преобразуется в 1 или 0, строка — по правилам разбора числа.
    /// </summary>
    /// <returns>Число.</returns>
    public double AsNumber() => Kind switch
    {
        FormulaValueKind.Text => double.TryParse(
            _text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed) ? parsed : 0.0,
        _ => _number,
    };

    /// <summary>
    /// Возвращает логическое представление значения.
    /// Число считается истиной, если оно не равно нулю; строка — если она не пуста.
    /// </summary>
    /// <returns>Логическое значение.</returns>
    public bool AsBoolean() => Kind switch
    {
        FormulaValueKind.Text => !string.IsNullOrEmpty(_text),
        _ => Math.Abs(_number) > double.Epsilon,
    };

    /// <summary>
    /// Возвращает строковое представление значения.
    /// </summary>
    /// <returns>Строка.</returns>
    public string AsText() => Kind switch
    {
        FormulaValueKind.Text => _text ?? string.Empty,
        FormulaValueKind.Boolean => AsBoolean() ? "истина" : "ложь",
        _ => _number.ToString("0.####", CultureInfo.CurrentCulture),
    };

    /// <inheritdoc />
    public bool Equals(FormulaValue other) => Kind == other.Kind
        && Kind == FormulaValueKind.Text
            ? string.Equals(_text, other._text, StringComparison.Ordinal)
            : Math.Abs(AsNumber() - other.AsNumber()) < double.Epsilon;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is FormulaValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Kind == FormulaValueKind.Text
        ? StringComparer.Ordinal.GetHashCode(_text ?? string.Empty)
        : _number.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => AsText();

    /// <summary>
    /// Сравнивает значения на равенство.
    /// </summary>
    /// <param name="left">Первое значение.</param>
    /// <param name="right">Второе значение.</param>
    /// <returns><see langword="true"/>, если значения равны.</returns>
    public static bool operator ==(FormulaValue left, FormulaValue right) => left.Equals(right);

    /// <summary>
    /// Сравнивает значения на неравенство.
    /// </summary>
    /// <param name="left">Первое значение.</param>
    /// <param name="right">Второе значение.</param>
    /// <returns><see langword="true"/>, если значения различны.</returns>
    public static bool operator !=(FormulaValue left, FormulaValue right) => !left.Equals(right);
}
