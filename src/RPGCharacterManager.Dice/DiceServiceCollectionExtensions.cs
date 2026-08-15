using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.Dice;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Dice;

/// <summary>
/// Регистрация подсистемы бросков в контейнере зависимостей.
/// </summary>
public static class DiceServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует подсистему бросков кубиков.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddDice(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<IDiceService, DiceService>();

        return services;
    }
}
