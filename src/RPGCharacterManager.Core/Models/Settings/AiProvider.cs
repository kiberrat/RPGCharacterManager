namespace RPGCharacterManager.Core.Models.Settings;

/// <summary>
/// Служба языковых моделей, к которой обращается помощник.
///
/// Обе службы отвечают в одном формате, поэтому выбор меняет только адрес,
/// ключ доступа и состав доступных моделей — ни помощник, ни его инструменты
/// о поставщике не знают.
/// </summary>
public enum AiProvider
{
    /// <summary>Groq: быстрые модели, бесплатный уровень с ограничением частоты обращений.</summary>
    Groq = 0,

    /// <summary>OpenRouter: приложение показывает только бесплатные модели.</summary>
    OpenRouter = 1,

    /// <summary>Google AI Studio: модели Gemini через вход, совместимый с OpenAI.</summary>
    GoogleAi = 2,
}

/// <summary>
/// Общие сведения о службах языковых моделей.
/// </summary>
public static class AiProviders
{
    /// <summary>Окончание имени бесплатной модели OpenRouter.</summary>
    public const string FreeSuffix = ":free";

    /// <summary>
    /// Определяет, ограничивает ли приложение выбор бесплатными моделями.
    ///
    /// У OpenRouter платные модели списывают деньги с личного счёта пользователя,
    /// и приложение их не предлагает: раздел «AI» — вспомогательный, а не повод
    /// потратить деньги, о которых не предупредили.
    /// </summary>
    /// <param name="provider">Служба.</param>
    /// <returns><see langword="true"/>, если допустимы только бесплатные модели.</returns>
    public static bool RequiresFreeModels(AiProvider provider) => provider == AiProvider.OpenRouter;

    /// <summary>
    /// Определяет, бесплатна ли модель по её имени.
    /// </summary>
    /// <param name="model">Имя модели.</param>
    /// <returns><see langword="true"/>, если имя помечено как бесплатное.</returns>
    public static bool IsFreeModel(string? model) =>
        model is not null && model.EndsWith(FreeSuffix, StringComparison.OrdinalIgnoreCase);
}
