using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.Master;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Master;

/// <summary>
/// Регистрация режима мастера в контейнере зависимостей.
/// </summary>
public static class MasterServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует службу режима мастера.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddMasterMode(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<IMasterService, MasterService>();

        return services;
    }
}
