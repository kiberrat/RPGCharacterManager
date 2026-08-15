using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Engine.Functions;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Engine;

/// <summary>
/// Регистрация движка вычислений в контейнере зависимостей.
/// </summary>
public static class EngineServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует движок формул и встроенные функции.
    ///
    /// Пользовательская функция добавляется отдельной регистрацией
    /// <see cref="IFormulaFunction"/> и становится доступной во всех формулах
    /// без изменения движка.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddFormulaEngine(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<IRandomSource, SystemRandomSource>();

        services.AddSingleton<IFormulaFunction, MinimumFunction>();
        services.AddSingleton<IFormulaFunction, MaximumFunction>();
        services.AddSingleton<IFormulaFunction, SumFunction>();
        services.AddSingleton<IFormulaFunction, AverageFunction>();
        services.AddSingleton<IFormulaFunction, CountFunction>();
        services.AddSingleton<IFormulaFunction, RoundFunction>();
        services.AddSingleton<IFormulaFunction, FloorFunction>();
        services.AddSingleton<IFormulaFunction, CeilingFunction>();
        services.AddSingleton<IFormulaFunction, AbsoluteFunction>();
        services.AddSingleton<IFormulaFunction, ClampFunction>();
        services.AddSingleton<IFormulaFunction, IfFunction>();
        services.AddSingleton<IFormulaFunction, DiceFunction>();
        services.AddSingleton<IFormulaFunction, RandomFunction>();

        services.TryAddSingleton<IFormulaEngine, FormulaEngine>();

        return services;
    }
}
