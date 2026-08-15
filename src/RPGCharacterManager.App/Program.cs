using Avalonia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Ai;
using RPGCharacterManager.Campaigns;
using RPGCharacterManager.Layouts;
using RPGCharacterManager.Macros;
using RPGCharacterManager.Statistics;
using RPGCharacterManager.Extensions;
using RPGCharacterManager.Master;
using RPGCharacterManager.Search;
using RPGCharacterManager.Characters;
using RPGCharacterManager.Content;
using RPGCharacterManager.Database;
using RPGCharacterManager.Dice;
using RPGCharacterManager.Engine;
using RPGCharacterManager.GameRules;
using RPGCharacterManager.History;
using RPGCharacterManager.Import;
using RPGCharacterManager.Infrastructure;
using RPGCharacterManager.Infrastructure.Diagnostics;
using RPGCharacterManager.Items;
using RPGCharacterManager.Shared;
using RPGCharacterManager.UI;
using Velopack;

namespace RPGCharacterManager.App;

/// <summary>
/// Точка входа приложения.
/// </summary>
public static class Program
{
    private const int ExitCodeSuccess = 0;
    private const int ExitCodeStartupFailure = 1;

    /// <summary>
    /// Запускает приложение.
    /// </summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <returns>Код завершения процесса.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        // Обработчики установки и обновления должны выполниться раньше Avalonia и DI.
        VelopackApp.Build().Run();

        IHost? host = null;

        try
        {
            host = BuildHost(args);
            PrepareEnvironment(host.Services);

            return BuildAvaloniaApp(host.Services).StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            WriteStartupFailure(exception);
            return ExitCodeStartupFailure;
        }
        finally
        {
            host?.Dispose();
        }
    }

    /// <summary>
    /// Создаёт построитель приложения Avalonia без контейнера зависимостей.
    /// Метод требуется средствам предварительного просмотра разметки и конструктору Avalonia.
    /// </summary>
    /// <returns>Построитель приложения.</returns>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();

    /// <summary>
    /// Создаёт построитель приложения Avalonia с указанным контейнером зависимостей.
    /// </summary>
    /// <param name="services">Поставщик служб приложения.</param>
    /// <returns>Построитель приложения.</returns>
    private static AppBuilder BuildAvaloniaApp(IServiceProvider services) => AppBuilder
        .Configure(() => new App(services))
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();

    /// <summary>
    /// Строит узел приложения: конфигурацию, журналирование и контейнер зависимостей.
    /// </summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <returns>Построенный узел приложения.</returns>
    private static IHost BuildHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "RPGCM_")
            .AddCommandLine(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Logging.AddFileLogging();
#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddDatabase(builder.Configuration);
        builder.Services.AddFormulaEngine();
        builder.Services.AddGameRules();
        builder.Services.AddContent();
        builder.Services.AddCharacters();
        builder.Services.AddItems();
        builder.Services.AddDice();
        builder.Services.AddHistory();
        builder.Services.AddCampaigns();
        builder.Services.AddMasterMode();
        builder.Services.AddLayouts();
        builder.Services.AddSearch();
        builder.Services.AddMacros();
        builder.Services.AddStatistics();
        builder.Services.AddExtensions();
        builder.Services.AddImport();
        builder.Services.AddAi();
        builder.Services.AddUserInterface();

        return builder.Build();
    }

    /// <summary>
    /// Выполняет подготовку среды до создания окна: каталоги данных,
    /// централизованная обработка ошибок и загрузка пользовательских настроек.
    /// </summary>
    /// <param name="services">Поставщик служб приложения.</param>
    private static void PrepareEnvironment(IServiceProvider services)
    {
        services.GetRequiredService<IAppPathService>().EnsureDirectoriesExist();
        services.GetRequiredService<GlobalExceptionHandler>().Attach();

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Program).FullName!);
        StartupLog.ApplicationStarting(logger, ApplicationConstants.ApplicationName);

        // Настройки требуются до создания окна, поэтому загрузка выполняется
        // синхронно. Это единственная блокирующая операция этапа запуска.
        services.GetRequiredService<ISettingsService>().LoadAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Сообщает о сбое, произошедшем до инициализации системы журналирования.
    /// </summary>
    /// <param name="exception">Возникшее исключение.</param>
    private static void WriteStartupFailure(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ApplicationConstants.DataFolderName,
                ApplicationConstants.LogsFolderName);

            Directory.CreateDirectory(directory);

            File.AppendAllText(
                Path.Combine(directory, "startup-failure.log"),
                $"{DateTimeOffset.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception writeException) when (writeException is IOException or UnauthorizedAccessException)
        {
            // Записать сведения о сбое не удалось: дальнейшие действия невозможны.
        }
    }
}

/// <summary>
/// Сообщения журнала этапа запуска приложения.
/// </summary>
internal static partial class StartupLog
{
    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "Запуск приложения {ApplicationName}.")]
    public static partial void ApplicationStarting(ILogger logger, string applicationName);
}
