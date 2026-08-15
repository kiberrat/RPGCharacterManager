namespace RPGCharacterManager.Shared.Results;

/// <summary>
/// Результат операции без возвращаемого значения.
/// Используется там, где сбой является ожидаемым сценарием и не должен
/// выражаться исключением (проверка формул, импорт, восстановление базы данных).
/// </summary>
public class Result
{
    /// <summary>
    /// Создаёт результат операции.
    /// </summary>
    /// <param name="isSuccess">Признак успешного завершения.</param>
    /// <param name="error">Описание ошибки. Задаётся только для неуспешного результата.</param>
    protected Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Операция завершилась успешно.</summary>
    public bool IsSuccess { get; }

    /// <summary>Операция завершилась неудачей.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Описание ошибки или <see langword="null"/> для успешного результата.</summary>
    public string? Error { get; }

    /// <summary>
    /// Создаёт успешный результат.
    /// </summary>
    /// <returns>Успешный результат операции.</returns>
    public static Result Success() => new(true, null);

    /// <summary>
    /// Создаёт неуспешный результат.
    /// </summary>
    /// <param name="error">Описание причины сбоя.</param>
    /// <returns>Неуспешный результат операции.</returns>
    public static Result Failure(string error) => new(false, error);

    /// <summary>
    /// Создаёт успешный результат со значением.
    /// </summary>
    /// <typeparam name="TValue">Тип возвращаемого значения.</typeparam>
    /// <param name="value">Возвращаемое значение.</param>
    /// <returns>Успешный результат операции со значением.</returns>
    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.FromValue(value);

    /// <summary>
    /// Создаёт неуспешный результат для операции со значением.
    /// </summary>
    /// <typeparam name="TValue">Тип возвращаемого значения.</typeparam>
    /// <param name="error">Описание причины сбоя.</param>
    /// <returns>Неуспешный результат операции.</returns>
    public static Result<TValue> Failure<TValue>(string error) => Result<TValue>.FromError(error);
}
