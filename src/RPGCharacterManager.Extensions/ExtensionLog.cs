using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.Extensions;

/// <summary>
/// Сообщения журнала подсистемы расширений.
/// </summary>
internal static partial class ExtensionLog
{
    [LoggerMessage(
        EventId = 18001,
        Level = LogLevel.Information,
        Message = "Установлено расширение «{Name}» {Version}: объектов {Count}.")]
    public static partial void Installed(ILogger logger, string name, string version, int count);

    [LoggerMessage(
        EventId = 18002,
        Level = LogLevel.Information,
        Message = "Удалено расширение «{Name}»: объектов {Count}.")]
    public static partial void Removed(ILogger logger, string name, int count);

    [LoggerMessage(
        EventId = 18003,
        Level = LogLevel.Information,
        Message = "Выгружено расширение «{Name}»: объектов {Count}, файл {Path}.")]
    public static partial void Exported(ILogger logger, string name, int count, string path);

    [LoggerMessage(
        EventId = 18004,
        Level = LogLevel.Error,
        Message = "Не удалось выполнить действие с расширением.")]
    public static partial void OperationFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 18005,
        Level = LogLevel.Warning,
        Message = "Установка расширения «{Name}» прервана, изменения отменены.")]
    public static partial void InstallRolledBack(ILogger logger, Exception exception, string name);
}
