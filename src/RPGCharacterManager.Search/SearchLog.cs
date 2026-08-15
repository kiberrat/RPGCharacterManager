using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.Search;

/// <summary>
/// Сообщения журнала подсистемы поиска.
/// </summary>
internal static partial class SearchLog
{
    [LoggerMessage(
        EventId = 15001,
        Level = LogLevel.Information,
        Message = "Поиск «{Query}»: найдено {Count}.")]
    public static partial void SearchCompleted(ILogger logger, string query, int count);

    [LoggerMessage(
        EventId = 15002,
        Level = LogLevel.Error,
        Message = "Поставщик находок {Provider} завершился ошибкой.")]
    public static partial void ProviderFailed(ILogger logger, Exception exception, string provider);
}
