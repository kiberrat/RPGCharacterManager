using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.Search;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Search;

/// <summary>
/// Регистрация глобального поиска в контейнере зависимостей.
/// </summary>
public static class SearchServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует службу поиска и встроенных поставщиков находок.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddSearch(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<ISearchService, SearchService>();

        services.AddSingleton<ISearchProvider, CampaignSearchProvider>();
        services.AddSingleton<ISearchProvider, CharacterSearchProvider>();
        services.AddSingleton<ISearchProvider, ContentSearchProvider>();
        services.AddSingleton<ISearchProvider, HistorySearchProvider>();

        return services;
    }
}
