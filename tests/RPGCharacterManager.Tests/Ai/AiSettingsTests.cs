using RPGCharacterManager.Core.Models.Settings;

namespace RPGCharacterManager.Tests.Ai;

/// <summary>
/// Проверка настроек помощника: у каждой службы свои ключ и модель.
/// </summary>
public sealed class AiSettingsTests
{
    [Fact]
    public void Ключ_УКаждойСлужбыСвой()
    {
        var settings = new AppSettings();

        settings.SetAiKey("ключ-groq");

        settings.AiProvider = AiProvider.OpenRouter;
        settings.SetAiKey("ключ-openrouter");

        Assert.Equal("ключ-openrouter", settings.GetAiKey());

        // Переключение туда и обратно не должно требовать повторного ввода.
        settings.AiProvider = AiProvider.Groq;
        Assert.Equal("ключ-groq", settings.GetAiKey());
    }

    [Fact]
    public void Ключ_ТриСлужбы_НеПеремешиваются()
    {
        var settings = new AppSettings();

        foreach (var provider in Enum.GetValues<AiProvider>())
        {
            settings.AiProvider = provider;
            settings.SetAiKey($"ключ-{provider}");
            settings.SetAiModel($"модель-{provider}");
        }

        foreach (var provider in Enum.GetValues<AiProvider>())
        {
            settings.AiProvider = provider;

            Assert.Equal($"ключ-{provider}", settings.GetAiKey());

            // Модель OpenRouter обязана быть бесплатной, поэтому её проверяем отдельно.
            if (provider != AiProvider.OpenRouter)
            {
                Assert.Equal($"модель-{provider}", settings.GetAiModel());
            }
        }
    }

    [Fact]
    public void Ключ_ПробелыИПустаяСтрока_СчитаютсяОтсутствием()
    {
        var settings = new AppSettings();

        settings.SetAiKey("   ");
        Assert.Null(settings.GetAiKey());

        settings.SetAiKey("  ключ  ");
        Assert.Equal("ключ", settings.GetAiKey());
    }

    [Fact]
    public void Модель_УКаждойСлужбыСвоя()
    {
        var settings = new AppSettings();

        settings.SetAiModel("llama-3.3-70b-versatile");

        settings.AiProvider = AiProvider.OpenRouter;
        settings.SetAiModel("автор/модель:free");

        Assert.Equal("автор/модель:free", settings.GetAiModel());

        settings.AiProvider = AiProvider.Groq;
        Assert.Equal("llama-3.3-70b-versatile", settings.GetAiModel());
    }

    [Fact]
    public void Модель_ПлатнаяВOpenRouter_НеВозвращается()
    {
        // Приложение работает с OpenRouter только на бесплатных моделях, поэтому
        // платная запись, оставшаяся в настройках, не должна списать деньги.
        var settings = new AppSettings { AiProvider = AiProvider.OpenRouter };

        settings.AiOpenRouterModel = "автор/платная";

        Assert.Null(settings.GetAiModel());

        settings.SetAiModel("автор/бесплатная:free");
        Assert.Equal("автор/бесплатная:free", settings.GetAiModel());
    }

    [Fact]
    public void Копия_СодержитНастройкиПомощника()
    {
        var settings = new AppSettings
        {
            AiProvider = AiProvider.OpenRouter,
            AiGroqKey = "ключ-groq",
            AiOpenRouterKey = "ключ-openrouter",
            AiGoogleKey = "ключ-google",
            AiModel = "модель-groq",
            AiOpenRouterModel = "модель:free",
            AiGoogleModel = "gemini-2.5-flash",
            AiStyle = AiStyle.Technical,
        };

        var copy = settings.Clone();

        Assert.Equal(AiProvider.OpenRouter, copy.AiProvider);
        Assert.Equal("ключ-groq", copy.AiGroqKey);
        Assert.Equal("ключ-openrouter", copy.AiOpenRouterKey);
        Assert.Equal("ключ-google", copy.AiGoogleKey);
        Assert.Equal("модель-groq", copy.AiModel);
        Assert.Equal("модель:free", copy.AiOpenRouterModel);
        Assert.Equal("gemini-2.5-flash", copy.AiGoogleModel);
        Assert.Equal(AiStyle.Technical, copy.AiStyle);
    }

    [Fact]
    public void Бесплатность_ОпределяетсяПоОкончаниюИмени()
    {
        Assert.True(AiProviders.IsFreeModel("автор/модель:free"));
        Assert.True(AiProviders.IsFreeModel("автор/модель:FREE"));
        Assert.False(AiProviders.IsFreeModel("автор/модель"));
        Assert.False(AiProviders.IsFreeModel(null));

        Assert.True(AiProviders.RequiresFreeModels(AiProvider.OpenRouter));
        Assert.False(AiProviders.RequiresFreeModels(AiProvider.Groq));
    }
}
