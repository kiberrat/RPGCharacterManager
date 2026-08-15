using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Shared;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Shell;

/// <summary>
/// Модель представления строки состояния главного окна.
///
/// Согласно документу 003_UI_UX.md строка состояния показывает текущего персонажа,
/// игровую систему, версию проекта, состояние сохранения, фоновые задачи и объём
/// используемой памяти.
/// </summary>
public sealed partial class StatusBarViewModel : ViewModelBase, IDisposable
{
    /// <summary>Периодичность обновления сведений об использовании памяти.</summary>
    private static readonly TimeSpan MemoryRefreshInterval = TimeSpan.FromSeconds(3);

    private const double BytesInMegabyte = 1024.0 * 1024.0;

    private readonly IApplicationStatusService _status;
    private readonly IBackgroundTaskService _backgroundTasks;
    private readonly DispatcherTimer _memoryTimer;

    [ObservableProperty]
    private string? _characterName;

    [ObservableProperty]
    private string? _gameSystemName;

    [ObservableProperty]
    private string _saveStateText = string.Empty;

    [ObservableProperty]
    private string _backgroundTasksText = string.Empty;

    [ObservableProperty]
    private string _memoryUsageText = string.Empty;

    /// <summary>
    /// Создаёт модель представления строки состояния.
    /// </summary>
    /// <param name="status">Служба сведений о состоянии приложения.</param>
    /// <param name="backgroundTasks">Служба фоновых задач.</param>
    public StatusBarViewModel(IApplicationStatusService status, IBackgroundTaskService backgroundTasks)
    {
        _status = Guard.NotNull(status);
        _backgroundTasks = Guard.NotNull(backgroundTasks);

        _status.PropertyChanged += OnStatusPropertyChanged;

        // ReadOnlyObservableCollection реализует INotifyCollectionChanged явно,
        // поэтому подписка выполняется через приведение к интерфейсу.
        ((INotifyCollectionChanged)_backgroundTasks.RunningTasks).CollectionChanged += OnBackgroundTasksChanged;

        _memoryTimer = new DispatcherTimer { Interval = MemoryRefreshInterval };
        _memoryTimer.Tick += (_, _) => RefreshMemoryUsage();
        _memoryTimer.Start();

        RefreshStatus();
        RefreshBackgroundTasks();
        RefreshMemoryUsage();
    }

    /// <summary>Версия приложения, отображаемая в строке состояния.</summary>
    public string VersionText { get; } =
        $"{ApplicationConstants.ApplicationName} {typeof(StatusBarViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";

    /// <inheritdoc />
    public void Dispose()
    {
        _memoryTimer.Stop();
        _status.PropertyChanged -= OnStatusPropertyChanged;
        ((INotifyCollectionChanged)_backgroundTasks.RunningTasks).CollectionChanged -= OnBackgroundTasksChanged;
    }

    private void OnStatusPropertyChanged(object? sender, PropertyChangedEventArgs args) => RefreshStatus();

    private void OnBackgroundTasksChanged(object? sender, NotifyCollectionChangedEventArgs args) =>
        RefreshBackgroundTasks();

    private void RefreshStatus()
    {
        CharacterName = _status.CurrentCharacterName;
        GameSystemName = _status.CurrentGameSystemName;
        SaveStateText = _status.SaveState switch
        {
            SaveState.Modified => "Есть несохранённые изменения",
            SaveState.Saving => "Сохранение…",
            SaveState.Failed => "Ошибка сохранения",
            _ => "Все изменения сохранены",
        };
    }

    private void RefreshBackgroundTasks()
    {
        var count = _backgroundTasks.RunningTasks.Count;
        BackgroundTasksText = count == 0
            ? "Фоновых задач нет"
            : string.Create(CultureInfo.CurrentCulture, $"Выполняется задач: {count}");
    }

    private void RefreshMemoryUsage()
    {
        var megabytes = GC.GetTotalMemory(forceFullCollection: false) / BytesInMegabyte;
        MemoryUsageText = string.Create(CultureInfo.CurrentCulture, $"Память: {megabytes:F1} МБ");
    }
}
