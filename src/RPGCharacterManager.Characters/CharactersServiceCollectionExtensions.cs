using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Characters;

/// <summary>
/// Регистрация подсистемы персонажей в контейнере зависимостей.
/// </summary>
public static class CharactersServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует мастер создания персонажа, расчёт параметров и развитие персонажа.
    ///
    /// Новый шаг мастера подключается регистрацией собственного
    /// <see cref="ICharacterStepProvider"/> и появляется в мастере без изменения кода.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddCharacters(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.AddSingleton<ICharacterStepProvider, StandardCharacterStepProvider>();

        services.TryAddSingleton<ICharacterCalculator, CharacterCalculator>();
        services.TryAddSingleton<ICharacterBuilderService, CharacterBuilderService>();
        services.TryAddSingleton<ICharacterService, CharacterService>();
        services.TryAddSingleton<ICharacterProgressionService, CharacterProgressionService>();
        services.TryAddSingleton<ICharacterSheetService, CharacterSheetService>();
        services.TryAddSingleton<ISpellbookService, SpellbookService>();
        services.TryAddSingleton<IEffectService, EffectService>();
        services.TryAddSingleton<IRestService, RestService>();

        return services;
    }
}
