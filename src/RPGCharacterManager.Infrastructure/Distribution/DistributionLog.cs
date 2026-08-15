using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.Infrastructure.Distribution;

internal static partial class DistributionLog
{
    [LoggerMessage(EventId = 2100, Level = LogLevel.Warning, Message = "Не удалось проверить обновления приложения.")]
    internal static partial void UpdateCheckFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning, Message = "Не удалось загрузить обновление приложения.")]
    internal static partial void UpdateDownloadFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Error, Message = "Не удалось запустить установку обновления.")]
    internal static partial void UpdateApplyFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2110, Level = LogLevel.Warning, Message = "Сервер обратной связи вернул код {StatusCode}.")]
    internal static partial void FeedbackRejected(ILogger logger, int statusCode);

    [LoggerMessage(EventId = 2111, Level = LogLevel.Warning, Message = "Не удалось отправить обратную связь.")]
    internal static partial void FeedbackSendFailed(ILogger logger, Exception exception);
}