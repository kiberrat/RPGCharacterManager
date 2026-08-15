using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.Database;

/// <summary>
/// Сообщения журнала слоя доступа к данным.
/// Методы генерируются исходным генератором <see cref="LoggerMessageAttribute"/>.
/// </summary>
internal static partial class DatabaseLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "База данных готова к работе: {Path}")]
    public static partial void DatabaseReady(ILogger logger, string path);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Error, Message = "Не удалось подготовить базу данных: {Path}")]
    public static partial void DatabaseInitializationFailed(ILogger logger, Exception exception, string path);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "Проверка соединения с базой данных завершилась ошибкой.")]
    public static partial void ConnectionCheckFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "Создана резервная копия базы данных: {Path} ({SizeInBytes} байт).")]
    public static partial void BackupCreated(ILogger logger, string path, long sizeInBytes);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Error, Message = "Не удалось создать резервную копию.")]
    public static partial void BackupFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Information,
        Message = "База данных восстановлена из копии: {Path}")]
    public static partial void BackupRestored(ILogger logger, string path);

    [LoggerMessage(
        EventId = 2104,
        Level = LogLevel.Error,
        Message = "Не удалось восстановить базу данных из копии: {Path}")]
    public static partial void RestoreFailed(ILogger logger, Exception exception, string path);

    [LoggerMessage(
        EventId = 2105,
        Level = LogLevel.Warning,
        Message = "Не удалось удалить устаревшую резервную копию: {Path}")]
    public static partial void BackupDeleteFailed(ILogger logger, Exception exception, string path);
}
