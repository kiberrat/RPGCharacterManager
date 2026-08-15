using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Layouts;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Документ «Интерфейс»: редактор макетов листа персонажа.
///
/// Макет — это вкладки и расставленные по ним панели. Перечень панелей окно
/// не знает: он приходит из каталога, поэтому панель, появившаяся на будущем
/// этапе, окажется в списке доступных сама.
/// </summary>
public sealed partial class LayoutEditorViewModel : DocumentViewModelBase
{
    /// <summary>Шаг изменения доли ширины панели.</summary>
    public const double WidthStep = 0.25;

    private readonly ILayoutService _layouts;
    private readonly ISheetPanelCatalog _catalog;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;

    [ObservableProperty]
    private LayoutListItem? _selectedLayout;

    [ObservableProperty]
    private LayoutTabRowViewModel? _selectedTab;

    [ObservableProperty]
    private SheetPanelDescriptor? _selectedAvailablePanel;

    [ObservableProperty]
    private string _layoutName = string.Empty;

    [ObservableProperty]
    private string _newTabTitle = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    private Guid? _openLayoutId;

    /// <summary>
    /// Создаёт модель представления редактора макетов.
    /// </summary>
    /// <param name="layouts">Служба макетов.</param>
    /// <param name="catalog">Каталог панелей.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    /// <param name="notifications">Служба всплывающих уведомлений.</param>
    public LayoutEditorViewModel(
        ILayoutService layouts,
        ISheetPanelCatalog catalog,
        IDialogService dialogs,
        INotificationService notifications)
        : base(LayoutShellContributor.EditorDocumentId, "Интерфейс")
    {
        _layouts = Guard.NotNull(layouts);
        _catalog = Guard.NotNull(catalog);
        _dialogs = Guard.NotNull(dialogs);
        _notifications = Guard.NotNull(notifications);

        AvailablePanels = catalog.Panels;
        _selectedAvailablePanel = AvailablePanels.Count > 0 ? AvailablePanels[0] : null;
    }

    /// <summary>Макеты в порядке названий.</summary>
    public ObservableCollection<LayoutListItem> Layouts { get; } = [];

    /// <summary>Вкладки открытого макета.</summary>
    public ObservableCollection<LayoutTabRowViewModel> Tabs { get; } = [];

    /// <summary>Панели, которые можно поставить на вкладку.</summary>
    public IReadOnlyList<SheetPanelDescriptor> AvailablePanels { get; }

    /// <summary>Макет открыт.</summary>
    public bool IsLayoutOpen => SelectedLayout is not null;

    /// <inheritdoc />
    public override Task InitializeAsync(CancellationToken cancellationToken = default) =>
        RefreshAsync(cancellationToken);

    /// <summary>
    /// Перечитывает список макетов, сохраняя открытый.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var result = await _layouts.GetAllAsync(cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Макеты", result.Error!).ConfigureAwait(true);
            return;
        }

        var previous = SelectedLayout?.Id;

        Layouts.Clear();

        foreach (var layout in result.Value)
        {
            Layouts.Add(layout);
        }

        _openLayoutId = null;

        SelectedLayout = previous is { } id
            ? Layouts.FirstOrDefault(layout => layout.Id == id) ?? Layouts.FirstOrDefault()
            : Layouts.FirstOrDefault(layout => layout.IsDefault) ?? Layouts.FirstOrDefault();
    }

    /// <summary>
    /// Создаёт макет копией открытого.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после создания.</returns>
    [RelayCommand]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        var result = await _layouts
            .CreateAsync("Новый макет", SelectedLayout?.Id, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Новый макет", result.Error!).ConfigureAwait(true);
            return;
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(true);

        SelectedLayout = Layouts.FirstOrDefault(layout => layout.Id == result.Value);
        _notifications.Show("Макет создан", NotificationKind.Success);
    }

    /// <summary>
    /// Сохраняет название открытого макета.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    [RelayCommand]
    private async Task RenameAsync(CancellationToken cancellationToken)
    {
        if (SelectedLayout is not { } layout)
        {
            return;
        }

        await RunAsync(
            () => _layouts.RenameAsync(layout.Id, LayoutName, cancellationToken),
            "Переименование макета",
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Применяет открытый макет к листу персонажа.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после применения.</returns>
    [RelayCommand]
    private async Task ApplyAsync(CancellationToken cancellationToken)
    {
        if (SelectedLayout is not { } layout)
        {
            return;
        }

        if (await RunAsync(
                () => _layouts.ApplyAsync(layout.Id, cancellationToken),
                "Применение макета",
                cancellationToken).ConfigureAwait(true))
        {
            _notifications.Show(
                "Макет применён. Откройте лист персонажа заново, чтобы увидеть его.",
                NotificationKind.Success);
        }
    }

    /// <summary>
    /// Удаляет открытый макет.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (SelectedLayout is not { } layout)
        {
            return;
        }

        var confirmed = await _dialogs
            .ShowConfirmationAsync("Удаление макета", $"Удалить макет «{layout.Name}»?")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        await RunAsync(
            () => _layouts.DeleteAsync(layout.Id, cancellationToken),
            "Удаление макета",
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Добавляет вкладку в открытый макет.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после добавления.</returns>
    [RelayCommand]
    private async Task AddTabAsync(CancellationToken cancellationToken)
    {
        if (SelectedLayout is not { } layout)
        {
            return;
        }

        var result = await _layouts
            .AddTabAsync(layout.Id, NewTabTitle, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Новая вкладка", result.Error!).ConfigureAwait(true);
            return;
        }

        NewTabTitle = string.Empty;
        await ReloadLayoutAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Сохраняет заголовок вкладки.
    /// </summary>
    /// <param name="tab">Вкладка.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    [RelayCommand]
    private async Task RenameTabAsync(LayoutTabRowViewModel? tab, CancellationToken cancellationToken)
    {
        if (tab is null)
        {
            return;
        }

        await RunAsync(
            () => _layouts.RenameTabAsync(tab.Id, tab.Title, cancellationToken),
            "Переименование вкладки",
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Удаляет вкладку вместе со стоящими на ней панелями.
    /// </summary>
    /// <param name="tab">Вкладка.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task DeleteTabAsync(LayoutTabRowViewModel? tab, CancellationToken cancellationToken)
    {
        if (tab is null)
        {
            return;
        }

        await RunAsync(
            () => _layouts.DeleteTabAsync(tab.Id, cancellationToken),
            "Удаление вкладки",
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Двигает вкладку левее.
    /// </summary>
    /// <param name="tab">Вкладка.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после перестановки.</returns>
    [RelayCommand]
    private Task MoveTabLeftAsync(LayoutTabRowViewModel? tab, CancellationToken cancellationToken) =>
        MoveTabAsync(tab, -1, cancellationToken);

    /// <summary>
    /// Двигает вкладку правее.
    /// </summary>
    /// <param name="tab">Вкладка.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после перестановки.</returns>
    [RelayCommand]
    private Task MoveTabRightAsync(LayoutTabRowViewModel? tab, CancellationToken cancellationToken) =>
        MoveTabAsync(tab, 1, cancellationToken);

    /// <summary>
    /// Ставит выбранную панель на вкладку.
    /// </summary>
    /// <param name="tab">Вкладка.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после добавления.</returns>
    [RelayCommand]
    private async Task AddPanelAsync(LayoutTabRowViewModel? tab, CancellationToken cancellationToken)
    {
        if (tab is null || SelectedAvailablePanel is not { } panel)
        {
            return;
        }

        await RunAsync(
            () => _layouts.AddPanelAsync(tab.Id, panel.Id, cancellationToken),
            "Добавление панели",
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Убирает панель с макета.
    /// </summary>
    /// <param name="panel">Панель.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task RemovePanelAsync(LayoutPanelRowViewModel? panel, CancellationToken cancellationToken)
    {
        if (panel is null)
        {
            return;
        }

        await RunAsync(
            () => _layouts.RemovePanelAsync(panel.Id, cancellationToken),
            "Удаление панели",
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Расширяет панель.
    /// </summary>
    /// <param name="panel">Панель.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после изменения.</returns>
    [RelayCommand]
    private Task WidenPanelAsync(LayoutPanelRowViewModel? panel, CancellationToken cancellationToken) =>
        ResizePanelAsync(panel, WidthStep, cancellationToken);

    /// <summary>
    /// Сужает панель.
    /// </summary>
    /// <param name="panel">Панель.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после изменения.</returns>
    [RelayCommand]
    private Task NarrowPanelAsync(LayoutPanelRowViewModel? panel, CancellationToken cancellationToken) =>
        ResizePanelAsync(panel, -WidthStep, cancellationToken);

    /// <summary>
    /// Переносит панель туда, куда её перетащили.
    /// </summary>
    /// <param name="request">Запрос переноса.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после переноса.</returns>
    [RelayCommand]
    private async Task DropPanelAsync(PanelDropRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return;
        }

        await RunAsync(
            () => _layouts.MovePanelAsync(
                request.Panel.Id,
                request.Tab.Id,
                request.Position,
                cancellationToken),
            "Перенос панели",
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Двигает вкладку на указанное число мест.
    /// </summary>
    /// <param name="tab">Вкладка.</param>
    /// <param name="offset">Смещение: −1 левее, +1 правее.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после перестановки.</returns>
    private async Task MoveTabAsync(
        LayoutTabRowViewModel? tab,
        int offset,
        CancellationToken cancellationToken)
    {
        if (tab is null)
        {
            return;
        }

        var position = Tabs.IndexOf(tab) + offset;

        if (position < 0 || position >= Tabs.Count)
        {
            return;
        }

        await RunAsync(
            () => _layouts.MoveTabAsync(tab.Id, position, cancellationToken),
            "Перестановка вкладки",
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Изменяет долю ширины панели.
    /// </summary>
    /// <param name="panel">Панель.</param>
    /// <param name="delta">Изменение доли.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после изменения.</returns>
    private async Task ResizePanelAsync(
        LayoutPanelRowViewModel? panel,
        double delta,
        CancellationToken cancellationToken)
    {
        if (panel is null)
        {
            return;
        }

        await RunAsync(
            () => _layouts.ResizePanelAsync(panel.Id, panel.Width + delta, cancellationToken),
            "Изменение размера панели",
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Выполняет изменение макета и перечитывает его.
    /// </summary>
    /// <param name="change">Изменение.</param>
    /// <param name="title">Заголовок сообщения об ошибке.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><see langword="true"/>, если изменение удалось.</returns>
    private async Task<bool> RunAsync(
        Func<Task<Shared.Results.Result>> change,
        string title,
        CancellationToken cancellationToken)
    {
        var result = await change().ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync(title, result.Error!).ConfigureAwait(true);
            return false;
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(true);

        return true;
    }

    /// <summary>
    /// Перечитывает вкладки и панели открытого макета.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    private async Task ReloadLayoutAsync(CancellationToken cancellationToken)
    {
        if (SelectedLayout is not { } selected)
        {
            Tabs.Clear();
            Summary = string.Empty;
            _openLayoutId = null;

            return;
        }

        IsBusy = true;

        try
        {
            var result = await _layouts.GetAsync(selected.Id, cancellationToken).ConfigureAwait(true);

            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync("Макет", result.Error!).ConfigureAwait(true);
                return;
            }

            var layout = result.Value;
            var openTab = SelectedTab?.Id;

            Tabs.Clear();

            foreach (var tab in layout.Tabs)
            {
                Tabs.Add(new LayoutTabRowViewModel(tab));
            }

            SelectedTab = openTab is { } id
                ? Tabs.FirstOrDefault(tab => tab.Id == id) ?? Tabs.FirstOrDefault()
                : Tabs.FirstOrDefault();

            LayoutName = layout.Name;
            _openLayoutId = layout.Id;

            Summary = layout.IsDefault
                ? $"Применён к листу персонажа · вкладок: {layout.Tabs.Count}"
                : $"Вкладок: {layout.Tabs.Count}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Открывает выбранный макет.
    /// </summary>
    /// <param name="value">Выбранный макет.</param>
    partial void OnSelectedLayoutChanged(LayoutListItem? value)
    {
        OnPropertyChanged(nameof(IsLayoutOpen));

        // Обновление списка заменяет строку макета новой — с теми же вкладками.
        // Перечитывать его в этом случае незачем.
        if (value is not null && value.Id == _openLayoutId)
        {
            return;
        }

        _ = ReloadLayoutAsync(CancellationToken.None);
    }
}
