namespace RPGCharacterManager.Infrastructure.Distribution;

/// <summary>Параметры доставки обновлений и обратной связи.</summary>
public sealed class DistributionOptions
{
    /// <summary>Имя раздела конфигурации.</summary>
    public const string SectionName = "Distribution";

    /// <summary>URL репозитория GitHub либо каталог/URL ленты Velopack.</summary>
    public string? UpdateSource { get; set; }

    /// <summary>Адрес серверного обработчика обратной связи.</summary>
    public string? FeedbackEndpoint { get; set; }
}