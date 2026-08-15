using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Core.Models.Settings;

namespace RPGCharacterManager.Ai;

/// <summary>
/// Особенности одной службы языковых моделей.
///
/// Обе поддерживаемые службы отвечают в формате, совместимом с описанием OpenAI,
/// поэтому различаются они только адресом, составом моделей и страницей, где
/// пользователь получает ключ. Всё остальное — общий код.
/// </summary>
internal sealed class AiProviderProfile
{
    private static readonly AiProviderProfile GroqProfile = new(
        AiProvider.Groq,
        new Uri("https://api.groq.com/openai/v1/"),
        "llama-3.3-70b-versatile",
        [
            "llama-3.3-70b-versatile",
            "llama-3.1-8b-instant",
            "openai/gpt-oss-120b",
            "openai/gpt-oss-20b",
            "moonshotai/kimi-k2-instruct",
            "qwen/qwen3-32b",
            "qwen/qwen3.6-27b",
        ],
        new AiServiceInfo(
            "Groq",
            "console.groq.com",
            FreeModelsOnly: false,
            "Бесплатный уровень ограничивает число обращений в минуту и в день. " +
            "Если служба отвечает отказом, подождите или выберите модель полегче."));

    private static readonly AiProviderProfile OpenRouterProfile = new(
        AiProvider.OpenRouter,
        new Uri("https://openrouter.ai/api/v1/"),
        "meta-llama/llama-3.3-70b-instruct:free",
        [
            "meta-llama/llama-3.3-70b-instruct:free",
            "deepseek/deepseek-chat-v3-0324:free",
        ],
        new AiServiceInfo(
            "OpenRouter",
            "openrouter.ai/keys",
            FreeModelsOnly: true,
            "Приложение показывает только бесплатные модели, умеющие вызывать " +
            "инструменты: платные не предлагаются, а без инструментов помощник " +
            "не сможет ничего ни прочитать, ни предложить."));

    private static readonly AiProviderProfile GoogleProfile = new(
        AiProvider.GoogleAi,
        new Uri("https://generativelanguage.googleapis.com/v1beta/openai/"),
        // Google закрывает старые модели для новых пользователей, поэтому
        // встроенный перечень начинается с самой свежей. Настоящий состав
        // приложение всё равно запрашивает у службы.
        "gemini-3.6-flash",
        [
            "gemini-3.6-flash",
            "gemini-2.5-flash",
            "gemini-2.5-flash-lite",
            "gemini-2.5-pro",
        ],
        new AiServiceInfo(
            "Google AI Studio",
            "aistudio.google.com/apikey",
            FreeModelsOnly: false,
            "Модели Gemini. Бесплатный уровень ограничивает число обращений в минуту " +
            "и в день; при отказе выберите модель полегче — например, с окончанием flash."));

    private AiProviderProfile(
        AiProvider provider,
        Uri address,
        string defaultModel,
        IReadOnlyList<string> recommendedModels,
        AiServiceInfo info)
    {
        Provider = provider;
        Address = address;
        DefaultModel = defaultModel;
        RecommendedModels = recommendedModels;
        Info = info;
    }

    /// <summary>Служба, которую описывает профиль.</summary>
    public AiProvider Provider { get; }

    /// <summary>Адрес службы.</summary>
    public Uri Address { get; }

    /// <summary>Модель, выбираемая по умолчанию.</summary>
    public string DefaultModel { get; }

    /// <summary>
    /// Модели, показываемые до получения списка от службы.
    ///
    /// Перечень неизбежно устаревает: службы добавляют и убирают модели, не
    /// спрашивая приложение. Поэтому он служит лишь первым приближением,
    /// а настоящий состав приложение запрашивает у службы само.
    /// </summary>
    public IReadOnlyList<string> RecommendedModels { get; }

    /// <summary>Сведения о службе, показываемые пользователю.</summary>
    public AiServiceInfo Info { get; }

    /// <summary>
    /// Возвращает профиль службы.
    /// </summary>
    /// <param name="provider">Служба.</param>
    /// <returns>Профиль службы.</returns>
    public static AiProviderProfile Of(AiProvider provider) => provider switch
    {
        AiProvider.OpenRouter => OpenRouterProfile,
        AiProvider.GoogleAi => GoogleProfile,
        _ => GroqProfile,
    };
}
