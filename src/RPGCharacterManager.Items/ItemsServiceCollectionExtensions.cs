using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.Items;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Items;

/// <summary>
/// Регистрация подсистемы предметов в контейнере зависимостей.
/// </summary>
public static class ItemsServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует подсистему предметов: оружие, экипировку и инвентарь персонажа.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddItems(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<IWeaponService, WeaponService>();
        services.TryAddSingleton<IEquipmentService, EquipmentService>();
        services.TryAddSingleton<IInventoryService, InventoryService>();

        return services;
    }
}
