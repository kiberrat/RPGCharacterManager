using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using RPGCharacterManager.Core.Abstractions.Campaigns;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Участник кампании в списке состава.
///
/// Роль и заметки правятся прямо в строке. Кнопка сохранения появляется только
/// после изменения: молчаливое сохранение при переходе к другой строке скрыло бы
/// от мастера отказ базы данных.
/// </summary>
public sealed partial class CampaignMemberRowViewModel : ViewModelBase
{
    private string _storedRole;
    private string _storedNotes;

    [ObservableProperty]
    private string _role;

    [ObservableProperty]
    private string _notes;

    /// <summary>
    /// Создаёт строку состава кампании.
    /// </summary>
    /// <param name="member">Участник кампании.</param>
    /// <param name="roleTitle">Название поля роли: «Игрок» или «Роль».</param>
    public CampaignMemberRowViewModel(CampaignMemberInfo member, string roleTitle)
    {
        Member = Guard.NotNull(member);
        RoleTitle = Guard.NotNullOrWhiteSpace(roleTitle);

        _role = member.Role ?? string.Empty;
        _notes = member.Notes ?? string.Empty;
        _storedRole = _role;
        _storedNotes = _notes;
    }

    /// <summary>Сведения об участнике.</summary>
    public CampaignMemberInfo Member { get; }

    /// <summary>Идентификатор записи состава.</summary>
    public Guid Id => Member.Id;

    /// <summary>Название объекта.</summary>
    public string Name => Member.ObjectName;

    /// <summary>Название поля роли.</summary>
    public string RoleTitle { get; }

    /// <summary>Объект удалён, осталась только ссылка на него.</summary>
    public bool IsMissing => Member.IsMissing;

    /// <summary>Роль или заметки изменены и не сохранены.</summary>
    public bool IsChanged =>
        !string.Equals(Role, _storedRole, StringComparison.Ordinal)
        || !string.Equals(Notes, _storedNotes, StringComparison.Ordinal);

    /// <summary>
    /// Отмечает изменения строки как сохранённые.
    /// </summary>
    public void MarkSaved()
    {
        _storedRole = Role;
        _storedNotes = Notes;

        OnPropertyChanged(nameof(IsChanged));
    }

    partial void OnRoleChanged(string value) => OnPropertyChanged(nameof(IsChanged));

    partial void OnNotesChanged(string value) => OnPropertyChanged(nameof(IsChanged));
}

/// <summary>
/// Группа состава кампании: участники одного вида объектов.
/// </summary>
public sealed class CampaignGroupViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт группу состава.
    /// </summary>
    /// <param name="group">Группа состава кампании.</param>
    public CampaignGroupViewModel(CampaignGroup group)
    {
        Guard.NotNull(group);

        Title = group.Kind.Title;
        RoleTitle = group.Kind.RoleTitle;
        Members = [.. group.Members.Select(member => new CampaignMemberRowViewModel(member, group.Kind.RoleTitle))];
        Counter = string.Create(CultureInfo.CurrentCulture, $"{Members.Count}");
    }

    /// <summary>Название вида объектов.</summary>
    public string Title { get; }

    /// <summary>Название поля роли.</summary>
    public string RoleTitle { get; }

    /// <summary>Количество участников группы.</summary>
    public string Counter { get; }

    /// <summary>Участники группы.</summary>
    public ObservableCollection<CampaignMemberRowViewModel> Members { get; }
}

/// <summary>
/// Событие хронологии кампании.
/// </summary>
public sealed partial class CampaignEventRowViewModel : ViewModelBase
{
    private string _storedTitle;
    private string _storedGameDate;
    private string _storedDescription;

    [ObservableProperty]
    private string _eventTitle;

    [ObservableProperty]
    private string _gameDate;

    [ObservableProperty]
    private string _description;

    /// <summary>
    /// Создаёт строку хронологии.
    /// </summary>
    /// <param name="entry">Событие кампании.</param>
    public CampaignEventRowViewModel(CampaignEventInfo entry)
    {
        Entry = Guard.NotNull(entry);

        _eventTitle = entry.Title;
        _gameDate = entry.GameDate ?? string.Empty;
        _description = entry.Description ?? string.Empty;
        _storedTitle = _eventTitle;
        _storedGameDate = _gameDate;
        _storedDescription = _description;
    }

    /// <summary>Сведения о событии.</summary>
    public CampaignEventInfo Entry { get; }

    /// <summary>Идентификатор события.</summary>
    public Guid Id => Entry.Id;

    /// <summary>Событие изменено и не сохранено.</summary>
    public bool IsChanged =>
        !string.Equals(EventTitle, _storedTitle, StringComparison.Ordinal)
        || !string.Equals(GameDate, _storedGameDate, StringComparison.Ordinal)
        || !string.Equals(Description, _storedDescription, StringComparison.Ordinal);

    /// <summary>
    /// Отмечает изменения события как сохранённые.
    /// </summary>
    public void MarkSaved()
    {
        _storedTitle = EventTitle;
        _storedGameDate = GameDate;
        _storedDescription = Description;

        OnPropertyChanged(nameof(IsChanged));
    }

    partial void OnEventTitleChanged(string value) => OnPropertyChanged(nameof(IsChanged));

    partial void OnGameDateChanged(string value) => OnPropertyChanged(nameof(IsChanged));

    partial void OnDescriptionChanged(string value) => OnPropertyChanged(nameof(IsChanged));
}
