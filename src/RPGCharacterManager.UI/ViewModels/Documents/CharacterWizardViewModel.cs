using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;
using RPGCharacterManager.UI.ViewModels.Characters;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Документ «Создание персонажа»: пошаговый мастер.
///
/// Мастер не содержит перечня своих страниц: они создаются по описаниям шагов,
/// полученным от подсистемы персонажей. Новый шаг появляется в мастере сразу
/// после регистрации своего описания — изменять это окно не требуется.
/// </summary>
public sealed partial class CharacterWizardViewModel : DocumentViewModelBase
{
    private readonly ICharacterBuilderService _builder;
    private readonly IFormulaEngine _formulas;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly IBackgroundTaskService _backgroundTasks;

    private readonly CharacterDraft _draft = new();

    // Отслеживает последний шаг, с которого переход был разрешён: не даёт
    // перескочить вперёд через список шагов слева в обход проверки CanLeave
    // (двусторонняя привязка SelectedItem иначе позволяет это без проверок).
    private WizardStepViewModel? _lastAllowedStep;

    // Шаг, на события которого сейчас подписан мастер — чтобы отписаться от
    // прежнего шага при переходе и не копить обработчики.
    private WizardStepViewModel? _observedStep;

    [ObservableProperty]
    private WizardStepViewModel? _currentStep;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private bool _hasChanges;

    /// <summary>
    /// Создаёт модель представления мастера создания персонажа.
    /// </summary>
    /// <param name="builder">Мастер создания персонажа.</param>
    /// <param name="formulas">Единый движок вычислений.</param>
    /// <param name="navigation">Служба навигации.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    /// <param name="notifications">Служба всплывающих уведомлений.</param>
    /// <param name="backgroundTasks">Служба фоновых задач.</param>
    public CharacterWizardViewModel(
        ICharacterBuilderService builder,
        IFormulaEngine formulas,
        INavigationService navigation,
        IDialogService dialogs,
        INotificationService notifications,
        IBackgroundTaskService backgroundTasks)
        : base(CharacterShellContributor.WizardDocumentId, "Создание персонажа")
    {
        _builder = Guard.NotNull(builder);
        _formulas = Guard.NotNull(formulas);
        _navigation = Guard.NotNull(navigation);
        _dialogs = Guard.NotNull(dialogs);
        _notifications = Guard.NotNull(notifications);
        _backgroundTasks = Guard.NotNull(backgroundTasks);

        foreach (var definition in _builder.Steps)
        {
            Steps.Add(CreateStep(definition));
        }
    }

    /// <summary>Страницы мастера в порядке прохождения.</summary>
    public ObservableCollection<WizardStepViewModel> Steps { get; } = [];

    /// <summary>Возврат на предыдущий шаг возможен.</summary>
    public bool CanGoBack => CurrentIndex > 0;

    /// <summary>Переход на следующий шаг возможен.</summary>
    public bool CanGoForward =>
        CurrentIndex >= 0
        && CurrentIndex < Steps.Count - 1
        && (CurrentStep?.CanLeave ?? true);

    /// <summary>Открыт последний шаг мастера.</summary>
    public bool IsLastStep => CurrentIndex == Steps.Count - 1;

    private int CurrentIndex => CurrentStep is null ? -1 : Steps.IndexOf(CurrentStep);

    /// <inheritdoc />
    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Steps.Count > 0)
        {
            // Присваивание сразу загружает данные шага: страница обязана
            // подготовиться независимо от того, как пользователь на неё перешёл.
            CurrentStep = Steps[0];
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
    {
        if (!HasChanges)
        {
            return true;
        }

        return await _dialogs.ShowConfirmationAsync(
                "Незавершённое создание",
                "Персонаж ещё не создан. Закрыть мастер и потерять сделанный выбор?")
            .ConfigureAwait(true);
    }

    /// <summary>
    /// Переходит на следующий шаг.
    /// </summary>
    [RelayCommand]
    private void GoForward()
    {
        if (CanGoForward)
        {
            CurrentStep = Steps[CurrentIndex + 1];
        }
    }

    /// <summary>
    /// Возвращается на предыдущий шаг.
    /// </summary>
    [RelayCommand]
    private void GoBack()
    {
        if (CanGoBack)
        {
            CurrentStep = Steps[CurrentIndex - 1];
        }
    }

    /// <summary>
    /// Создаёт персонажа по сделанному выбору.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после создания.</returns>
    [RelayCommand]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        var result = await _backgroundTasks
            .RunAsync(
                "Создание персонажа",
                token => _builder.CreateAsync(_draft, token),
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs
                .ShowErrorAsync("Создание персонажа", "Персонаж не создан.", result.Error)
                .ConfigureAwait(true);

            return;
        }

        HasChanges = false;
        _notifications.Show($"Персонаж «{_draft.Character.Name}» создан", NotificationKind.Success);

        await _navigation.OpenAsync(CharacterShellContributor.ListDocumentId, null, cancellationToken)
            .ConfigureAwait(true);

        await _navigation.CloseAsync(this, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Готовит открытую страницу мастера.
    ///
    /// Загрузка выполняется здесь, а не в командах перехода: пользователь может
    /// открыть шаг и кнопкой «Далее», и щелчком по списку шагов слева, и переходом
    /// из списка замечаний — во всех случаях страница обязана подготовиться.
    /// </summary>
    /// <param name="value">Открытая страница.</param>
    ///
    /// <remarks>
    /// Список шагов слева привязан к <see cref="CurrentStep"/> двусторонне, поэтому
    /// щелчок по шагу впереди меняет страницу напрямую, минуя команду «Далее» и её
    /// проверку <see cref="CanGoForward"/>. Без проверки здесь пользователь мог
    /// перейти сразу на страницу рас или классов, не выбрав игровую систему, и
    /// получить пустые списки без всякого объяснения причины — поэтому такой
    /// прыжок вперёд с шага, который нельзя покинуть, откатывается обратно.
    /// </remarks>
    partial void OnCurrentStepChanged(WizardStepViewModel? value)
    {
        if (_lastAllowedStep is not null && !_lastAllowedStep.CanLeave)
        {
            var previousIndex = Steps.IndexOf(_lastAllowedStep);
            var targetIndex = value is null ? -1 : Steps.IndexOf(value);

            if (targetIndex > previousIndex)
            {
                // Поле уже содержит запрещённое значение, поэтому повторное
                // присваивание не будет проигнорировано как отсутствие
                // изменений — рекурсивный вызов этого же метода корректно
                // восстановит страницу и все зависящие от неё состояния.
                CurrentStep = _lastAllowedStep;
                return;
            }
        }

        _lastAllowedStep = value;

        if (_observedStep is not null)
        {
            _observedStep.PropertyChanged -= OnCurrentStepPropertyChanged;
        }

        _observedStep = value;

        if (_observedStep is not null)
        {
            _observedStep.PropertyChanged += OnCurrentStepPropertyChanged;
        }

        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        OnPropertyChanged(nameof(IsLastStep));

        ProgressText = value is null
            ? string.Empty
            : string.Create(
                CultureInfo.CurrentCulture,
                $"Шаг {CurrentIndex + 1} из {Steps.Count}: {value.Title}");

        if (value is not null)
        {
            _ = value.ActivateAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Обновляет доступность перехода вперёд, когда открытая страница сама
    /// сообщает об изменении своего <see cref="WizardStepViewModel.CanLeave"/>
    /// (например, пользователь только что выбрал игровую систему).
    /// </summary>
    private void OnCurrentStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WizardStepViewModel.CanLeave), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(CanGoForward));
        }
    }

    /// <summary>
    /// Создаёт страницу мастера по описанию шага.
    /// </summary>
    /// <param name="definition">Описание шага.</param>
    /// <returns>Страница мастера.</returns>
    private WizardStepViewModel CreateStep(CharacterStepDefinition definition) => definition.Kind switch
    {
        CharacterStepKind.GameSystem =>
            new GameSystemStepViewModel(definition, _draft, _builder, MarkChanged),

        CharacterStepKind.Fields =>
            new FieldsStepViewModel(definition, _draft, _builder, MarkChanged),

        CharacterStepKind.SingleChoice =>
            new SingleChoiceStepViewModel(definition, _draft, _builder, MarkChanged),

        CharacterStepKind.MultipleChoice =>
            new MultipleChoiceStepViewModel(definition, _draft, _builder, MarkChanged),

        CharacterStepKind.Attributes =>
            new AttributesStepViewModel(definition, _draft, _builder, _formulas, MarkChanged),

        _ => new SummaryStepViewModel(definition, _draft, _builder, MarkChanged, GoToStepById),
    };

    private void MarkChanged() => HasChanges = true;

    private void GoToStepById(string stepId)
    {
        var step = Steps.FirstOrDefault(item =>
            string.Equals(item.Definition.Id, stepId, StringComparison.Ordinal));

        if (step is not null)
        {
            CurrentStep = step;
        }
    }
}
