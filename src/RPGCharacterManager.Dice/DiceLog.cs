using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.Dice;

/// <summary>
/// Сообщения журнала подсистемы бросков.
/// </summary>
internal static partial class DiceLog
{
    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Information,
        Message = "Бросок «{Expression}» ({Mode}) дал {Total}.")]
    public static partial void RollPerformed(ILogger logger, string expression, string mode, double total);

    [LoggerMessage(
        EventId = 8002,
        Level = LogLevel.Warning,
        Message = "Не удалось выполнить бросок «{Expression}»: {Reason}")]
    public static partial void RollFailed(ILogger logger, string expression, string reason);

    [LoggerMessage(
        EventId = 8003,
        Level = LogLevel.Information,
        Message = "Из журнала бросков удалено записей: {Count}.")]
    public static partial void HistoryCleared(ILogger logger, int count);

    [LoggerMessage(
        EventId = 8004,
        Level = LogLevel.Error,
        Message = "Ошибка подсистемы бросков.")]
    public static partial void DiceOperationFailed(ILogger logger, Exception exception);
}
