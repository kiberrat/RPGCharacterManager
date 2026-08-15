using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Campaigns;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;
using RPGCharacterManager.UI.ViewModels.Content;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Документ «Кампании»: игры, их состав и хронология.
///
/// Состав кампании собирается из объектов, которые уже есть в приложении:
/// персонажей и контента любого вида. Поэтому в кампанию входят и монстры,
/// и квесты, и локации, и всё, что появится позже, — окно об этом не знает
/// и берёт перечень видов из каталога.
/// </summary>
public sealed partial class CampaignsViewModel : DocumentViewModelBase
{
    /// <summary>Сколько объектов показывает список выбора участника.</summary>
    public const int CandidateLimit = 50;

    private readonly ICampaignService _campaigns;
    private readonly ICampaignCatalog _catalog;
    private readonly IContentService _content;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;

    private Guid? _openCampaignId;

    [ObservableProperty]
    private CampaignListItem? _selectedCampaign;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _world = string.Empty;

    [ObservableProperty]
    private string _startDate = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private ContentReference? _selectedGameSystem;

    [ObservableProperty]
    private CampaignKind? _selectedKind;

    [ObservableProperty]
    private string _candidateSearch = string.Empty;

    [ObservableProperty]
    private CampaignObject? _selectedCandidate;

    [ObservableProperty]
    private string _newMemberRole = string.Empty;

    [ObservableProperty]
    private string _newEventTitle = string.Empty;

    [ObservableProperty]
    private string _newEventDate = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Создаёт модель представления кампаний.
    /// </summary>
    /// <param name="campaigns">Менеджер кампаний.</param>
    /// <param name="catalog">Каталог объектов, доступных кампании.</param>
    /// <param name="content">Служба контента: перечень игровых систем.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    /// <param name="notifications">Служба всплывающих уведомлений.</param>
    public CampaignsViewModel(
        ICampaignService campaigns,
        ICampaignCatalog catalog,
        IContentService content,
        IDialogService dialogs,
        INotificationService notifications)
        : base(CampaignShellContributor.ListDocumentId, "Кампании")
    {
        _campaigns = Guard.NotNull(campaigns);
        _catalog = Guard.NotNull(catalog);
        _content = Guard.NotNull(content);
        _dialogs = Guard.NotNull(dialogs);
        _notifications = Guard.NotNull(notifications);

        Kinds = _catalog.Kinds;
        SelectedKind = Kinds.Count > 0 ? Kinds[0] : null;
    }

    /// <summary>Кампании в порядке названий.</summary>
    public ObservableCollection<CampaignListItem> Campaigns { get; } = [];

    /// <summary>Игровые системы для выбора.</summary>
    public ObservableCollection<ContentReference> GameSystems { get; } = [];

    /// <summary>Виды объектов, которые можно добавить в состав.</summary>
    public IReadOnlyList<CampaignKind> Kinds { get; }

    /// <summary>Объекты выбранного вида, подходящие под строку поиска.</summary>
    public ObservableCollection<CampaignObject> Candidates { get; } = [];

    /// <summary>Состав кампании по видам объектов.</summary>
    public ObservableCollection<CampaignGroupViewModel> Groups { get; } = [];

    /// <summary>События хронологии в порядке следования.</summary>
    public ObservableCollection<CampaignEventRowViewModel> Events { get; } = [];

    /// <summary>Кампания выбрана и открыта.</summary>
    public bool IsCampaignOpen => SelectedCampaign is not null;

    /// <summary>Кампаний нет.</summary>
    public bool IsListEmpty => Campaigns.Count == 0;

    /// <summary>Состав кампании пуст.</summary>
    public bool IsRosterEmpty => Groups.Count == 0;

    /// <summary>Хронология кампании пуста.</summary>
    public bool IsTimelineEmpty => Events.Count == 0;

    /// <inheritdoc />
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await LoadGameSystemsAsync(cancellationToken).ConfigureAwait(true);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Перечитывает список кампаний, сохраняя выбранную.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var result = await _campaigns.GetAllAsync(cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Кампании", result.Error!).ConfigureAwait(true);
            return;
        }

        var previous = SelectedCampaign?.Id;

        Campaigns.Clear();

        foreach (var campaign in result.Value)
        {
            Campaigns.Add(campaign);
        }

        OnPropertyChanged(nameof(IsListEmpty));

        SelectedCampaign = previous is { } id
            ? Campaigns.FirstOrDefault(campaign => campaign.Id == id)
            : Campaigns.FirstOrDefault();
    }

    /// <summary>
    /// Создаёт кампанию и открывает её.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после создания.</returns>
    [RelayCommand]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        var draft = new CampaignDraft { Name = "Новая кампания" };
        var result = await _campaigns.SaveAsync(draft, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Новая кампания", result.Error!).ConfigureAwait(true);
            return;
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(true);

        SelectedCampaign = Campaigns.FirstOrDefault(campaign => campaign.Id == result.Value);
        _notifications.Show("Кампания создана", NotificationKind.Success);
    }

    /// <summary>
    /// Сохраняет сведения открытой кампании.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (SelectedCampaign is not { } campaign)
        {
            return;
        }

        var draft = new CampaignDraft
        {
            Id = campaign.Id,
            Name = Name,
            Description = Description,
            World = World,
            StartDate = StartDate,
            Notes = Notes,
            GameSystemId = SelectedGameSystem is { } system && system.Id != Guid.Empty ? system.Id : null,
            IsActive = IsActive,
        };

        var result = await _campaigns.SaveAsync(draft, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Сохранение кампании", result.Error!).ConfigureAwait(true);
            return;
        }

        _notifications.Show($"Кампания «{Name}» сохранена", NotificationKind.Success);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Удаляет открытую кампанию.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (SelectedCampaign is not { } campaign)
        {
            return;
        }

        var confirmed = await _dialogs
            .ShowConfirmationAsync(
                "Удаление кампании",
                $"Удалить кампанию «{campaign.Name}»? Её состав и хронология будут удалены. "
                + "Сами персонажи, монстры и локации останутся: кампания лишь ссылалась на них.")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var result = await _campaigns.DeleteAsync(campaign.Id, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Удаление кампании", result.Error!).ConfigureAwait(true);
            return;
        }

        SelectedCampaign = null;
        _notifications.Show($"Кампания «{campaign.Name}» удалена", NotificationKind.Success);

        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Перечитывает перечень объектов выбранного вида.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    [RelayCommand]
    private async Task SearchCandidatesAsync(CancellationToken cancellationToken)
    {
        Candidates.Clear();
        SelectedCandidate = null;

        if (SelectedKind is not { } kind)
        {
            return;
        }

        var found = await _catalog
            .SearchAsync(kind.Id, CandidateSearch, CandidateLimit, cancellationToken)
            .ConfigureAwait(true);

        foreach (var candidate in found)
        {
            Candidates.Add(candidate);
        }

        SelectedCandidate = Candidates.FirstOrDefault();
    }

    /// <summary>
    /// Добавляет выбранный объект в состав кампании.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после добавления.</returns>
    [RelayCommand]
    private async Task AddMemberAsync(CancellationToken cancellationToken)
    {
        if (SelectedCampaign is not { } campaign || SelectedKind is not { } kind)
        {
            return;
        }

        if (SelectedCandidate is not { } candidate)
        {
            await _dialogs
                .ShowInformationAsync("Состав кампании", $"Выберите, кого добавить: {kind.SingularName.ToLowerInvariant()}.")
                .ConfigureAwait(true);
            return;
        }

        var result = await _campaigns
            .AddMemberAsync(campaign.Id, kind.Id, candidate.Id, NewMemberRole, cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Состав кампании", result.Error!).ConfigureAwait(true);
            return;
        }

        NewMemberRole = string.Empty;
        _notifications.Show($"«{candidate.Name}» в составе кампании", NotificationKind.Success);

        await ReloadCardAsync(campaign.Id, cancellationToken).ConfigureAwait(true);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Сохраняет роль и заметки участника.
    /// </summary>
    /// <param name="row">Строка состава.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    [RelayCommand]
    private async Task SaveMemberAsync(CampaignMemberRowViewModel? row, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return;
        }

        var result = await _campaigns
            .UpdateMemberAsync(row.Id, row.Role, row.Notes, cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Состав кампании", result.Error!).ConfigureAwait(true);
            return;
        }

        row.MarkSaved();
        _notifications.Show($"«{row.Name}»: изменения сохранены", NotificationKind.Success);
    }

    /// <summary>
    /// Убирает участника из состава кампании.
    /// </summary>
    /// <param name="row">Строка состава.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task RemoveMemberAsync(CampaignMemberRowViewModel? row, CancellationToken cancellationToken)
    {
        if (row is null || SelectedCampaign is not { } campaign)
        {
            return;
        }

        var result = await _campaigns.RemoveMemberAsync(row.Id, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Состав кампании", result.Error!).ConfigureAwait(true);
            return;
        }

        await ReloadCardAsync(campaign.Id, cancellationToken).ConfigureAwait(true);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Добавляет событие в конец хронологии.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после добавления.</returns>
    [RelayCommand]
    private async Task AddEventAsync(CancellationToken cancellationToken)
    {
        if (SelectedCampaign is not { } campaign)
        {
            return;
        }

        var draft = new CampaignEventDraft
        {
            CampaignId = campaign.Id,
            Title = NewEventTitle,
            GameDate = NewEventDate,
        };

        var result = await _campaigns.SaveEventAsync(draft, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Хронология", result.Error!).ConfigureAwait(true);
            return;
        }

        NewEventTitle = string.Empty;
        NewEventDate = string.Empty;

        await ReloadCardAsync(campaign.Id, cancellationToken).ConfigureAwait(true);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Сохраняет изменённое событие хронологии.
    /// </summary>
    /// <param name="row">Строка хронологии.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    [RelayCommand]
    private async Task SaveEventAsync(CampaignEventRowViewModel? row, CancellationToken cancellationToken)
    {
        if (row is null || SelectedCampaign is not { } campaign)
        {
            return;
        }

        var draft = new CampaignEventDraft
        {
            Id = row.Id,
            CampaignId = campaign.Id,
            Title = row.EventTitle,
            GameDate = row.GameDate,
            Description = row.Description,
        };

        var result = await _campaigns.SaveEventAsync(draft, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Хронология", result.Error!).ConfigureAwait(true);
            return;
        }

        row.MarkSaved();
        _notifications.Show("Событие сохранено", NotificationKind.Success);
    }

    /// <summary>
    /// Перемещает событие на хронологии раньше.
    /// </summary>
    /// <param name="row">Строка хронологии.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после перемещения.</returns>
    [RelayCommand]
    private Task MoveEventUpAsync(CampaignEventRowViewModel? row, CancellationToken cancellationToken) =>
        MoveEventAsync(row, -1, cancellationToken);

    /// <summary>
    /// Перемещает событие на хронологии позже.
    /// </summary>
    /// <param name="row">Строка хронологии.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после перемещения.</returns>
    [RelayCommand]
    private Task MoveEventDownAsync(CampaignEventRowViewModel? row, CancellationToken cancellationToken) =>
        MoveEventAsync(row, 1, cancellationToken);

    /// <summary>
    /// Удаляет событие хронологии.
    /// </summary>
    /// <param name="row">Строка хронологии.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task DeleteEventAsync(CampaignEventRowViewModel? row, CancellationToken cancellationToken)
    {
        if (row is null || SelectedCampaign is not { } campaign)
        {
            return;
        }

        var confirmed = await _dialogs
            .ShowConfirmationAsync("Хронология", $"Удалить событие «{row.EventTitle}»?")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var result = await _campaigns.DeleteEventAsync(row.Id, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Хронология", result.Error!).ConfigureAwait(true);
            return;
        }

        await ReloadCardAsync(campaign.Id, cancellationToken).ConfigureAwait(true);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    partial void OnSelectedCampaignChanged(CampaignListItem? value)
    {
        OnPropertyChanged(nameof(IsCampaignOpen));

        if (value is null)
        {
            ClearCard();
            return;
        }

        // Обновление списка заменяет строку кампании новой — со свежими счётчиками,
        // но той же кампании. Перечитывать её карточку в этом случае незачем.
        if (value.Id == _openCampaignId)
        {
            return;
        }

        _ = ReloadCardAsync(value.Id, CancellationToken.None);
    }

    partial void OnSelectedKindChanged(CampaignKind? value) =>
        _ = SearchCandidatesAsync(CancellationToken.None);

    partial void OnCandidateSearchChanged(string value) =>
        _ = SearchCandidatesAsync(CancellationToken.None);

    private async Task MoveEventAsync(
        CampaignEventRowViewModel? row,
        int offset,
        CancellationToken cancellationToken)
    {
        if (row is null || SelectedCampaign is not { } campaign)
        {
            return;
        }

        var result = await _campaigns.MoveEventAsync(row.Id, offset, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Хронология", result.Error!).ConfigureAwait(true);
            return;
        }

        await ReloadCardAsync(campaign.Id, cancellationToken).ConfigureAwait(true);
    }

    private async Task ReloadCardAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        IsBusy = true;
        _openCampaignId = campaignId;

        try
        {
            var result = await _campaigns.GetAsync(campaignId, cancellationToken).ConfigureAwait(true);

            if (result.IsFailure)
            {
                ClearCard();
                await _dialogs.ShowErrorAsync("Кампания", result.Error!).ConfigureAwait(true);
                return;
            }

            var card = result.Value;

            Name = card.Draft.Name;
            Description = card.Draft.Description ?? string.Empty;
            World = card.Draft.World ?? string.Empty;
            StartDate = card.Draft.StartDate ?? string.Empty;
            Notes = card.Draft.Notes ?? string.Empty;
            IsActive = card.Draft.IsActive;

            SelectedGameSystem = card.Draft.GameSystemId is { } systemId
                ? GameSystems.FirstOrDefault(system => system.Id == systemId)
                  ?? ContentFieldViewModel.EmptyReference
                : ContentFieldViewModel.EmptyReference;

            Groups.Clear();

            foreach (var group in card.Groups)
            {
                Groups.Add(new CampaignGroupViewModel(group));
            }

            Events.Clear();

            foreach (var entry in card.Events)
            {
                Events.Add(new CampaignEventRowViewModel(entry));
            }

            Summary = string.Create(
                CultureInfo.CurrentCulture,
                $"Участников: {card.MemberCount} · событий: {card.Events.Count}");

            OnPropertyChanged(nameof(IsRosterEmpty));
            OnPropertyChanged(nameof(IsTimelineEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadGameSystemsAsync(CancellationToken cancellationToken)
    {
        var systems = await _content
            .GetReferencesAsync(ContentTypeIds.GameSystems, cancellationToken)
            .ConfigureAwait(true);

        GameSystems.Clear();
        GameSystems.Add(ContentFieldViewModel.EmptyReference);

        foreach (var system in systems)
        {
            GameSystems.Add(system);
        }
    }

    private void ClearCard()
    {
        _openCampaignId = null;

        Name = string.Empty;
        Description = string.Empty;
        World = string.Empty;
        StartDate = string.Empty;
        Notes = string.Empty;
        IsActive = true;
        SelectedGameSystem = ContentFieldViewModel.EmptyReference;
        Summary = string.Empty;

        Groups.Clear();
        Events.Clear();

        OnPropertyChanged(nameof(IsRosterEmpty));
        OnPropertyChanged(nameof(IsTimelineEmpty));
    }
}
