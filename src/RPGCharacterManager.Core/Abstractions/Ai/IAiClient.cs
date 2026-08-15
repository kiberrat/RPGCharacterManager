using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Ai;

/// <summary>
/// Сведения о службе языковых моделей, показываемые пользователю в настройках.
/// </summary>
/// <param name="Title">Название службы.</param>
/// <param name="KeyPage">Адрес страницы, на которой выдаётся ключ.</param>
/// <param name="FreeModelsOnly">Приложение предлагает только бесплатные модели службы.</param>
/// <param name="Notice">Что стоит знать об этой службе.</param>
public sealed record AiServiceInfo(
    string Title,
    string KeyPage,
    bool FreeModelsOnly,
    string Notice);

/// <summary>
/// Клиент службы языковой модели.
///
/// Интерфейс намеренно не знает ни о каком поставщике: приложение обращается к
/// нему одинаково, поэтому замена службы или переход на локальную модель
/// (документ 024_AI_Помощник.md) не затрагивает ни помощника, ни интерфейс.
/// </summary>
public interface IAiClient
{
    /// <summary>Ключ доступа задан и обращение к службе возможно.</summary>
    bool IsConfigured { get; }

    /// <summary>Выбранная пользователем модель.</summary>
    string Model { get; }

    /// <summary>Сведения о выбранной службе.</summary>
    AiServiceInfo Service { get; }

    /// <summary>
    /// Модели, рекомендуемые поставщиком службы.
    /// Показываются в настройках, пока список моделей не получен по сети.
    /// </summary>
    IReadOnlyList<string> RecommendedModels { get; }

    /// <summary>
    /// Отправляет запрос модели и возвращает её ответ.
    /// </summary>
    /// <param name="request">Запрос с перепиской и доступными инструментами.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ модели либо описание ошибки.</returns>
    Task<Result<AiReply>> CompleteAsync(AiRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет связь со службой: доступность ключа, выбранной модели и ответа на
    /// простой вопрос. Результат показывается пользователю как подтверждение работы.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сведения о связи либо описание ошибки.</returns>
    Task<Result<AiConnection>> CheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает список моделей, доступных по заданному ключу.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Имена моделей либо описание ошибки.</returns>
    Task<Result<IReadOnlyList<string>>> GetModelsAsync(CancellationToken cancellationToken = default);
}
