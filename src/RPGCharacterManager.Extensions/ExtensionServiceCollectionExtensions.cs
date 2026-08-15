using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.Extensions;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Extensions;

/// <summary>
/// Регистрация расширений в контейнере зависимостей.
/// </summary>
public static class ExtensionServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует службу расширений.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddExtensions(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<IExtensionService, ExtensionService>();

        return services;
    }
}
