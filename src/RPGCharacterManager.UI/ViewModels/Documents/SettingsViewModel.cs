using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Core.Abstractions.Data;
using RPGCharacterManager.Core.Abstractions.Distribution;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.Settings;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Logging;
using RPGCharacterManager.UI.Shell;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Документ «Настройки»: редактирование пользовательских параметров приложения.
///
/// Изменение любого параметра сразу сохраняется и применяется — отдельная кнопка
/// сохранения не требуется, что соответствует требованию автоматического сохранения
/// из документа STYLE_GUIDE.
/// </summary>
public sealed partial class SettingsViewModel : DocumentViewModelBase, IDisposable
{
    private readonly ISettingsService _settings;
    private readonly INotificationService _notifications;
    private readonly IAppPathService _paths;
    private readonly IAiClient _ai;
    private readonly IApplicationUpdateService _updates;
    private readonly IBackupService _backups;
    private readonly IDialogService _dialogs;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IDisposable _settingsSubscription;

    /// <summary>
    /// Признак выполнения программного обновления свойств.
    /// Не даёт загрузке значений из настроек повторно запустить их сохранение.
    /// </summary>
    private bool _isApplyingExternalUpdate;

    /// <summary>Служба, для которой собран текущий список моделей.</summary>
    private AiProvider? _modelsProvider;

    [ObservableProperty]
    private ThemeMode _theme;

    [ObservableProperty]
    private AccentColor _accent;

    [ObservableProperty]
    private double _fontSize;

    [ObservableProperty]
    private double _interfaceScale;

    [ObservableProperty]
    private int _backupIntervalHours;

    [ObservableProperty]
    private int _backupRetentionDays;

    [ObservableProperty]
    private int _diceHistoryLimit;

    [ObservableProperty]
    private bool _diceAnimationEnabled;

    [ObservableProperty]
    private AiProvider _aiProvider;

    [ObservableProperty]
    private string _aiApiKey = string.Empty;

    [ObservableProperty]
    private string _aiModel = string.Empty;

    [ObservableProperty]
    private AiStyle _aiStyle;

    [ObservableProperty]
    private string _aiCheckText = string.Empty;

    [ObservableProperty]
    private string _updateStatusText = string.Empty;

    [ObservableProperty]
    private string _updateReleaseNotes = string.Empty;

    [ObservableProperty]
    private int _updateProgress;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private bool _isUpdateDownloaded;

    [ObservableProperty]
    private bool _isUpdating;

    /// <summary>
    /// Создаёт модель представления документа настроек.
    /// </summary>
    /// <param name="settings">Служба пользовательских настроек.</param>
    /// <param name="notifications">Служба всплывающих уведомлений.</param>
    /// <param name="paths">Служба путей пользовательских данных.</param>
    /// <param name="ai">Клиент службы языковой модели.</param>
    /// <param name="updates">Служба обновления приложения.</param>
    /// <param name="backups">Служба резервного копирования.</param>
    /// <param name="dialogs">Служба диалогов.</param>
    /// <param name="eventBus">Шина событий.</param>
    /// <param name="dispatcher">Диспетчер потока пользовательского интерфейса.</param>
    /// <param name="logger">Журналировщик.</param>
    public SettingsViewModel(
        ISettingsService settings,
        INotificationService notifications,
        IAppPathService paths,
        IAiClient ai,
        IApplicationUpdateService updates,
        IBackupService backups,
        IDialogService dialogs,
        IEventBus eventBus,
        IUiDispatcher dispatcher,
        ILogger<SettingsViewModel> logger)
        : base(CoreShellContributor.SettingsDocumentId, "Настройки")
    {
        _settings = Guard.NotNull(settings);
        _notifications = Guard.NotNull(notifications);
        _paths = Guard.NotNull(paths);
        _ai = Guard.NotNull(ai);
        _updates = Guard.NotNull(updates);
        _backups = Guard.NotNull(backups);
        _dialogs = Guard.NotNull(dialogs);
        _logger = Guard.NotNull(logger);

        Guard.NotNull(eventBus);
        Guard.NotNull(dispatcher);

        AiModels = [];

        // Настройки могут измениться и вне этого документа — например,
        // сочетанием клавиш изменения масштаба. Страница обязана оставаться
        // согласованной с фактическими значениями.
        _settingsSubscription = eventBus.SubscribeOnUiThread<SettingsChangedEvent>(
            dispatcher,
            _ => LoadFromSettings());

        LoadFromSettings();
        UpdateStatusText = GetInitialUpdateStatus();
    }

    /// <inheritdoc />
    public void Dispose() => _settingsSubscription.Dispose();

    /// <summary>
    /// Запрашивает у службы состав её моделей при открытии настроек.
    ///
    /// Встроенный перечень моделей устаревает, а состав службы меняется без
    /// участия приложения. Требовать от пользователя нажать «Обновить список»
    /// значит показывать ему неполный выбор до тех пор, пока он не догадается
    /// это сделать, — поэтому список запрашивается сам, как только есть ключ.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся сразу: список читается в стороне.</returns>
    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_ai.IsConfigured)
        {
            // Ответа службы раздел не ждёт: оболочка показывает документ только
            // после его подготовки, и медленная служба задержала бы открытие
            // настроек целиком, а недоступная — не дала бы открыть их вовсе.
            // Список моделей появится, когда придёт; до тех пор показан встроенный.
            _ = LoadAiModelsAsync(cancellationToken);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Читает список моделей, не задерживая открытие раздела.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после чтения.</returns>
    private async Task LoadAiModelsAsync(CancellationToken cancellationToken)
    {
        await RefreshAiModelsAsync(cancellationToken).ConfigureAwait(true);

        // Отчёт о числе моделей нужен только тогда, когда пользователь сам
        // нажал «Обновить список»: при открытии настроек это шум.
        AiCheckText = string.Empty;
    }

    /// <summary>Доступные режимы оформления.</summary>
    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(ThemeMode.Dark, "Тёмная"),
        new(ThemeMode.Light, "Светлая"),
        new(ThemeMode.System, "Как в Windows"),
    ];

    /// <summary>Доступные акцентные цвета.</summary>
    public IReadOnlyList<AccentOption> AccentOptions { get; } =
    [
        new(AccentColor.Blue, "Синий"),
        new(AccentColor.Red, "Красный"),
        new(AccentColor.Green, "Зелёный"),
        new(AccentColor.Purple, "Фиолетовый"),
        new(AccentColor.Orange, "Оранжевый"),
    ];

    /// <summary>Доступные службы языковых моделей.</summary>
    public IReadOnlyList<AiProviderOption> AiProviderOptions { get; } =
    [
        new(AiProvider.Groq, "Groq"),
        new(AiProvider.OpenRouter, "OpenRouter (бесплатные модели)"),
        new(AiProvider.GoogleAi, "Google AI Studio (Gemini)"),
    ];

    /// <summary>Название выбранной службы.</summary>
    public string AiServiceTitle => _ai.Service.Title;

    /// <summary>Страница, на которой выдаётся ключ выбранной службы.</summary>
    public string AiKeyPage => _ai.Service.KeyPage;

    /// <summary>Что стоит знать о выбранной службе.</summary>
    public string AiServiceNotice => _ai.Service.Notice;

    /// <summary>Доступные стили ответов помощника.</summary>
    public IReadOnlyList<AiStyleOption> AiStyleOptions { get; } =
    [
        new(AiStyle.Brief, "Кратко"),
        new(AiStyle.Detailed, "Подробно"),
        new(AiStyle.GameMaster, "Как мастер игры"),
        new(AiStyle.Technical, "Технически"),
    ];

    /// <summary>Модели, доступные для выбора.</summary>
    public ObservableCollection<string> AiModels { get; }

    /// <summary>Минимально допустимый размер шрифта.</summary>
    public double MinimumFontSize { get; } = AppSettings.MinimumFontSize;

    /// <summary>Максимально допустимый размер шрифта.</summary>
    public double MaximumFontSize { get; } = AppSettings.MaximumFontSize;

    /// <summary>Минимально допустимый масштаб интерфейса.</summary>
    public double MinimumInterfaceScale { get; } = AppSettings.MinimumInterfaceScale;

    /// <summary>Максимально допустимый масштаб интерфейса.</summary>
    public double MaximumInterfaceScale { get; } = AppSettings.MaximumInterfaceScale;

    /// <summary>Каталог пользовательских данных.</summary>
    public string DataDirectory => _paths.DataDirectory;

    /// <summary>Каталог журналов работы приложения.</summary>
    public string LogsDirectory => _paths.LogsDirectory;

    /// <summary>Версия приложения.</summary>
    public string Version => _updates.CurrentVersion;

    /// <summary>Можно ли сейчас проверить наличие обновления.</summary>
    public bool CanCheckUpdates => _updates.IsConfigured && _updates.IsInstalled && !IsUpdating;

    /// <summary>Можно ли загрузить найденное обновление.</summary>
    public bool CanDownloadUpdate => IsUpdateAvailable && !IsUpdateDownloaded && !IsUpdating;

    /// <summary>Можно ли установить загруженное обновление.</summary>
    public bool CanInstallUpdate => IsUpdateDownloaded && !IsUpdating;

    /// <summary>Проверяет наличие новой версии.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача проверки.</returns>
    [RelayCommand(CanExecute = nameof(CanCheckUpdates))]
    private async Task CheckUpdatesAsync(CancellationToken cancellationToken)
    {
        IsUpdating = true;
        UpdateStatusText = "Проверяем обновления…";
        UpdateProgress = 0;
        RefreshUpdateCommands();
        try
        {
            var result = await _updates.CheckAsync(cancellationToken).ConfigureAwait(true);
            if (result.IsFailure)
            {
                UpdateStatusText = result.Error!;
                return;
            }

            var update = result.Value;
            IsUpdateAvailable = update is not null;
            IsUpdateDownloaded = false;
            UpdateReleaseNotes = update?.ReleaseNotes ?? string.Empty;
            UpdateStatusText = update is null
                ? $"Установлена последняя версия: {_updates.CurrentVersion}."
                : $"Доступна версия {update.Version}. Размер: {FormatSize(update.DownloadSizeBytes)}.";
        }
        finally
        {
            IsUpdating = false;
            RefreshUpdateCommands();
        }
    }

    /// <summary>Загружает найденную версию.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача загрузки.</returns>
    [RelayCommand(CanExecute = nameof(CanDownloadUpdate))]
    private async Task DownloadUpdateAsync(CancellationToken cancellationToken)
    {
        IsUpdating = true;
        UpdateStatusText = "Загружаем обновление…";
        RefreshUpdateCommands();
        try
        {
            var progress = new Progress<int>(value =>
            {
                UpdateProgress = value;
                UpdateStatusText = $"Загружаем обновление… {value}%";
            });
            var result = await _updates.DownloadAsync(progress, cancellationToken).ConfigureAwait(true);
            if (result.IsFailure)
            {
                UpdateStatusText = result.Error!;
                return;
            }

            IsUpdateDownloaded = true;
            UpdateStatusText = "Обновление загружено и готово к установке.";
        }
        finally
        {
            IsUpdating = false;
            RefreshUpdateCommands();
        }
    }

    /// <summary>Создаёт резервную копию и запускает установку с перезапуском.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача подготовки установки.</returns>
    [RelayCommand(CanExecute = nameof(CanInstallUpdate))]
    private async Task InstallUpdateAsync(CancellationToken cancellationToken)
    {
        if (!await _dialogs.ShowConfirmationAsync(
                "Установить обновление",
                "Приложение создаст резервную копию, закроется, установит обновление и запустится снова.")
            .ConfigureAwait(true))
        {
            return;
        }

        IsUpdating = true;
        UpdateStatusText = "Создаём резервную копию перед обновлением…";
        RefreshUpdateCommands();
        try
        {
            var backup = await _backups.CreateBackupAsync(
                "Перед обновлением приложения",
                false,
                cancellationToken).ConfigureAwait(true);
            if (backup.IsFailure)
            {
                UpdateStatusText = $"Обновление отменено: {backup.Error}";
                await _dialogs.ShowErrorAsync("Обновление", UpdateStatusText).ConfigureAwait(true);
                return;
            }

            UpdateStatusText = "Перезапускаем приложение для установки…";
            var result = _updates.ApplyAndRestart();
            if (result.IsFailure)
            {
                UpdateStatusText = result.Error!;
                await _dialogs.ShowErrorAsync("Обновление", result.Error!).ConfigureAwait(true);
            }
        }
        finally
        {
            IsUpdating = false;
            RefreshUpdateCommands();
        }
    }

    private string GetInitialUpdateStatus()
    {
        if (!_updates.IsConfigured)
        {
            return "Локальная тестовая сборка: источник обновлений будет подключён при публикации.";
        }

        return _updates.IsInstalled
            ? $"Текущая версия: {_updates.CurrentVersion}."
            : "Автообновление станет доступно после установки через Setup.exe.";
    }

    private void RefreshUpdateCommands()
    {
        OnPropertyChanged(nameof(CanCheckUpdates));
        OnPropertyChanged(nameof(CanDownloadUpdate));
        OnPropertyChanged(nameof(CanInstallUpdate));
        CheckUpdatesCommand.NotifyCanExecuteChanged();
        DownloadUpdateCommand.NotifyCanExecuteChanged();
        InstallUpdateCommand.NotifyCanExecuteChanged();
    }

    private static string FormatSize(long bytes) => bytes <= 0
        ? "не указан"
        : $"{bytes / 1024d / 1024d:0.0} МБ";
    /// <summary>
    /// Открывает каталог пользовательских данных в проводнике.
    /// </summary>
    [RelayCommand]
    private void OpenDataFolder() => OpenFolder(_paths.DataDirectory);

    /// <summary>
    /// Открывает каталог журналов в проводнике.
    /// </summary>
    [RelayCommand]
    private void OpenLogsFolder() => OpenFolder(_paths.LogsDirectory);

    /// <summary>
    /// Открывает каталог в проводнике операционной системы.
    /// </summary>
    /// <param name="path">Полный путь к каталогу.</param>
    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            UiLog.FolderOpenFailed(_logger, exception, path);
            _notifications.Show("Не удалось открыть папку", NotificationKind.Warning);
        }
    }

    /// <summary>
    /// Проверяет связь со службой языковой модели и показывает итог рядом с ключом.
    ///
    /// Проверка выполняет настоящий запрос, а не просто сверяет длину ключа:
    /// иначе она подтверждала бы лишь то, что поле заполнено.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после проверки.</returns>
    [RelayCommand]
    private async Task CheckAiAsync(CancellationToken cancellationToken)
    {
        // Состав моделей у служб меняется: встроенный перечень успевает устареть,
        // и проверка спотыкалась о модель, закрытую для новых пользователей.
        // Поэтому список сначала запрашивается у службы: ключ к этому времени
        // уже введён, и проверять есть чем.
        await RefreshAiModelsAsync(cancellationToken).ConfigureAwait(true);

        AiCheckText = "Проверяем связь…";

        // Сбой проверки не должен закрывать приложение: команда выполняется
        // в потоке интерфейса, и необработанное исключение обрывает работу
        // вместе со всеми открытыми разделами.
        try
        {
            var result = await _ai.CheckAsync(cancellationToken).ConfigureAwait(true);

            if (result.IsFailure)
            {
                AiCheckText = result.Error!;
                return;
            }

            var connection = result.Value;

            AiCheckText =
                $"Связь есть: модель {connection.Model} ответила за " +
                $"{connection.Latency.TotalSeconds.ToString("0.0", CultureInfo.CurrentCulture)} с. " +
                $"Моделей доступно: {connection.AvailableModels}.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            UiLog.SettingsUpdateFailed(_logger, exception);

            AiCheckText = $"Проверка связи сорвалась: {exception.Message}";
        }
    }

    /// <summary>
    /// Перечитывает список моделей, доступных по заданному ключу.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после чтения.</returns>
    [RelayCommand]
    private async Task RefreshAiModelsAsync(CancellationToken cancellationToken)
    {
        Shared.Results.Result<IReadOnlyList<string>> result;

        try
        {
            result = await _ai.GetModelsAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            UiLog.SettingsUpdateFailed(_logger, exception);

            AiCheckText = $"Не удалось прочитать список моделей: {exception.Message}";
            return;
        }

        if (result.IsFailure)
        {
            AiCheckText = result.Error!;
            return;
        }

        var selected = AiModel;

        AiModels.Clear();

        foreach (var model in result.Value)
        {
            AiModels.Add(model);
        }

        _modelsProvider = _settings.Current.AiProvider;

        // Модель, оставшаяся от другой службы, в новом списке не существует:
        // сохранять её выбор — верный способ получить отказ при первом запросе.
        AiModel = AiModels.Contains(selected) ? selected : AiModels.FirstOrDefault() ?? selected;

        AiCheckText = AiModels.Count > 0
            ? $"Служба вернула подходящих моделей: {AiModels.Count}."
            : "Служба не вернула ни одной подходящей модели.";
    }

    /// <summary>
    /// Готовит список моделей выбранной службы.
    ///
    /// Список, полученный от службы, сохраняется до смены службы: настройки
    /// перечитываются при любом изменении — вплоть до каждого знака, введённого
    /// в поле ключа, — и заполнение списка заново стирало бы полученный перечень
    /// сразу после нажатия «Обновить список».
    /// </summary>
    private void EnsureAiModels()
    {
        if (_modelsProvider != _settings.Current.AiProvider || AiModels.Count == 0)
        {
            AiModels.Clear();

            foreach (var model in _ai.RecommendedModels)
            {
                AiModels.Add(model);
            }

            _modelsProvider = _settings.Current.AiProvider;
        }

        // Выбранная модель обязана быть в списке: иначе поле выбора покажет
        // пустоту, хотя запросы уходят к вполне определённой модели.
        var current = _ai.Model;

        if (!AiModels.Contains(current))
        {
            AiModels.Insert(0, current);
        }
    }

    /// <summary>
    /// Возвращает все параметры к значениям по умолчанию.
    /// </summary>
    /// <returns>Задача, завершающаяся после сохранения параметров.</returns>
    [RelayCommand]
    private async Task RestoreDefaultsAsync()
    {
        var defaults = new AppSettings();

        await ApplyAsync(settings =>
        {
            settings.Theme = defaults.Theme;
            settings.Accent = defaults.Accent;
            settings.FontSize = defaults.FontSize;
            settings.InterfaceScale = defaults.InterfaceScale;
            settings.BackupIntervalHours = defaults.BackupIntervalHours;
            settings.BackupRetentionDays = defaults.BackupRetentionDays;
            settings.DiceHistoryLimit = defaults.DiceHistoryLimit;
            settings.DiceAnimationEnabled = defaults.DiceAnimationEnabled;
        }).ConfigureAwait(true);

        LoadFromSettings();
        _notifications.Show("Параметры возвращены к значениям по умолчанию", NotificationKind.Success);
    }

    partial void OnThemeChanged(ThemeMode value) => Persist(settings => settings.Theme = value);

    partial void OnAccentChanged(AccentColor value) => Persist(settings => settings.Accent = value);

    partial void OnFontSizeChanged(double value) => Persist(settings => settings.FontSize = value);

    partial void OnInterfaceScaleChanged(double value) => Persist(settings => settings.InterfaceScale = value);

    partial void OnBackupIntervalHoursChanged(int value) =>
        Persist(settings => settings.BackupIntervalHours = value);

    partial void OnBackupRetentionDaysChanged(int value) =>
        Persist(settings => settings.BackupRetentionDays = value);

    partial void OnDiceHistoryLimitChanged(int value) => Persist(settings => settings.DiceHistoryLimit = value);

    partial void OnIsUpdatingChanged(bool value) => RefreshUpdateCommands();

    partial void OnIsUpdateAvailableChanged(bool value) => RefreshUpdateCommands();

    partial void OnIsUpdateDownloadedChanged(bool value) => RefreshUpdateCommands();

    partial void OnDiceAnimationEnabledChanged(bool value) =>
        Persist(settings => settings.DiceAnimationEnabled = value);

    partial void OnAiApiKeyChanged(string value) => Persist(settings => settings.SetAiKey(value));

    partial void OnAiModelChanged(string value) => Persist(settings => settings.SetAiModel(value));

    partial void OnAiStyleChanged(AiStyle value) => Persist(settings => settings.AiStyle = value);

    partial void OnAiProviderChanged(AiProvider value)
    {
        if (_isApplyingExternalUpdate)
        {
            return;
        }

        _ = ChangeProviderAsync(value);
    }

    /// <summary>
    /// Переключает службу языковых моделей.
    ///
    /// Ключ и модель у каждой службы свои, поэтому после переключения поля
    /// перечитываются, а список моделей запрашивается заново: модели одной службы
    /// в другой не существуют.
    /// </summary>
    /// <param name="value">Выбранная служба.</param>
    /// <returns>Задача, завершающаяся после переключения.</returns>
    private async Task ChangeProviderAsync(AiProvider value)
    {
        await ApplyAsync(settings => settings.AiProvider = value).ConfigureAwait(true);

        LoadFromSettings();

        OnPropertyChanged(nameof(AiServiceTitle));
        OnPropertyChanged(nameof(AiKeyPage));
        OnPropertyChanged(nameof(AiServiceNotice));

        AiCheckText = string.Empty;

        if (_ai.IsConfigured)
        {
            await RefreshAiModelsAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private void LoadFromSettings()
    {
        _isApplyingExternalUpdate = true;

        try
        {
            var current = _settings.Current;

            Theme = current.Theme;
            Accent = current.Accent;
            FontSize = current.FontSize;
            InterfaceScale = current.InterfaceScale;
            BackupIntervalHours = current.BackupIntervalHours;
            BackupRetentionDays = current.BackupRetentionDays;
            DiceHistoryLimit = current.DiceHistoryLimit;
            DiceAnimationEnabled = current.DiceAnimationEnabled;
            AiProvider = current.AiProvider;
            AiApiKey = current.GetAiKey() ?? string.Empty;
            AiStyle = current.AiStyle;

            EnsureAiModels();

            AiModel = _ai.Model;
        }
        finally
        {
            _isApplyingExternalUpdate = false;
        }
    }

    private void Persist(Action<AppSettings> modify)
    {
        if (_isApplyingExternalUpdate)
        {
            return;
        }

        _ = ApplyAsync(modify);
    }

    private async Task ApplyAsync(Action<AppSettings> modify)
    {
        try
        {
            await _settings.UpdateAsync(modify).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            UiLog.SettingsUpdateFailed(_logger, exception);
            _notifications.Show("Не удалось сохранить параметры приложения", NotificationKind.Error);
        }
    }

    /// <summary>
    /// Вариант выбора режима оформления.
    /// </summary>
    /// <param name="Value">Режим оформления.</param>
    /// <param name="Title">Отображаемое название.</param>
    public sealed record ThemeOption(ThemeMode Value, string Title);

    /// <summary>
    /// Вариант выбора акцентного цвета.
    /// </summary>
    /// <param name="Value">Акцентный цвет.</param>
    /// <param name="Title">Отображаемое название.</param>
    public sealed record AccentOption(AccentColor Value, string Title);

    /// <summary>
    /// Вариант выбора стиля ответов помощника.
    /// </summary>
    /// <param name="Value">Стиль ответа.</param>
    /// <param name="Title">Отображаемое название.</param>
    public sealed record AiStyleOption(AiStyle Value, string Title);

    /// <summary>
    /// Вариант выбора службы языковых моделей.
    /// </summary>
    /// <param name="Value">Служба.</param>
    /// <param name="Title">Отображаемое название.</param>
    public sealed record AiProviderOption(AiProvider Value, string Title);
}
