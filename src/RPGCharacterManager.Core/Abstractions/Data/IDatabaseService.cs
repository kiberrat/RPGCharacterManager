using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Data;

/// <summary>
/// Обслуживание базы данных приложения.
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// Подготавливает базу данных к работе: создаёт файл при первом запуске
    /// и применяет непринятые миграции схемы.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат подготовки базы данных.</returns>
    Task<Result> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет доступность базы данных.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><see langword="true"/>, если соединение установлено.</returns>
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}
