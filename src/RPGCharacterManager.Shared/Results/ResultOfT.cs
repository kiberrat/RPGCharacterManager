namespace RPGCharacterManager.Shared.Results;

/// <summary>
/// Результат операции, возвращающей значение.
/// </summary>
/// <typeparam name="TValue">Тип возвращаемого значения.</typeparam>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    private Result(bool isSuccess, TValue? value, string? error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Значение успешной операции.
    /// </summary>
    /// <exception cref="InvalidOperationException">Обращение к значению неуспешного результата.</exception>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"Обращение к значению неуспешного результата. Причина сбоя: {Error}");

    /// <summary>
    /// Создаёт успешный результат со значением.
    /// </summary>
    /// <param name="value">Возвращаемое значение.</param>
    /// <returns>Успешный результат.</returns>
    internal static Result<TValue> FromValue(TValue value) => new(true, value, null);

    /// <summary>
    /// Создаёт неуспешный результат.
    /// </summary>
    /// <param name="error">Описание причины сбоя.</param>
    /// <returns>Неуспешный результат.</returns>
    internal static Result<TValue> FromError(string error) => new(false, default, error);

    /// <summary>
    /// Возвращает значение успешного результата либо указанное значение по умолчанию.
    /// </summary>
    /// <param name="defaultValue">Значение, возвращаемое при неуспешном результате.</param>
    /// <returns>Значение результата или <paramref name="defaultValue"/>.</returns>
    public TValue GetValueOrDefault(TValue defaultValue) => IsSuccess ? _value! : defaultValue;
}
