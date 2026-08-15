using System.Globalization;

namespace RPGCharacterManager.Shared.Extensions;

/// <summary>
/// Расширения для работы со строками.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Обрезает строку до указанной длины, добавляя многоточие.
    /// Используется для отображения длинных значений в интерфейсе и журналах.
    /// </summary>
    /// <param name="value">Исходная строка.</param>
    /// <param name="maximumLength">Максимальная длина результата вместе с многоточием.</param>
    /// <returns>Исходная либо укороченная строка.</returns>
    public static string Truncate(this string? value, int maximumLength)
    {
        const string Ellipsis = "…";

        if (string.IsNullOrEmpty(value) || value.Length <= maximumLength)
        {
            return value ?? string.Empty;
        }

        return maximumLength <= Ellipsis.Length
            ? Ellipsis
            : value[..(maximumLength - Ellipsis.Length)] + Ellipsis;
    }

    /// <summary>
    /// Сравнивает строки без учёта регистра и культуры.
    /// Применяется для поиска игровых объектов по внутренним именам.
    /// </summary>
    /// <param name="value">Первая строка.</param>
    /// <param name="other">Вторая строка.</param>
    /// <returns><see langword="true"/>, если строки эквивалентны.</returns>
    public static bool EqualsIgnoreCase(this string? value, string? other) =>
        string.Equals(value, other, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Приводит отображаемое имя к внутреннему имени: нижний регистр, пробелы заменены подчёркиваниями.
    /// Внутренние имена используются в формулах и правилах.
    /// </summary>
    /// <param name="value">Отображаемое имя.</param>
    /// <returns>Внутреннее имя объекта.</returns>
    public static string ToSystemName(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;
        var previousWasSeparator = false;

        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[length++] = char.ToLower(character, CultureInfo.CurrentCulture);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && length > 0)
            {
                buffer[length++] = '_';
                previousWasSeparator = true;
            }
        }

        if (length > 0 && buffer[length - 1] == '_')
        {
            length--;
        }

        return new string(buffer[..length]);
    }
}
