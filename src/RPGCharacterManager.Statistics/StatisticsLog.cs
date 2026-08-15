using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.Statistics;

/// <summary>
/// Сообщения журнала подсистемы статистики.
/// </summary>
internal static partial class StatisticsLog
{
    [LoggerMessage(
        EventId = 17001,
        Level = LogLevel.Information,
        Message = "Статистика собрана: бросков {Rolls}, атак {Attacks}.")]
    public static partial void ReportBuilt(ILogger logger, int rolls, int attacks);

    [LoggerMessage(
        EventId = 17002,
        Level = LogLevel.Error,
        Message = "Не удалось собрать статистику.")]
    public static partial void ReportFailed(ILogger logger, Exception exception);
}
