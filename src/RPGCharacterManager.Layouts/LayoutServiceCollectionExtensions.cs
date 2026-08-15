using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.Layouts;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Layouts;

/// <summary>
/// Регистрация подсистемы макетов в контейнере зависимостей.
/// </summary>
public static class LayoutServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует службу макетов интерфейса.
    ///
    /// Каталог панелей регистрирует слой интерфейса: панели объявляет он,
    /// а подсистема макетов знает только их ключи.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddLayouts(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<ILayoutService, LayoutService>();

        return services;
    }
}
