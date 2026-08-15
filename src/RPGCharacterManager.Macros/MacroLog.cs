using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.Macros;

/// <summary>
/// Сообщения журнала подсистемы макросов.
/// </summary>
internal static partial class MacroLog
{
    [LoggerMessage(
        EventId = 16001,
        Level = LogLevel.Information,
        Message = "Сохранён макрос «{Name}» ({MacroId}).")]
    public static partial void MacroSaved(ILogger logger, string name, Guid macroId);

    [LoggerMessage(
        EventId = 16002,
        Level = LogLevel.Information,
        Message = "Удалён макрос «{Name}» ({MacroId}).")]
    public static partial void MacroDeleted(ILogger logger, string name, Guid macroId);

    [LoggerMessage(
        EventId = 16003,
        Level = LogLevel.Information,
        Message = "Выполнен макрос «{Name}» для персонажа «{CharacterName}»: изменений — {Changes}.")]
    public static partial void MacroExecuted(
        ILogger logger,
        string name,
        string characterName,
        int changes);

    [LoggerMessage(
        EventId = 16004,
        Level = LogLevel.Error,
        Message = "Не удалось выполнить действие с макросом: {Action}.")]
    public static partial void ActionFailed(ILogger logger, Exception exception, string action);
}
