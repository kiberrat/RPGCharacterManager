using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.Macros;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Macros;

/// <summary>
/// Регистрация подсистемы макросов в контейнере зависимостей.
/// </summary>
public static class MacroServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует службу макросов.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddMacros(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<IMacroService, MacroService>();

        return services;
    }
}
