using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Distribution;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Infrastructure.Diagnostics;
using RPGCharacterManager.Infrastructure.Events;
using RPGCharacterManager.Infrastructure.Logging;
using RPGCharacterManager.Infrastructure.Distribution;
using RPGCharacterManager.Infrastructure.Settings;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Infrastructure;

/// <summary>
/// Регистрация инфраструктурных служб в контейнере зависимостей.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует службы путей, настроек, шины событий, фоновых задач и обработки ошибок.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <param name="configuration">Конфигурация приложения.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        Guard.NotNull(services);
        Guard.NotNull(configuration);

        services.TryAddSingleton<IAppPathService, AppPathService>();
        services.TryAddSingleton<IEventBus, InMemoryEventBus>();
        services.TryAddSingleton<ISettingsService, JsonSettingsService>();
        services.TryAddSingleton<IBackgroundTaskService, BackgroundTaskService>();
        services.TryAddSingleton<IApplicationStatusService, ApplicationStatusService>();
        services.TryAddSingleton<GlobalExceptionHandler>();
        services.TryAddSingleton<IApplicationUpdateService, VelopackApplicationUpdateService>();
        services.TryAddSingleton<IFeedbackService, HttpFeedbackService>();

        services.AddOptions<DistributionOptions>()
            .Bind(configuration.GetSection(DistributionOptions.SectionName));

        services.AddOptions<FileLoggerOptions>()
            .Bind(configuration.GetSection(FileLoggerOptions.SectionName))
            .PostConfigure<IAppPathService>((options, paths) =>
            {
                // Каталог журналов всегда определяется службой путей: конфигурация
                // задаёт только уровень подробности и правила хранения файлов.
                options.Directory = paths.LogsDirectory;
            });

        return services;
    }

    /// <summary>
    /// Подключает журналирование в файл к системе журналирования приложения.
    /// </summary>
    /// <param name="builder">Построитель системы журналирования.</param>
    /// <returns>Тот же построитель для построения цепочки вызовов.</returns>
    public static ILoggingBuilder AddFileLogging(this ILoggingBuilder builder)
    {
        Guard.NotNull(builder);

        builder.Services.TryAddSingleton<FileLogSink>();
        builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();

        return builder;
    }
}
