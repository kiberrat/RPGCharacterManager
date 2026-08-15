using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.Statistics;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Statistics;

/// <summary>
/// Регистрация статистики в контейнере зависимостей.
/// </summary>
public static class StatisticsServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует службу статистики.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddStatistics(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<IStatisticsService, StatisticsService>();

        return services;
    }
}
