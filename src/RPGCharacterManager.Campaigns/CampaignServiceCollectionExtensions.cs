using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.Campaigns;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Campaigns;

/// <summary>
/// Регистрация подсистемы кампаний в контейнере зависимостей.
/// </summary>
public static class CampaignServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует менеджер кампаний и каталог доступных ему объектов.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddCampaigns(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<ICampaignCatalog, CampaignCatalog>();
        services.TryAddSingleton<ICampaignService, CampaignService>();

        return services;
    }
}
