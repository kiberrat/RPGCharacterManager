using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.UI.Logging;

/// <summary>
/// Сообщения журнала слоя представления.
/// Методы генерируются исходным генератором <see cref="LoggerMessageAttribute"/>.
/// </summary>
internal static partial class UiLog
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Debug, Message = "Открыт документ «{DocumentId}».")]
    public static partial void DocumentOpened(ILogger logger, string documentId);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Debug, Message = "Закрыт документ «{DocumentId}».")]
    public static partial void DocumentClosed(ILogger logger, string documentId);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Error, Message = "Не удалось открыть документ «{DocumentId}».")]
    public static partial void DocumentOpenFailed(ILogger logger, Exception exception, string documentId);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Error, Message = "Не удалось открыть каталог «{Path}».")]
    public static partial void FolderOpenFailed(ILogger logger, Exception exception, string path);

    [LoggerMessage(EventId = 3005, Level = LogLevel.Information, Message = "Оформление применено: тема {Theme}, акцент {Accent}.")]
    public static partial void ThemeApplied(ILogger logger, string theme, string accent);

    [LoggerMessage(EventId = 3006, Level = LogLevel.Error, Message = "Не удалось сохранить настройки из окна настроек.")]
    public static partial void SettingsUpdateFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3007, Level = LogLevel.Error, Message = "Сбой раздела помощника.")]
    public static partial void AiSectionFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3008, Level = LogLevel.Error, Message = "Не удалось прочитать горячие клавиши макросов.")]
    public static partial void MacroShortcutsFailed(ILogger logger, Exception exception);
}
