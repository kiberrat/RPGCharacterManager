using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.History;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.History;

/// <summary>
/// Регистрация подсистемы журнала событий в контейнере зависимостей.
/// </summary>
public static class HistoryServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует журнал событий.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddHistory(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<IHistoryService, HistoryService>();

        return services;
    }
}
