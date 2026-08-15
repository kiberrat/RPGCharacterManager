using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.Ai;

/// <summary>
/// Сообщения журнала подсистемы помощника.
///
/// Ключ доступа в журнал не попадает ни при каких обстоятельствах: записываются
/// только имя модели, имена вызванных инструментов и расход единиц обработки текста.
/// </summary>
internal static partial class AiLog
{
    [LoggerMessage(
        EventId = 10001,
        Level = LogLevel.Information,
        Message = "Связь с моделью «{Model}» установлена за {Milliseconds} мс, доступно моделей: {Count}.")]
    public static partial void ConnectionChecked(ILogger logger, string model, long milliseconds, int count);

    [LoggerMessage(
        EventId = 10002,
        Level = LogLevel.Warning,
        Message = "Не удалось обратиться к модели «{Model}»: {Reason}")]
    public static partial void RequestFailed(ILogger logger, string model, string reason);

    [LoggerMessage(
        EventId = 10003,
        Level = LogLevel.Information,
        Message = "Помощник вызвал инструмент «{Tool}».")]
    public static partial void ToolInvoked(ILogger logger, string tool);

    [LoggerMessage(
        EventId = 10004,
        Level = LogLevel.Warning,
        Message = "Инструмент «{Tool}» завершился ошибкой.")]
    public static partial void ToolFailed(ILogger logger, Exception exception, string tool);

    [LoggerMessage(
        EventId = 10005,
        Level = LogLevel.Information,
        Message = "Помощник ответил за {Rounds} обращений, израсходовано единиц: {Tokens}.")]
    public static partial void AnswerProduced(ILogger logger, int rounds, int tokens);

    [LoggerMessage(
        EventId = 10006,
        Level = LogLevel.Information,
        Message = "Применено предложение помощника: {Summary}")]
    public static partial void ProposalApplied(ILogger logger, string summary);

    [LoggerMessage(
        EventId = 10007,
        Level = LogLevel.Warning,
        Message = "Не удалось применить предложение помощника: {Summary}")]
    public static partial void ProposalFailed(ILogger logger, string summary);

    [LoggerMessage(
        EventId = 10008,
        Level = LogLevel.Information,
        Message = "Разбор источника «{Source}»: часть {Step} из {Total}.")]
    public static partial void SourceChunkStarted(ILogger logger, string source, int step, int total);

    [LoggerMessage(
        EventId = 10009,
        Level = LogLevel.Error,
        Message = "Ошибка подсистемы помощника.")]
    public static partial void AiOperationFailed(ILogger logger, Exception exception);
}
