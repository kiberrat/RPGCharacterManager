using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Data;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Infrastructure.Diagnostics;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.ViewModels;
using RPGCharacterManager.UI.Views;

namespace RPGCharacterManager.App;

/// <summary>
/// Класс приложения Avalonia.
///
/// Отвечает только за подключение готовых служб к жизненному циклу интерфейса.
/// Бизнес-логика и построение контейнера зависимостей находятся вне этого класса.
/// </summary>
public sealed class App : Application
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// Создаёт приложение с уже построенным контейнером зависимостей.
    /// </summary>
    /// <param name="services">Поставщик служб приложения.</param>
    public App(IServiceProvider services) => _services = Guard.NotNull(services);

    /// <summary>
    /// Создаёт приложение без контейнера. Конструктор используется средствами
    /// предварительного просмотра разметки, которые создают экземпляр без аргументов.
    /// </summary>
    public App() => _services = new ServiceCollection().BuildServiceProvider();

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ConfigureDesktopLifetime(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureDesktopLifetime(IClassicDesktopStyleApplicationLifetime desktop)
    {
        // Оформление применяется до создания окна, чтобы исключить видимое
        // переключение темы после запуска.
        var settings = _services.GetRequiredService<ISettingsService>();
        _services.GetRequiredService<IThemeService>().Apply(settings.Current);

        var viewModel = _services.GetRequiredService<MainWindowViewModel>();
        desktop.MainWindow = new MainWindow { DataContext = viewModel };
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        desktop.Exit += OnExit;

        // Подготовка хранилища и открытие первого документа выполняются после
        // показа окна, чтобы запуск приложения оставался быстрым.
        desktop.MainWindow.Opened += async (_, _) => await StartApplicationAsync(viewModel).ConfigureAwait(true);
    }

    private async Task StartApplicationAsync(MainWindowViewModel viewModel)
    {
        var database = _services.GetRequiredService<IDatabaseService>();
        var notifications = _services.GetRequiredService<INotificationService>();
        var backgroundTasks = _services.GetRequiredService<IBackgroundTaskService>();

        var result = await backgroundTasks
            .RunAsync("Подготовка базы данных", database.InitializeAsync)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            notifications.Show(result.Error ?? "База данных недоступна.", NotificationKind.Error);
        }

        await viewModel.InitializeAsync().ConfigureAwait(true);
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs args)
    {
        _services.GetService<GlobalExceptionHandler>()?.Dispose();
        (_services.GetRequiredService<MainWindowViewModel>() as IDisposable)?.Dispose();

        _services.GetService<ILoggerFactory>()?.Dispose();
    }
}
