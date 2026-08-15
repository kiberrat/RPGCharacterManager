using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Документ «Персонажи»: список созданных персонажей и действия над ними.
/// </summary>
public sealed partial class CharactersViewModel : DocumentViewModelBase, IDisposable
{
    /// <summary>Количество персонажей, загружаемых в список за один раз.</summary>
    public const int PageSize = 200;

    private readonly ICharacterService _characters;
    private readonly ICharacterProgressionService _progression;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly IBackgroundTaskService _backgroundTasks;
    private readonly IDisposable _characterSubscription;

    [ObservableProperty]
    private CharacterListItem? _selectedCharacter;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _listSummary = string.Empty;

    [ObservableProperty]
    private string _lastReport = string.Empty;

    /// <summary>
    /// Создаёт модель представления списка персонажей.
    /// </summary>
    /// <param name="characters">Служба персонажей.</param>
    /// <param name="progression">Служба развития персонажа.</param>
    /// <param name="navigation">Служба навигации.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    /// <param name="notifications">Служба всплывающих уведомлений.</param>
    /// <param name="backgroundTasks">Служба фоновых задач.</param>
    /// <param name="eventBus">Шина событий приложения.</param>
    /// <param name="dispatcher">Диспетчер потока интерфейса.</param>
    public CharactersViewModel(
        ICharacterService characters,
        ICharacterProgressionService progression,
        INavigationService navigation,
        IDialogService dialogs,
        INotificationService notifications,
        IBackgroundTaskService backgroundTasks,
        IEventBus eventBus,
        IUiDispatcher dispatcher)
        : base(CharacterShellContributor.ListDocumentId, "Персонажи")
    {
        _characters = Guard.NotNull(characters);
        _progression = Guard.NotNull(progression);
        _navigation = Guard.NotNull(navigation);
        _dialogs = Guard.NotNull(dialogs);
        _notifications = Guard.NotNull(notifications);
        _backgroundTasks = Guard.NotNull(backgroundTasks);

        Guard.NotNull(eventBus);

        // Список обновляется, когда персонажа создаёт мастер или изменяет другой
        // раздел: разделы приложения не знают друг о друге и связаны только шиной.
        _characterSubscription = eventBus.SubscribeOnUiThread<CharacterChangedEvent>(
            dispatcher,
            OnCharacterChanged);
    }

    /// <inheritdoc />
    public void Dispose() => _characterSubscription.Dispose();

    /// <summary>Созданные персонажи.</summary>
    public ObservableCollection<CharacterListItem> Characters { get; } = [];

    /// <summary>Список персонажей пуст.</summary>
    public bool IsEmpty => Characters.Count == 0;

    /// <summary>Персонаж выбран.</summary>
    public bool HasSelection => SelectedCharacter is not null;

    /// <summary>Отчёт о последней операции присутствует.</summary>
    public bool HasReport => !string.IsNullOrWhiteSpace(LastReport);

    /// <inheritdoc />
    public override Task InitializeAsync(CancellationToken cancellationToken = default) =>
        ReloadAsync(cancellationToken);

    /// <summary>
    /// Перечитывает список персонажей.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    [RelayCommand]
    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        var page = await _characters
            .SearchAsync(SearchText, 0, PageSize, cancellationToken)
            .ConfigureAwait(true);

        var selectedId = SelectedCharacter?.Id;

        Characters.Clear();

        foreach (var character in page.Items)
        {
            Characters.Add(character);
        }

        SelectedCharacter = Characters.FirstOrDefault(character => character.Id == selectedId);

        ListSummary = page.TotalCount > page.Items.Count
            ? string.Create(
                CultureInfo.CurrentCulture,
                $"Показано {page.Items.Count} из {page.TotalCount}. Уточните поиск, чтобы увидеть остальных.")
            : string.Create(CultureInfo.CurrentCulture, $"Персонажей: {page.TotalCount}");

        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// Открывает мастер создания персонажа.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после открытия мастера.</returns>
    [RelayCommand]
    private async Task CreateAsync(CancellationToken cancellationToken) =>
        await _navigation
            .OpenAsync(CharacterShellContributor.WizardDocumentId, null, cancellationToken)
            .ConfigureAwait(true);

    /// <summary>
    /// Открывает лист выбранного персонажа.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после открытия листа.</returns>
    [RelayCommand]
    private async Task OpenSheetAsync(CancellationToken cancellationToken)
    {
        if (SelectedCharacter is not { } character)
        {
            return;
        }

        await _navigation
            .OpenAsync(CharacterShellContributor.SheetDocumentId, character.Id, cancellationToken)
            .ConfigureAwait(true);
    }

    /// <summary>
    /// Повышает уровень выбранного персонажа.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после повышения уровня.</returns>
    [RelayCommand]
    private async Task LevelUpAsync(CancellationToken cancellationToken)
    {
        if (SelectedCharacter is not { } character)
        {
            return;
        }

        var report = await _backgroundTasks
            .RunAsync(
                "Повышение уровня",
                token => _progression.LevelUpAsync(character.Id, 1, token),
                cancellationToken)
            .ConfigureAwait(true);

        await ShowReportAsync(report, "Повышение уровня", cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Пересчитывает параметры выбранного персонажа по текущим формулам и правилам.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после пересчёта.</returns>
    [RelayCommand]
    private async Task RecalculateAsync(CancellationToken cancellationToken)
    {
        if (SelectedCharacter is not { } character)
        {
            return;
        }

        var report = await _backgroundTasks
            .RunAsync(
                "Пересчёт персонажа",
                token => _progression.RecalculateAsync(character.Id, token),
                cancellationToken)
            .ConfigureAwait(true);

        await ShowReportAsync(report, "Пересчёт персонажа", cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Удаляет выбранного персонажа.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (SelectedCharacter is not { } character)
        {
            return;
        }

        var confirmed = await _dialogs
            .ShowConfirmationAsync(
                "Удаление персонажа",
                $"Удалить персонажа «{character.Name}»? Действие нельзя отменить.")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var result = await _characters.DeleteAsync(character.Id, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs
                .ShowErrorAsync("Удаление персонажа", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);

            return;
        }

        _notifications.Show($"Персонаж «{character.Name}» удалён", NotificationKind.Success);
        LastReport = string.Empty;

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Перечитывает список после изменения персонажа в другом разделе.
    /// </summary>
    /// <param name="payload">Сведения о произошедшем изменении.</param>
    private void OnCharacterChanged(CharacterChangedEvent payload) =>
        _ = ReloadAsync(CancellationToken.None);

    partial void OnSearchTextChanged(string value) => _ = ReloadAsync(CancellationToken.None);

    partial void OnSelectedCharacterChanged(CharacterListItem? value) =>
        OnPropertyChanged(nameof(HasSelection));

    partial void OnLastReportChanged(string value) => OnPropertyChanged(nameof(HasReport));

    /// <summary>
    /// Показывает отчёт об изменении персонажа и обновляет список.
    /// </summary>
    /// <param name="report">Результат операции.</param>
    /// <param name="title">Заголовок сообщения об ошибке.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после обновления списка.</returns>
    private async Task ShowReportAsync(
        Shared.Results.Result<CharacterUpdateReport> report,
        string title,
        CancellationToken cancellationToken)
    {
        if (report.IsFailure)
        {
            await _dialogs
                .ShowErrorAsync(title, report.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);

            return;
        }

        LastReport = DescribeReport(report.Value);
        _notifications.Show(BuildNotification(report.Value), NotificationKind.Success);

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    private static string BuildNotification(CharacterUpdateReport report) =>
        report.CurrentLevel > report.PreviousLevel
            ? $"«{report.CharacterName}»: уровень {report.CurrentLevel.ToString(CultureInfo.CurrentCulture)}"
            : $"«{report.CharacterName}»: параметры пересчитаны";

    /// <summary>
    /// Составляет описание произошедших изменений для отображения пользователю.
    /// </summary>
    /// <param name="report">Отчёт об изменении персонажа.</param>
    /// <returns>Текст отчёта.</returns>
    private static string DescribeReport(CharacterUpdateReport report)
    {
        var lines = new List<string>();

        if (report.CurrentLevel > report.PreviousLevel)
        {
            lines.Add(
                $"Уровень: {report.PreviousLevel.ToString(CultureInfo.CurrentCulture)}"
                + $" → {report.CurrentLevel.ToString(CultureInfo.CurrentCulture)}");
        }

        lines.AddRange(report.Changes);

        if (report.AppliedRules.Count > 0)
        {
            lines.Add("Применённые правила: " + string.Join(", ", report.AppliedRules.Distinct()));
        }

        lines.AddRange(report.Issues.Select(issue => $"Замечание: {issue.Message}"));

        return lines.Count > 0
            ? string.Join(Environment.NewLine, lines)
            : "Изменений не потребовалось: все значения уже соответствуют формулам и правилам.";
    }
}
