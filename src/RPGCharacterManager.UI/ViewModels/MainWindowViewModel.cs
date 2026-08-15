using System.Collections.ObjectModel;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Distribution;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Macros;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.Settings;
using RPGCharacterManager.Core.Models.Shell;
using RPGCharacterManager.Shared;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Behaviors;
using RPGCharacterManager.UI.Logging;
using RPGCharacterManager.UI.ViewModels.Dice;
using RPGCharacterManager.UI.ViewModels.Documents;
using RPGCharacterManager.UI.ViewModels.Shell;

namespace RPGCharacterManager.UI.ViewModels;

/// <summary>
/// Модель представления главного окна.
///
/// Собирает панель навигации и команды из всех зарегистрированных
/// <see cref="IShellContributor"/>. Само окно не содержит сведений о конкретных
/// подсистемах, поэтому новые разделы приложения добавляются без его изменения.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly INavigationService _navigation;
    private readonly ISettingsService _settings;
    private readonly IThemeService _theme;
    private readonly IEventBus _eventBus;
    private readonly IDialogService _dialogs;
    private readonly IMacroService _macros;
    private readonly INotificationService _notifications;
    private readonly IApplicationUpdateService _updates;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IReadOnlyList<IShellContributor> _contributors;
    private readonly List<IDisposable> _subscriptions = [];

    /// <summary>
    /// Длительность исчезновения вкладки. Столько времени документ остаётся
    /// в списке после закрытия, чтобы соседние вкладки успели плавно
    /// занять его место. Значение согласовано с переходом стиля «Вкладки».
    /// </summary>
    private static readonly TimeSpan TabDisappearance = TimeSpan.FromMilliseconds(120);

    /// <summary>
    /// Признак того, что раздел отмечается вслед за показанным документом.
    /// В это время выбор раздела не должен открывать документ повторно.
    /// </summary>
    private bool _isSynchronizingNavigation;

    [ObservableProperty]
    private NavigationItemViewModel? _selectedNavigationItem;

    /// <summary>Документ, вкладка которого сейчас исчезает.</summary>
    [ObservableProperty]
    private IDocument? _closingDocument;

    // Выбранная вкладка и показанный документ разделены намеренно.
    //
    // Полоса вкладок — обычный список, и при перестановке элемента он на мгновение
    // снимает выделение. Если бы содержимое рабочей области следовало за выделением
    // напрямую, каждая перестановка при перетаскивании вкладки запускала бы переход
    // к пустой области и обратно, и рабочая область оставалась бы пустой. Показанный
    // документ меняется только тогда, когда об этом сообщает служба навигации.

    /// <summary>Документ, выбранный в полосе вкладок.</summary>
    [ObservableProperty]
    private IDocument? _selectedDocument;

    /// <summary>Документ, содержимое которого показано в рабочей области.</summary>
    [ObservableProperty]
    private IDocument? _activeDocument;

    [ObservableProperty]
    private bool _isNavigationVisible = true;

    [ObservableProperty]
    private double _interfaceScale = 1.0;

    /// <summary>
    /// Создаёт модель представления главного окна.
    /// </summary>
    /// <param name="navigation">Служба навигации по документам.</param>
    /// <param name="settings">Служба пользовательских настроек.</param>
    /// <param name="theme">Служба оформления.</param>
    /// <param name="eventBus">Шина событий.</param>
    /// <param name="dispatcher">Диспетчер потока пользовательского интерфейса.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    /// <param name="macros">Служба макросов: горячие клавиши и их выполнение.</param>
    /// <param name="notifications">Служба всплывающих уведомлений.</param>
    /// <param name="updates">Служба обновления приложения.</param>
    /// <param name="statusBar">Модель представления строки состояния.</param>
    /// <param name="dicePanel">Модель представления панели бросков.</param>
    /// <param name="contributors">Поставщики элементов оболочки.</param>
    /// <param name="logger">Журналировщик.</param>
    public MainWindowViewModel(
        INavigationService navigation,
        ISettingsService settings,
        IThemeService theme,
        IEventBus eventBus,
        IUiDispatcher dispatcher,
        IDialogService dialogs,
        IMacroService macros,
        INotificationService notifications,
        IApplicationUpdateService updates,
        StatusBarViewModel statusBar,
        DicePanelViewModel dicePanel,
        IEnumerable<IShellContributor> contributors,
        ILogger<MainWindowViewModel> logger)
    {
        Guard.NotNull(dispatcher);

        _navigation = Guard.NotNull(navigation);
        _settings = Guard.NotNull(settings);
        _theme = Guard.NotNull(theme);
        _eventBus = Guard.NotNull(eventBus);
        _dialogs = Guard.NotNull(dialogs);
        _macros = Guard.NotNull(macros);
        _notifications = Guard.NotNull(notifications);
        _updates = Guard.NotNull(updates);
        _logger = Guard.NotNull(logger);
        StatusBar = Guard.NotNull(statusBar);
        DicePanel = Guard.NotNull(dicePanel);

        _contributors = Guard.NotNull(contributors).OrderBy(contributor => contributor.Order).ToList();

        NavigationItems = BuildNavigation();

        foreach (var shortcut in BuildShortcuts())
        {
            Shortcuts.Add(shortcut);
        }

        _navigation.ActiveDocumentChanged += OnActiveDocumentChanged;

        // Обработчики изменяют свойства, связанные с интерфейсом, поэтому подписка
        // выполняется с переключением в поток интерфейса.
        _subscriptions.Add(_eventBus.SubscribeOnUiThread<SettingsChangedEvent>(dispatcher, OnSettingsChanged));
        _subscriptions.Add(_eventBus.SubscribeOnUiThread<ApplicationErrorEvent>(dispatcher, OnApplicationError));

        // Сочетания клавиш макросов приходят из базы данных и меняются во время
        // работы приложения, поэтому список перечитывается по событию.
        _subscriptions.Add(_eventBus.SubscribeOnUiThread<MacrosChangedEvent>(
            dispatcher,
            OnMacrosChanged));

        InterfaceScale = _settings.Current.InterfaceScale;
    }

    /// <summary>
    /// Перечитывает сочетания клавиш макросов.
    ///
    /// Сочетания встроенных команд остаются на месте: заменяются только те,
    /// что принадлежат макросам.
    /// </summary>
    /// <returns>Задача, завершающаяся после обновления списка.</returns>
    private async Task ReloadMacroShortcutsAsync()
    {
        try
        {
            var macros = await _macros.GetHotkeysAsync().ConfigureAwait(true);

            foreach (var existing in Shortcuts.Where(shortcut => shortcut.IsMacro).ToList())
            {
                Shortcuts.Remove(existing);
            }

            foreach (var macro in macros)
            {
                if (ParseGesture(macro.Hotkey) is not { } gesture)
                {
                    continue;
                }

                Shortcuts.Add(new ShortcutDefinition(
                    gesture,
                    new RelayCommand(() => _ = RunMacroAsync(macro)),
                    IsMacro: true));
            }
        }
        catch (Exception exception)
        {
            UiLog.MacroShortcutsFailed(_logger, exception);
        }
    }

    /// <summary>
    /// Разбирает сочетание клавиш, записанное пользователем.
    ///
    /// Пользователь мог написать что угодно: непонятное сочетание просто
    /// не работает и не мешает остальным.
    /// </summary>
    /// <param name="hotkey">Текст сочетания.</param>
    /// <returns>Сочетание клавиш либо <see langword="null"/>.</returns>
    private static KeyGesture? ParseGesture(string? hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return null;
        }

        try
        {
            return KeyGesture.Parse(hotkey);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Перечитывает горячие клавиши при изменении состава макросов.
    /// </summary>
    /// <param name="changed">Событие изменения макросов.</param>
    private void OnMacrosChanged(MacrosChangedEvent changed) => _ = ReloadMacroShortcutsAsync();

    /// <summary>
    /// Выполняет макрос над персонажем открытого листа.
    /// </summary>
    /// <param name="macro">Макрос.</param>
    /// <returns>Задача, завершающаяся после выполнения.</returns>
    private async Task RunMacroAsync(MacroListItem macro)
    {
        // Макрос меняет персонажа, поэтому ему нужен персонаж: горячая клавиша
        // работает над тем, чей лист открыт.
        if (_navigation.ActiveDocument is not ICharacterDocument document)
        {
            _notifications.Show(
                $"«{macro.Name}»: откройте лист персонажа — макрос выполняется над ним",
                NotificationKind.Warning);

            return;
        }

        var result = await _macros.RunAsync(macro.Id, document.CharacterId).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Макрос", result.Error!).ConfigureAwait(true);
            return;
        }

        var report = result.Value;

        _notifications.Show(
            $"«{report.MacroName}» → {report.CharacterName}: {report.Summary}",
            report.WasConditionMet ? NotificationKind.Success : NotificationKind.Warning);
    }

    /// <summary>Заголовок главного окна.</summary>
    public string WindowTitle { get; } = ApplicationConstants.ApplicationName;

    /// <summary>Разделы панели навигации — единственный способ перейти в раздел.</summary>
    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    /// <summary>
    /// Горячие клавиши главного окна.
    ///
    /// Список изменяемый: сочетания макросов приходят из базы данных и правятся
    /// во время работы приложения.
    /// </summary>
    public ObservableCollection<ShortcutDefinition> Shortcuts { get; } = [];

    /// <summary>Открытые документы рабочей области.</summary>
    public ReadOnlyObservableCollection<IDocument> Documents => _navigation.Documents;

    /// <summary>Модель представления строки состояния.</summary>
    public StatusBarViewModel StatusBar { get; }

    /// <summary>
    /// Панель бросков кубиков.
    ///
    /// Панель принадлежит окну, а не документу: бросок нужен в любом разделе,
    /// и закрывать лист персонажа ради него незачем.
    /// </summary>
    public DicePanelViewModel DicePanel { get; }

    /// <summary>
    /// Открывает документ, соответствующий разделу навигации, выбранному по умолчанию.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после открытия документа.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Горячие клавиши читаются здесь, а не в конструкторе: окно создаётся
        // до подготовки базы данных, и в конструкторе таблицы макросов
        // ещё может не быть — например сразу после обновления приложения.
        await ReloadMacroShortcutsAsync().ConfigureAwait(true);

        // Панель бросков закрыта при запуске, поэтому её содержимое читается при
        // первом открытии: запуск приложения не должен ждать журнал бросков.
        var first = NavigationItems.FirstOrDefault();
        if (first is null)
        {
            return;
        }

        // Отмечать раздел вручную не нужно: панель следует за показанным документом.
        await OpenDocumentAsync(first.DocumentId, cancellationToken).ConfigureAwait(true);

        // Проверка идёт после показа основного раздела и не задерживает запуск окна.
        _ = CheckForUpdatesOnStartupAsync();
    }

    /// <summary>Тихо проверяет обновления и уведомляет только о найденной версии.</summary>
    /// <returns>Задача проверки.</returns>
    private async Task CheckForUpdatesOnStartupAsync()
    {
        if (!_updates.IsConfigured || !_updates.IsInstalled)
        {
            return;
        }

        var result = await _updates.CheckAsync().ConfigureAwait(true);
        if (result.IsSuccess && result.Value is { } update)
        {
            _notifications.Show(
                $"Доступна версия {update.Version}. Откройте «Настройки» → «Обновления приложения».",
                NotificationKind.Information);
        }
    }
    /// <inheritdoc />
    public void Dispose()
    {
        _navigation.ActiveDocumentChanged -= OnActiveDocumentChanged;

        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
        StatusBar.Dispose();
        DicePanel.Dispose();
    }

    /// <summary>Открывает домашнюю страницу в новой вкладке или активирует её, если она уже открыта.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача открытия вкладки.</returns>
    [RelayCommand]
    private Task NewTabAsync(CancellationToken cancellationToken) =>
        OpenDocumentAsync(DocumentIds.Overview, cancellationToken);
    /// <summary>
    /// Закрывает указанный документ рабочей области.
    /// </summary>
    /// <param name="document">Закрываемый документ.</param>
    /// <returns>Задача, завершающаяся после попытки закрытия.</returns>
    [RelayCommand]
    private async Task CloseDocumentAsync(IDocument? document)
    {
        if (document is null)
        {
            return;
        }

        // Документ спрашивают о готовности до анимации: если он потребует
        // подтверждения и получит отказ, вкладка не должна ни исчезать,
        // ни возвращаться обратно.
        if (!await document.CanCloseAsync().ConfigureAwait(true))
        {
            return;
        }

        // Вкладка сначала сжимается и гаснет и только потом покидает полосу.
        // Пока она сжимается, соседние вкладки плавно занимают её место.
        ClosingDocument = document;
        await Task.Delay(TabDisappearance).ConfigureAwait(true);

        // Готовность уже подтверждена выше, поэтому документ убирается без
        // повторного вопроса: иначе пользователь увидел бы то же подтверждение дважды.
        _navigation.Close(document);
        ClosingDocument = null;
    }

    /// <summary>
    /// Перемещает документ в новую позицию полосы вкладок.
    /// </summary>
    /// <param name="request">Запрос перемещения, сформированный при перетаскивании вкладки.</param>
    [RelayCommand]
    private void MoveDocument(ReorderRequest? request)
    {
        if (request?.Item is IDocument document)
        {
            _navigation.Move(document, request.TargetIndex);
        }
    }

    /// <summary>
    /// Показывает или скрывает панель навигации.
    /// </summary>
    [RelayCommand]
    private void ToggleNavigation() => IsNavigationVisible = !IsNavigationVisible;

    partial void OnSelectedNavigationItemChanged(NavigationItemViewModel? value)
    {
        // Раздел отмечен вслед за уже показанным документом — открывать нечего.
        if (_isSynchronizingNavigation || value is null)
        {
            return;
        }

        _ = OpenDocumentAsync(value.DocumentId, CancellationToken.None);
    }

    partial void OnSelectedDocumentChanged(IDocument? value)
    {
        if (value is not null)
        {
            _navigation.Activate(value);
        }
    }

    private async Task OpenDocumentAsync(string documentId, CancellationToken cancellationToken)
    {
        try
        {
            await _navigation.OpenAsync(documentId, null, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            UiLog.DocumentOpenFailed(_logger, exception, documentId);
            await _dialogs
                .ShowErrorAsync("Не удалось открыть раздел", $"Раздел «{documentId}» недоступен.", exception.ToString())
                .ConfigureAwait(true);
        }
    }

    private void OnActiveDocumentChanged(object? sender, IDocument? document)
    {
        ActiveDocument = document;
        SelectedDocument = document;
        SynchronizeNavigation(document);

        // Панель бросков следует за показанным персонажем: пока открыт его лист,
        // формула броска видит его характеристики.
        if (document is ICharacterDocument character)
        {
            DicePanel.SetCharacter(character.CharacterId, character.CharacterName);
        }
        else
        {
            DicePanel.SetCharacter(null, null);
        }
    }

    /// <summary>
    /// Отмечает в панели разделов тот раздел, документ которого показан.
    ///
    /// Панель показывает, где пользователь находится, поэтому отметка следует за
    /// рабочей областью, а не за последним щелчком по панели. У документа, открытого
    /// для конкретного объекта — например, у листа персонажа, — своего раздела нет,
    /// и тогда отметка снимается: показывать раздел, которого на экране нет, значит
    /// вводить в заблуждение.
    /// </summary>
    /// <param name="document">Показанный документ или <see langword="null"/>.</param>
    private void SynchronizeNavigation(IDocument? document)
    {
        _isSynchronizingNavigation = true;

        try
        {
            SelectedNavigationItem = document is null
                ? null
                : NavigationItems.FirstOrDefault(item =>
                    string.Equals(item.DocumentId, document.DocumentId, StringComparison.Ordinal));
        }
        finally
        {
            _isSynchronizingNavigation = false;
        }
    }

    private void OnSettingsChanged(SettingsChangedEvent notification)
    {
        var settings = notification.Settings;

        _theme.Apply(settings);
        InterfaceScale = settings.InterfaceScale;

        UiLog.ThemeApplied(_logger, settings.Theme.ToString(), settings.Accent.ToString());
    }

    private void OnApplicationError(ApplicationErrorEvent notification)
    {
        // Диалог показывается только для ошибок, прерывающих работу пользователя.
        // Остальные сбои уже записаны в журнал службой, обнаружившей ошибку.
        if (!notification.IsFatal)
        {
            return;
        }

        _ = _dialogs.ShowErrorAsync(
            "Критическая ошибка",
            $"В подсистеме «{notification.Source}» произошла ошибка, работа может быть нарушена.",
            notification.Exception.ToString());
    }

    private ObservableCollection<NavigationItemViewModel> BuildNavigation() =>
        new(_contributors
            .SelectMany(contributor => contributor.GetNavigationItems())
            .OrderBy(item => item.Order)
            .Select(item => new NavigationItemViewModel(item)));

    /// <summary>
    /// Собирает горячие клавиши из команд подсистем.
    /// Команда без сочетания клавиш пропускается: она вызывается из своего раздела.
    /// </summary>
    /// <returns>Горячие клавиши главного окна.</returns>
    private List<ShortcutDefinition> BuildShortcuts() =>
        _contributors
            .SelectMany(contributor => contributor.GetCommands())
            .OrderBy(command => command.Order)
            .Where(command => !string.IsNullOrWhiteSpace(command.GestureText))
            .Select(command => new ShortcutDefinition(
                KeyGesture.Parse(command.GestureText!),
                command.Command))
            .ToList();
}
