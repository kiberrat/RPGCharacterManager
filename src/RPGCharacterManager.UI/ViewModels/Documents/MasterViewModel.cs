using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.History;
using RPGCharacterManager.Core.Abstractions.Master;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Документ «Мастер»: ведение игровой сессии за всех персонажей сразу.
///
/// Окно не знает ни одной игровой механики. «Урон» здесь — уменьшение ресурса,
/// выбранного мастером в списке, потому что хиты в этом приложении такой же
/// ресурс, как мана или заряды. Очередь хода показывается только тогда, когда
/// игровая система задала формулу инициативы.
/// </summary>
public sealed partial class MasterViewModel : DocumentViewModelBase
{
    /// <summary>Пункт отбора «все персонажи».</summary>
    public static readonly MasterOption AllCampaigns = new(Guid.Empty, "Все персонажи");

    /// <summary>Сколько записей журнала показывает раздел.</summary>
    public const int JournalLimit = 60;

    private readonly IMasterService _master;
    private readonly IHistoryService _history;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;

    [ObservableProperty]
    private MasterOption? _selectedCampaign;

    [ObservableProperty]
    private MasterOption? _selectedResource;

    [ObservableProperty]
    private MasterOption? _selectedEffect;

    [ObservableProperty]
    private string _effectSearch = string.Empty;

    [ObservableProperty]
    private string _amount = "1";

    [ObservableProperty]
    private string _initiativeHint = string.Empty;

    [ObservableProperty]
    private bool _isInitiativeEnabled;

    [ObservableProperty]
    private bool _isCombatStarted;

    [ObservableProperty]
    private string _initiativeFormula = string.Empty;

    [ObservableProperty]
    private int _round = 1;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Создаёт модель представления режима мастера.
    /// </summary>
    /// <param name="master">Служба режима мастера.</param>
    /// <param name="history">Журнал событий.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    /// <param name="notifications">Служба всплывающих уведомлений.</param>
    public MasterViewModel(
        IMasterService master,
        IHistoryService history,
        IDialogService dialogs,
        INotificationService notifications)
        : base(MasterShellContributor.BoardDocumentId, "Мастер")
    {
        _master = Guard.NotNull(master);
        _history = Guard.NotNull(history);
        _dialogs = Guard.NotNull(dialogs);
        _notifications = Guard.NotNull(notifications);

        Campaigns.Add(AllCampaigns);
        _selectedCampaign = AllCampaigns;
    }

    /// <summary>Персонажи сводки.</summary>
    public ObservableCollection<MasterRowViewModel> Rows { get; } = [];

    /// <summary>Кампании для отбора персонажей.</summary>
    public ObservableCollection<MasterOption> Campaigns { get; } = [];

    /// <summary>Ресурсы показанных персонажей.</summary>
    public ObservableCollection<MasterOption> Resources { get; } = [];

    /// <summary>Эффекты, доступные для наложения.</summary>
    public ObservableCollection<MasterOption> Effects { get; } = [];

    /// <summary>Общий журнал показанных персонажей.</summary>
    public ObservableCollection<HistoryRecord> Journal { get; } = [];

    /// <summary>Показывать нечего.</summary>
    public bool IsEmpty => Rows.Count == 0;

    /// <summary>Журнал пуст.</summary>
    public bool IsJournalEmpty => Journal.Count == 0;

    /// <summary>Выбран хотя бы один персонаж.</summary>
    public bool HasSelection => Rows.Any(row => row.IsSelected);

    /// <inheritdoc />
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
        await SearchEffectsAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Перечитывает сводку, журнал и списки выбора.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            var result = await _master.GetBoardAsync(CampaignFilter(), cancellationToken).ConfigureAwait(true);

            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync("Режим мастера", result.Error!).ConfigureAwait(true);
                return;
            }

            var board = result.Value;
            var selected = Rows.Where(row => row.IsSelected).Select(row => row.Id).ToHashSet();

            Rows.Clear();

            foreach (var character in board.Characters)
            {
                Rows.Add(new MasterRowViewModel(character)
                {
                    IsSelected = selected.Contains(character.Id),
                });
            }

            UpdateCampaigns(board.Campaigns);
            UpdateResources(board.Resources);

            IsInitiativeEnabled = board.Initiative.IsEnabled;
            IsCombatStarted = board.Initiative.IsStarted;
            InitiativeFormula = board.Initiative.Formula ?? string.Empty;
            InitiativeHint = board.Initiative.DisabledReason ?? string.Empty;
            Round = board.Initiative.Round;

            Summary = board.IsEmpty
                ? "Персонажей нет"
                : $"Персонажей: {board.Characters.Count}";

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasSelection));

            await ReloadJournalAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Загружает эффекты, подходящие под строку поиска.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    [RelayCommand]
    private async Task SearchEffectsAsync(CancellationToken cancellationToken)
    {
        var found = await _master.GetEffectsAsync(EffectSearch, cancellationToken).ConfigureAwait(true);
        var previous = SelectedEffect?.Id;

        Effects.Clear();

        foreach (var effect in found)
        {
            Effects.Add(effect);
        }

        SelectedEffect = previous is { } id
            ? Effects.FirstOrDefault(effect => effect.Id == id) ?? Effects.FirstOrDefault()
            : Effects.FirstOrDefault();
    }

    /// <summary>
    /// Отмечает всех показанных персонажей.
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        foreach (var row in Rows)
        {
            row.IsSelected = true;
        }

        OnPropertyChanged(nameof(HasSelection));
    }

    /// <summary>
    /// Снимает отметки со всех персонажей.
    /// </summary>
    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var row in Rows)
        {
            row.IsSelected = false;
        }

        OnPropertyChanged(nameof(HasSelection));
    }

    /// <summary>
    /// Отнимает величину у выбранного ресурса: урон, расход, потеря.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после изменения.</returns>
    [RelayCommand]
    private Task SubtractAsync(CancellationToken cancellationToken) =>
        ChangeResourceAsync(-1, cancellationToken);

    /// <summary>
    /// Прибавляет величину к выбранному ресурсу: лечение, восстановление.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после изменения.</returns>
    [RelayCommand]
    private Task AddAsync(CancellationToken cancellationToken) =>
        ChangeResourceAsync(1, cancellationToken);

    /// <summary>
    /// Накладывает выбранный эффект на отмеченных персонажей.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после наложения.</returns>
    [RelayCommand]
    private async Task ApplyEffectAsync(CancellationToken cancellationToken)
    {
        if (SelectedEffect is not { } effect || !TryGetSelection(out var ids))
        {
            return;
        }

        var result = await _master.ApplyEffectAsync(ids, effect.Id, cancellationToken).ConfigureAwait(true);
        await ReportAsync($"Наложение «{effect.Name}»", result, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Снимает выбранный эффект с отмеченных персонажей.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после снятия.</returns>
    [RelayCommand]
    private async Task RemoveEffectAsync(CancellationToken cancellationToken)
    {
        if (SelectedEffect is not { } effect || !TryGetSelection(out var ids))
        {
            return;
        }

        var result = await _master.RemoveEffectAsync(ids, effect.Id, cancellationToken).ConfigureAwait(true);
        await ReportAsync($"Снятие «{effect.Name}»", result, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Бросает инициативу отмеченным персонажам и начинает бой.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после броска.</returns>
    [RelayCommand]
    private async Task RollInitiativeAsync(CancellationToken cancellationToken)
    {
        if (!TryGetSelection(out var ids))
        {
            return;
        }

        var result = await _master
            .RollInitiativeAsync(CampaignFilter(), ids, cancellationToken).ConfigureAwait(true);

        await ReportAsync("Инициатива", result, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Передаёт ход следующему участнику очереди.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после передачи хода.</returns>
    [RelayCommand]
    private async Task NextTurnAsync(CancellationToken cancellationToken)
    {
        var result = await _master.NextTurnAsync(CampaignFilter(), cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Очередь хода", result.Error!).ConfigureAwait(true);
            return;
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Завершает бой и очищает очередь хода.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после завершения боя.</returns>
    [RelayCommand]
    private async Task EndCombatAsync(CancellationToken cancellationToken)
    {
        var result = await _master.EndCombatAsync(CampaignFilter(), cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Завершение боя", result.Error!).ConfigureAwait(true);
            return;
        }

        _notifications.Show("Бой завершён", NotificationKind.Success);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Сохраняет значение инициативы, введённое мастером вручную.
    /// </summary>
    /// <param name="row">Строка сводки.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    [RelayCommand]
    private async Task SaveInitiativeAsync(MasterRowViewModel? row, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return;
        }

        if (!row.TryReadInitiative(out var value))
        {
            await _dialogs
                .ShowErrorAsync("Инициатива", "Введите число: например, 17.")
                .ConfigureAwait(true);

            return;
        }

        var result = await _master
            .SetInitiativeAsync(CampaignFilter(), row.Id, value, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Инициатива", result.Error!).ConfigureAwait(true);
            return;
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Изменяет выбранный ресурс у отмеченных персонажей.
    /// </summary>
    /// <param name="sign">Знак изменения: −1 отнимает, +1 прибавляет.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после изменения.</returns>
    private async Task ChangeResourceAsync(int sign, CancellationToken cancellationToken)
    {
        if (SelectedResource is not { } resource || !TryGetSelection(out var ids))
        {
            return;
        }

        if (!double.TryParse(Amount, NumberStyles.Float, CultureInfo.CurrentCulture, out var amount)
            || amount <= 0)
        {
            await _dialogs
                .ShowErrorAsync("Изменение ресурса", "Введите положительное число: например, 7.")
                .ConfigureAwait(true);

            return;
        }

        var result = await _master
            .ChangeResourceAsync(ids, resource.Id, sign * amount, cancellationToken).ConfigureAwait(true);

        var action = sign < 0 ? "Отнято" : "Прибавлено";
        await ReportAsync($"{action}: «{resource.Name}»", result, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Показывает итог массового действия и перечитывает сводку.
    /// </summary>
    /// <param name="title">Заголовок сообщения.</param>
    /// <param name="result">Итог действия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после обновления.</returns>
    private async Task ReportAsync(
        string title,
        Shared.Results.Result<MassResult> result,
        CancellationToken cancellationToken)
    {
        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync(title, result.Error!).ConfigureAwait(true);
            return;
        }

        var report = result.Value;

        // Отказ по части персонажей не отменяет остальных, поэтому мастеру
        // показывается и сделанное, и несделанное — одним сообщением.
        if (report.IsComplete)
        {
            _notifications.Show($"{title}: {report.Changed}", NotificationKind.Success);
        }
        else
        {
            _notifications.Show(
                $"{title}: {report.Changed}. Без изменений: {string.Join(" ", report.Failures)}",
                report.Changed > 0 ? NotificationKind.Warning : NotificationKind.Error);
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Перечитывает общий журнал показанных персонажей.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    private async Task ReloadJournalAsync(CancellationToken cancellationToken)
    {
        // Без отбора по кампании журнал общий: показываются события всех
        // персонажей и события, ни с кем не связанные.
        var characters = CampaignFilter() is null
            ? null
            : Rows.Select(row => row.Id).ToList();

        var page = await _history
            .GetAsync(new HistoryQuery(characters, Limit: JournalLimit), cancellationToken)
            .ConfigureAwait(true);

        Journal.Clear();

        if (page.IsSuccess)
        {
            foreach (var record in page.Value.Records)
            {
                Journal.Add(record);
            }
        }

        OnPropertyChanged(nameof(IsJournalEmpty));
    }

    /// <summary>
    /// Обновляет список кампаний, сохраняя выбранную.
    /// </summary>
    /// <param name="campaigns">Кампании из сводки.</param>
    private void UpdateCampaigns(IReadOnlyList<MasterOption> campaigns)
    {
        var previous = SelectedCampaign?.Id;

        Campaigns.Clear();
        Campaigns.Add(AllCampaigns);

        foreach (var campaign in campaigns)
        {
            Campaigns.Add(campaign);
        }

        SelectedCampaign = Campaigns.FirstOrDefault(campaign => campaign.Id == previous) ?? AllCampaigns;
    }

    /// <summary>
    /// Обновляет список ресурсов, сохраняя выбранный.
    /// </summary>
    /// <param name="resources">Ресурсы из сводки.</param>
    private void UpdateResources(IReadOnlyList<MasterOption> resources)
    {
        var previous = SelectedResource?.Id;

        Resources.Clear();

        foreach (var resource in resources)
        {
            Resources.Add(resource);
        }

        // Первый ресурс — обычно здоровье: он стоит первым в порядке отображения,
        // заданном самим пользователем.
        SelectedResource = Resources.FirstOrDefault(resource => resource.Id == previous)
            ?? Resources.FirstOrDefault();
    }

    /// <summary>
    /// Возвращает отбор по кампании.
    /// </summary>
    /// <returns>Кампания либо <see langword="null"/> для всех персонажей.</returns>
    private Guid? CampaignFilter() =>
        SelectedCampaign is { } campaign && campaign.Id != Guid.Empty ? campaign.Id : null;

    /// <summary>
    /// Собирает отмеченных персонажей.
    /// </summary>
    /// <param name="ids">Идентификаторы отмеченных персонажей.</param>
    /// <returns><see langword="true"/>, если отмечен хотя бы один персонаж.</returns>
    private bool TryGetSelection(out List<Guid> ids)
    {
        ids = Rows.Where(row => row.IsSelected).Select(row => row.Id).ToList();

        if (ids.Count == 0)
        {
            _notifications.Show("Отметьте персонажей", NotificationKind.Warning);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Перечитывает сводку при смене кампании.
    /// </summary>
    /// <param name="value">Выбранная кампания.</param>
    partial void OnSelectedCampaignChanged(MasterOption? value)
    {
        if (value is not null && !IsBusy)
        {
            _ = RefreshAsync(CancellationToken.None);
        }
    }
}
