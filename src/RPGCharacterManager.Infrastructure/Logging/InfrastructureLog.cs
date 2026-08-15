using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.Infrastructure.Logging;

/// <summary>
/// Сообщения журнала слоя инфраструктуры.
///
/// Методы генерируются исходным генератором <see cref="LoggerMessageAttribute"/>:
/// строка шаблона разбирается на этапе компиляции, аргументы не упаковываются в объекты,
/// а при отключённом уровне журналирования вызов не выполняет никакой работы.
/// Централизованное объявление также исключает расхождение текстов сообщений.
/// </summary>
internal static partial class InfrastructureLog
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Файл настроек не найден, применяются значения по умолчанию: {Path}")]
    public static partial void SettingsFileMissing(ILogger logger, string path);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Настройки загружены: {Path}")]
    public static partial void SettingsLoaded(ILogger logger, string path);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "Не удалось прочитать файл настроек, применяются значения по умолчанию: {Path}")]
    public static partial void SettingsReadFailed(ILogger logger, Exception exception, string path);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Debug, Message = "Настройки сохранены: {Path}")]
    public static partial void SettingsSaved(ILogger logger, string path);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Error, Message = "Не удалось сохранить настройки приложения.")]
    public static partial void SettingsSaveFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Error,
        Message = "Обработчик события {EventType} завершился ошибкой.")]
    public static partial void EventHandlerFailed(ILogger logger, Exception exception, string eventType);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Debug, Message = "Запущена фоновая задача «{Title}» ({TaskId}).")]
    public static partial void BackgroundTaskStarted(ILogger logger, string title, Guid taskId);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Debug, Message = "Фоновая задача «{Title}» завершена успешно.")]
    public static partial void BackgroundTaskCompleted(ILogger logger, string title);

    [LoggerMessage(EventId = 1203, Level = LogLevel.Information, Message = "Фоновая задача «{Title}» отменена.")]
    public static partial void BackgroundTaskCancelled(ILogger logger, string title);

    [LoggerMessage(EventId = 1204, Level = LogLevel.Error, Message = "Фоновая задача «{Title}» завершилась ошибкой.")]
    public static partial void BackgroundTaskFailed(ILogger logger, Exception exception, string title);

    [LoggerMessage(
        EventId = 1301,
        Level = LogLevel.Information,
        Message = "Централизованная обработка ошибок активирована.")]
    public static partial void ExceptionHandlerAttached(ILogger logger);

    [LoggerMessage(EventId = 1302, Message = "Необработанная ошибка в «{Source}».")]
    public static partial void UnhandledError(ILogger logger, LogLevel level, Exception exception, string source);

    [LoggerMessage(EventId = 1303, Level = LogLevel.Error, Message = "Не удалось опубликовать сведения об ошибке.")]
    public static partial void ErrorPublicationFailed(ILogger logger, Exception exception);
}
