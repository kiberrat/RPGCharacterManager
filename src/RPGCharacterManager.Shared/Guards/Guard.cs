using System.Runtime.CompilerServices;

namespace RPGCharacterManager.Shared.Guards;

/// <summary>
/// Проверки входных аргументов.
/// Единая точка валидации параметров публичных методов, чтобы не дублировать
/// однотипные конструкции <c>if (x is null) throw ...</c> по всему решению.
/// </summary>
public static class Guard
{
    /// <summary>
    /// Проверяет, что значение не равно <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">Ссылочный тип проверяемого значения.</typeparam>
    /// <param name="value">Проверяемое значение.</param>
    /// <param name="parameterName">Имя параметра. Подставляется компилятором.</param>
    /// <returns>Исходное значение, если проверка пройдена.</returns>
    /// <exception cref="ArgumentNullException">Значение равно <see langword="null"/>.</exception>
    public static T NotNull<T>(T? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        return value;
    }

    /// <summary>
    /// Проверяет, что строка не является <see langword="null"/>, пустой строкой или строкой из пробельных символов.
    /// </summary>
    /// <param name="value">Проверяемая строка.</param>
    /// <param name="parameterName">Имя параметра. Подставляется компилятором.</param>
    /// <returns>Исходную строку, если проверка пройдена.</returns>
    /// <exception cref="ArgumentException">Строка пуста или состоит из пробельных символов.</exception>
    public static string NotNullOrWhiteSpace(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Значение не должно быть пустым.", parameterName);
        }

        return value;
    }

    /// <summary>
    /// Проверяет, что число находится в заданном диапазоне включительно.
    /// </summary>
    /// <typeparam name="T">Сравнимый числовой тип.</typeparam>
    /// <param name="value">Проверяемое значение.</param>
    /// <param name="minimum">Минимально допустимое значение.</param>
    /// <param name="maximum">Максимально допустимое значение.</param>
    /// <param name="parameterName">Имя параметра. Подставляется компилятором.</param>
    /// <returns>Исходное значение, если проверка пройдена.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Значение выходит за пределы диапазона.</exception>
    public static T InRange<T>(
        T value,
        T minimum,
        T maximum,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(minimum) < 0 || value.CompareTo(maximum) > 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Значение должно находиться в диапазоне от {minimum} до {maximum}.");
        }

        return value;
    }
}
