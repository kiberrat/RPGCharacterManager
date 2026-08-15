using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.History;

/// <summary>
/// Сообщения журнала подсистемы журнала событий.
/// </summary>
internal static partial class HistoryLog
{
    [LoggerMessage(
        EventId = 9001,
        Level = LogLevel.Information,
        Message = "Из журнала событий удалено записей: {Count}.")]
    public static partial void JournalCleared(ILogger logger, int count);

    [LoggerMessage(
        EventId = 9002,
        Level = LogLevel.Error,
        Message = "Ошибка подсистемы журнала событий.")]
    public static partial void JournalOperationFailed(ILogger logger, Exception exception);
}
