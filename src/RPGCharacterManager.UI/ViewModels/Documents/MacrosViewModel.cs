using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Macros;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;
using RPGCharacterManager.UI.ViewModels.Rules;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Документ «Макросы»: последовательности действий, запускаемые вручную.
///
/// Условия и действия правит теми же редакторами, что и конструктор правил:
/// у макроса и правила одинаковый состав, и заводить для него второй редактор
/// значило бы иметь два разных конструктора одного и того же (решение Р-97).
/// </summary>
public sealed partial class MacrosViewModel : DocumentViewModelBase
{
    /// <summary>Сколько персонажей показывает список выбора.</summary>
    public const int CharacterLimit = 200;

    private readonly IMacroService _macros;
    private readonly ICharacterService _characters;
    private readonly IRuleEngine _ruleEngine;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;

    private bool _isLoading;

    [ObservableProperty]
    private MacroListItem? _selectedMacro;

    [ObservableProperty]
    private CharacterListItem? _selectedCharacter;

    [ObservableProperty]
    private IRuleActionHandler? _selectedActionKind;

    [ObservableProperty]
    private ConditionNodeViewModel? _conditionRoot;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _hotkey = string.Empty;

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Создаёт модель представления макросов.
    /// </summary>
    /// <param name="macros">Служба макросов.</param>
    /// <param name="characters">Служба персонажей: над кем выполнять макрос.</param>
    /// <param name="ruleEngine">Движок правил: перечень доступных действий.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    /// <param name="notifications">Служба всплывающих уведомлений.</param>
    public MacrosViewModel(
        IMacroService macros,
        ICharacterService characters,
        IRuleEngine ruleEngine,
        IDialogService dialogs,
        INotificationService notifications)
        : base(MacroShellContributor.ListDocumentId, "Макросы")
    {
        _macros = Guard.NotNull(macros);
        _characters = Guard.NotNull(characters);
        _ruleEngine = Guard.NotNull(ruleEngine);
        _dialogs = Guard.NotNull(dialogs);
        _notifications = Guard.NotNull(notifications);

        AvailableActionKinds = [.. _ruleEngine.ActionHandlers
            .OrderBy(handler => handler.DisplayName, StringComparer.CurrentCulture)];

        _selectedActionKind = AvailableActionKinds.Count > 0 ? AvailableActionKinds[0] : null;
    }

    /// <summary>Макросы в порядке отображения.</summary>
    public ObservableCollection<MacroListItem> Macros { get; } = [];

    /// <summary>Персонажи, над которыми можно выполнить макрос.</summary>
    public ObservableCollection<CharacterListItem> Characters { get; } = [];

    /// <summary>Действия открытого макроса в порядке выполнения.</summary>
    public ObservableCollection<ActionEditorViewModel> Actions { get; } = [];

    /// <summary>Виды действий, доступные движку правил.</summary>
    public IReadOnlyList<IRuleActionHandler> AvailableActionKinds { get; }

    /// <summary>Макрос открыт для правки.</summary>
    public bool IsMacroOpen => SelectedMacro is not null;

    /// <summary>Макросов нет.</summary>
    public bool IsListEmpty => Macros.Count == 0;

    /// <inheritdoc />
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await LoadCharactersAsync(cancellationToken).ConfigureAwait(true);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Перечитывает список макросов, сохраняя открытый.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var result = await _macros.GetAllAsync(cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Макросы", result.Error!).ConfigureAwait(true);
            return;
        }

        var previous = SelectedMacro?.Id;

        Macros.Clear();

        foreach (var macro in result.Value)
        {
            Macros.Add(macro);
        }

        OnPropertyChanged(nameof(IsListEmpty));

        SelectedMacro = previous is { } id
            ? Macros.FirstOrDefault(macro => macro.Id == id)
            : Macros.FirstOrDefault();
    }

    /// <summary>
    /// Создаёт макрос и открывает его.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после создания.</returns>
    [RelayCommand]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        var draft = new MacroDefinition { Name = "Новый макрос" };
        var result = await _macros.SaveAsync(draft, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Новый макрос", result.Error!).ConfigureAwait(true);
            return;
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(true);

        SelectedMacro = Macros.FirstOrDefault(macro => macro.Id == result.Value);
        _notifications.Show("Макрос создан", NotificationKind.Success);
    }

    /// <summary>
    /// Сохраняет открытый макрос.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (SelectedMacro is not { } selected)
        {
            return;
        }

        var draft = new MacroDefinition
        {
            Id = selected.Id,
            Name = Name,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description,
            Category = string.IsNullOrWhiteSpace(Category) ? null : Category,
            Hotkey = string.IsNullOrWhiteSpace(Hotkey) ? null : Hotkey,
            Condition = ConditionRoot?.ToCondition(),
            Enabled = Enabled,
        };

        foreach (var action in Actions)
        {
            draft.Actions.Add(action.ToAction());
        }

        var result = await _macros.SaveAsync(draft, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Сохранение макроса", result.Error!).ConfigureAwait(true);
            return;
        }

        HasUnsavedChanges = false;
        _notifications.Show($"Макрос «{Name}» сохранён", NotificationKind.Success);

        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Удаляет открытый макрос.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (SelectedMacro is not { } macro)
        {
            return;
        }

        var confirmed = await _dialogs
            .ShowConfirmationAsync("Удаление макроса", $"Удалить макрос «{macro.Name}»?")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var result = await _macros.DeleteAsync(macro.Id, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Удаление макроса", result.Error!).ConfigureAwait(true);
            return;
        }

        SelectedMacro = null;
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Выполняет открытый макрос над выбранным персонажем.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после выполнения.</returns>
    [RelayCommand]
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        if (SelectedMacro is not { } macro)
        {
            return;
        }

        if (SelectedCharacter is not { } character)
        {
            _notifications.Show("Выберите персонажа", NotificationKind.Warning);
            return;
        }

        if (HasUnsavedChanges)
        {
            _notifications.Show("Сначала сохраните макрос", NotificationKind.Warning);
            return;
        }

        IsBusy = true;

        try
        {
            var result = await _macros
                .RunAsync(macro.Id, character.Id, cancellationToken).ConfigureAwait(true);

            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync("Выполнение макроса", result.Error!).ConfigureAwait(true);
                return;
            }

            var report = result.Value;

            Summary = report.HasChanges
                ? $"{report.Summary}: {string.Join("; ", report.Changes)}"
                : report.Summary;

            _notifications.Show(
                $"«{report.MacroName}» → {report.CharacterName}: {report.Summary}",
                report.WasConditionMet ? NotificationKind.Success : NotificationKind.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Добавляет в макрос действие выбранного вида.
    /// </summary>
    [RelayCommand]
    private void AddAction()
    {
        if (SelectedActionKind is not { } handler || !IsMacroOpen)
        {
            return;
        }

        Actions.Add(CreateActionViewModel(handler, action: null));
        MarkChanged();
    }

    /// <summary>
    /// Загружает персонажей для выбора.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    private async Task LoadCharactersAsync(CancellationToken cancellationToken)
    {
        var page = await _characters
            .SearchAsync(null, 0, CharacterLimit, cancellationToken).ConfigureAwait(true);

        Characters.Clear();

        foreach (var character in page.Items)
        {
            Characters.Add(character);
        }

        SelectedCharacter = Characters.FirstOrDefault();
    }

    /// <summary>
    /// Загружает открытый макрос в редактор.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    private async Task LoadMacroAsync(CancellationToken cancellationToken)
    {
        if (SelectedMacro is not { } selected)
        {
            Actions.Clear();
            ConditionRoot = null;
            Summary = string.Empty;
            HasUnsavedChanges = false;

            return;
        }

        _isLoading = true;

        try
        {
            var result = await _macros.GetAsync(selected.Id, cancellationToken).ConfigureAwait(true);

            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync("Макрос", result.Error!).ConfigureAwait(true);
                return;
            }

            var macro = result.Value;

            Name = macro.Name;
            Description = macro.Description ?? string.Empty;
            Category = macro.Category ?? string.Empty;
            Hotkey = macro.Hotkey ?? string.Empty;
            Enabled = macro.Enabled;
            Summary = string.Empty;

            ConditionRoot = ConditionNodeViewModel.FromCondition(macro.Condition, MarkChanged);

            Actions.Clear();

            foreach (var action in macro.Actions)
            {
                var handler = AvailableActionKinds
                    .FirstOrDefault(item => string.Equals(item.Kind, action.Kind, StringComparison.Ordinal));

                // Действие неизвестного вида пропускается: обработчик мог исчезнуть
                // вместе с подсистемой, и показать его нечем.
                if (handler is not null)
                {
                    Actions.Add(CreateActionViewModel(handler, action));
                }
            }

            HasUnsavedChanges = false;
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Создаёт редактор действия.
    /// </summary>
    /// <param name="handler">Обработчик вида действия.</param>
    /// <param name="action">Действие или <see langword="null"/> для нового.</param>
    /// <returns>Редактор действия.</returns>
    private ActionEditorViewModel CreateActionViewModel(IRuleActionHandler handler, RuleAction? action) =>
        new(handler, action, MarkChanged, RemoveAction, MoveAction);

    /// <summary>
    /// Убирает действие из макроса.
    /// </summary>
    /// <param name="action">Редактор действия.</param>
    private void RemoveAction(ActionEditorViewModel action)
    {
        Actions.Remove(action);
        MarkChanged();
    }

    /// <summary>
    /// Переставляет действие в порядке выполнения.
    /// </summary>
    /// <param name="action">Редактор действия.</param>
    /// <param name="offset">Смещение: −1 выше, +1 ниже.</param>
    private void MoveAction(ActionEditorViewModel action, int offset)
    {
        var index = Actions.IndexOf(action);
        var target = index + offset;

        if (index < 0 || target < 0 || target >= Actions.Count)
        {
            return;
        }

        Actions.Move(index, target);
        MarkChanged();
    }

    /// <summary>
    /// Отмечает наличие несохранённых изменений.
    /// </summary>
    private void MarkChanged()
    {
        if (!_isLoading)
        {
            HasUnsavedChanges = true;
        }
    }

    /// <summary>
    /// Загружает выбранный макрос.
    /// </summary>
    /// <param name="value">Выбранный макрос.</param>
    partial void OnSelectedMacroChanged(MacroListItem? value)
    {
        OnPropertyChanged(nameof(IsMacroOpen));

        _ = LoadMacroAsync(CancellationToken.None);
    }

    /// <summary>Отмечает изменение названия.</summary>
    /// <param name="value">Новое значение.</param>
    partial void OnNameChanged(string value) => MarkChanged();

    /// <summary>Отмечает изменение описания.</summary>
    /// <param name="value">Новое значение.</param>
    partial void OnDescriptionChanged(string value) => MarkChanged();

    /// <summary>Отмечает изменение категории.</summary>
    /// <param name="value">Новое значение.</param>
    partial void OnCategoryChanged(string value) => MarkChanged();

    /// <summary>Отмечает изменение сочетания клавиш.</summary>
    /// <param name="value">Новое значение.</param>
    partial void OnHotkeyChanged(string value) => MarkChanged();

    /// <summary>Отмечает включение или выключение макроса.</summary>
    /// <param name="value">Новое значение.</param>
    partial void OnEnabledChanged(bool value) => MarkChanged();
}
