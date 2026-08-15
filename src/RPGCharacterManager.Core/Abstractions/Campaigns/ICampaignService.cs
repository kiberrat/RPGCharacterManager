using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Campaigns;

/// <summary>
/// Строка списка кампаний.
/// </summary>
/// <param name="Id">Идентификатор кампании.</param>
/// <param name="Name">Название кампании.</param>
/// <param name="World">Мир или сеттинг.</param>
/// <param name="IsActive">Кампания активна.</param>
/// <param name="MemberCount">Количество участников состава.</param>
/// <param name="EventCount">Количество событий хронологии.</param>
public sealed record CampaignListItem(
    Guid Id,
    string Name,
    string? World,
    bool IsActive,
    int MemberCount,
    int EventCount);

/// <summary>
/// Участник кампании.
/// </summary>
/// <param name="Id">Идентификатор записи состава.</param>
/// <param name="KindId">Идентификатор вида объекта.</param>
/// <param name="ObjectId">Идентификатор самого объекта.</param>
/// <param name="ObjectName">Название объекта.</param>
/// <param name="Role">Роль в кампании.</param>
/// <param name="Notes">Заметки мастера.</param>
/// <param name="IsMissing">Объект удалён из базы, осталась только ссылка.</param>
public sealed record CampaignMemberInfo(
    Guid Id,
    string KindId,
    Guid ObjectId,
    string ObjectName,
    string? Role,
    string? Notes,
    bool IsMissing);

/// <summary>
/// Группа состава кампании: участники одного вида.
/// </summary>
/// <param name="Kind">Вид объектов группы.</param>
/// <param name="Members">Участники в порядке отображения.</param>
public sealed record CampaignGroup(CampaignKind Kind, IReadOnlyList<CampaignMemberInfo> Members);

/// <summary>
/// Событие хронологии кампании.
/// </summary>
/// <param name="Id">Идентификатор события.</param>
/// <param name="Title">Название события.</param>
/// <param name="Description">Описание события.</param>
/// <param name="GameDate">Игровая дата.</param>
/// <param name="SortOrder">Место на хронологии.</param>
public sealed record CampaignEventInfo(
    Guid Id,
    string Title,
    string? Description,
    string? GameDate,
    int SortOrder);

/// <summary>
/// Карточка кампании: её сведения, состав и хронология.
/// </summary>
/// <param name="Id">Идентификатор кампании.</param>
/// <param name="Draft">Изменяемые сведения кампании.</param>
/// <param name="Groups">Состав, разложенный по видам объектов.</param>
/// <param name="Events">События хронологии в заданном порядке.</param>
public sealed record CampaignCard(
    Guid Id,
    CampaignDraft Draft,
    IReadOnlyList<CampaignGroup> Groups,
    IReadOnlyList<CampaignEventInfo> Events)
{
    /// <summary>Общее количество участников состава.</summary>
    public int MemberCount => Groups.Sum(group => group.Members.Count);
}

/// <summary>
/// Изменяемые сведения кампании.
/// </summary>
public sealed record CampaignDraft
{
    /// <summary>Идентификатор кампании; <see langword="null"/> — создаётся новая.</summary>
    public Guid? Id { get; init; }

    /// <summary>Название кампании.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Описание кампании.</summary>
    public string? Description { get; init; }

    /// <summary>Мир или сеттинг кампании.</summary>
    public string? World { get; init; }

    /// <summary>Игровая дата начала кампании.</summary>
    public string? StartDate { get; init; }

    /// <summary>Заметки мастера.</summary>
    public string? Notes { get; init; }

    /// <summary>Идентификатор игровой системы кампании.</summary>
    public Guid? GameSystemId { get; init; }

    /// <summary>Кампания активна.</summary>
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// Изменяемые сведения события кампании.
/// </summary>
public sealed record CampaignEventDraft
{
    /// <summary>Идентификатор события; <see langword="null"/> — создаётся новое.</summary>
    public Guid? Id { get; init; }

    /// <summary>Идентификатор кампании.</summary>
    public Guid CampaignId { get; init; }

    /// <summary>Название события.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Описание события.</summary>
    public string? Description { get; init; }

    /// <summary>Игровая дата события.</summary>
    public string? GameDate { get; init; }
}

/// <summary>
/// Менеджер кампаний: игры, их состав и хронология.
///
/// Кампания не содержит игровых объектов, а ссылается на них: персонаж, монстр
/// или локация остаются единственной записью в базе и участвуют в любом числе игр.
/// </summary>
public interface ICampaignService
{
    /// <summary>
    /// Возвращает все кампании в порядке названий.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список кампаний.</returns>
    Task<Result<IReadOnlyList<CampaignListItem>>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает карточку кампании: сведения, состав и хронологию.
    /// </summary>
    /// <param name="campaignId">Идентификатор кампании.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Карточка кампании.</returns>
    Task<Result<CampaignCard>> GetAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт кампанию либо сохраняет изменения существующей.
    /// </summary>
    /// <param name="draft">Сведения кампании.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор сохранённой кампании.</returns>
    Task<Result<Guid>> SaveAsync(CampaignDraft draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет кампанию вместе с её составом и хронологией.
    ///
    /// Сами игровые объекты не удаляются: кампания лишь ссылалась на них.
    /// </summary>
    /// <param name="campaignId">Идентификатор кампании.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат удаления.</returns>
    Task<Result> DeleteAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет объект в состав кампании.
    /// </summary>
    /// <param name="campaignId">Идентификатор кампании.</param>
    /// <param name="kindId">Идентификатор вида объекта.</param>
    /// <param name="objectId">Идентификатор объекта.</param>
    /// <param name="role">Роль в кампании.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор созданной записи состава.</returns>
    Task<Result<Guid>> AddMemberAsync(
        Guid campaignId,
        string kindId,
        Guid objectId,
        string? role = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Изменяет роль и заметки участника.
    /// </summary>
    /// <param name="memberId">Идентификатор записи состава.</param>
    /// <param name="role">Роль в кампании.</param>
    /// <param name="notes">Заметки мастера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат сохранения.</returns>
    Task<Result> UpdateMemberAsync(
        Guid memberId,
        string? role,
        string? notes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Убирает участника из состава кампании.
    /// </summary>
    /// <param name="memberId">Идентификатор записи состава.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат удаления.</returns>
    Task<Result> RemoveMemberAsync(Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт событие хронологии либо сохраняет изменения существующего.
    /// Новое событие становится последним на хронологии.
    /// </summary>
    /// <param name="draft">Сведения события.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор сохранённого события.</returns>
    Task<Result<Guid>> SaveEventAsync(CampaignEventDraft draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Перемещает событие по хронологии, меняя его местами с соседним.
    /// </summary>
    /// <param name="eventId">Идентификатор события.</param>
    /// <param name="offset">Смещение: −1 — раньше, +1 — позже.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат перемещения.</returns>
    Task<Result> MoveEventAsync(Guid eventId, int offset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет событие хронологии.
    /// </summary>
    /// <param name="eventId">Идентификатор события.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат удаления.</returns>
    Task<Result> DeleteEventAsync(Guid eventId, CancellationToken cancellationToken = default);
}
