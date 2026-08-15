using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.GameRules.Actions;
using RPGCharacterManager.GameRules.Triggers;
using RPGCharacterManager.GameRules.Validation;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.GameRules;

/// <summary>
/// Регистрация подсистемы игровых правил в контейнере зависимостей.
/// </summary>
public static class GameRulesServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует движок правил, встроенные обработчики действий,
    /// перечень событий, проверку правил и их хранение.
    ///
    /// Новый вид действия или новое событие добавляется отдельной регистрацией
    /// <see cref="IRuleActionHandler"/> либо <see cref="IRuleTriggerProvider"/>
    /// и появляется в редакторе правил без изменения этого класса.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddGameRules(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.AddSingleton<IRuleActionHandler, SetValueActionHandler>();
        services.AddSingleton<IRuleActionHandler, AdjustValueActionHandler>();
        services.AddSingleton<IRuleActionHandler, AddTagActionHandler>();
        services.AddSingleton<IRuleActionHandler, RemoveTagActionHandler>();
        services.AddSingleton<IRuleActionHandler, SpendResourceActionHandler>();
        services.AddSingleton<IRuleActionHandler, RestoreResourceActionHandler>();
        services.AddSingleton<IRuleActionHandler, RollActionHandler>();

        services.AddSingleton<IRuleTriggerProvider, StandardTriggerProvider>();

        services.TryAddSingleton<IRuleTriggerCatalog, RuleTriggerCatalog>();
        services.TryAddSingleton<IRuleEngine, RuleEngine>();
        services.TryAddSingleton<IRuleValidator, RuleValidator>();
        services.TryAddSingleton<IRuleService, RuleService>();

        return services;
    }
}
