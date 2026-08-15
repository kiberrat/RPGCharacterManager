using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Ai.Tools;
using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Ai;

/// <summary>
/// Регистрация подсистемы помощника в контейнере зависимостей.
/// </summary>
public static class AiServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует помощника, клиент службы языковой модели и его инструменты.
    ///
    /// Инструменты регистрируются перечнем: помощник получает их все сразу,
    /// поэтому новый инструмент подключается одной строкой и сразу становится
    /// доступен модели.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddAi(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<IAiClient, AiChatClient>();
        services.TryAddSingleton<IAiLibrary, AiLibrary>();
        services.TryAddSingleton<IAiAssistant, AiAssistant>();

        services.AddSingleton<IAiTool, ListTypesTool>();
        services.AddSingleton<IAiTool, DescribeTypeTool>();
        services.AddSingleton<IAiTool, FindObjectsTool>();
        services.AddSingleton<IAiTool, ReadObjectTool>();
        services.AddSingleton<IAiTool, CreateObjectTool>();
        services.AddSingleton<IAiTool, CopyObjectTool>();
        services.AddSingleton<IAiTool, UpdateObjectTool>();
        services.AddSingleton<IAiTool, AddListItemTool>();
        services.AddSingleton<IAiTool, ListCharactersTool>();
        services.AddSingleton<IAiTool, ReadCharacterTool>();
        services.AddSingleton<IAiTool, CheckFormulaTool>();
        services.AddSingleton<IAiTool, CheckDatabaseTool>();

        return services;
    }
}
