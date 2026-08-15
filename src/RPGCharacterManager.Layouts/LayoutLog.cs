using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.Layouts;

/// <summary>
/// Сообщения журнала подсистемы макетов.
/// </summary>
internal static partial class LayoutLog
{
    [LoggerMessage(
        EventId = 14001,
        Level = LogLevel.Information,
        Message = "Создан встроенный макет листа персонажа: вкладок — {TabCount}.")]
    public static partial void DefaultLayoutCreated(ILogger logger, int tabCount);

    [LoggerMessage(
        EventId = 14002,
        Level = LogLevel.Information,
        Message = "Создан макет «{Name}» ({LayoutId}).")]
    public static partial void LayoutCreated(ILogger logger, string name, Guid layoutId);

    [LoggerMessage(
        EventId = 14003,
        Level = LogLevel.Error,
        Message = "Не удалось выполнить действие с макетом: {Action}.")]
    public static partial void ActionFailed(ILogger logger, Exception exception, string action);
}
