using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Campaigns;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Campaigns;

/// <summary>
/// Менеджер кампаний: игры, их состав и хронология.
///
/// Состав хранит вид объекта и его идентификатор, а не внешний ключ на каждую
/// таблицу: видов объектов столько, сколько зарегистрировано видов контента,
/// и таблица состава не должна расти вместе с ними (решение Р-89). Плата за это —
/// удаление объекта не убирает запись состава, поэтому служба проверяет каждую
/// ссылку при чтении и показывает потерянную явно.
/// </summary>
public sealed class CampaignService : ICampaignService
{
    private const string MissingObjectName = "Объект удалён";

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly ICampaignCatalog _catalog;
    private readonly ILogger<CampaignService> _logger;

    /// <summary>
    /// Создаёт менеджер кампаний.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="catalog">Каталог объектов, доступных кампании.</param>
    /// <param name="logger">Журналировщик.</param>
    public CampaignService(
        IDbContextFactory<RpgDbContext> contextFactory,
        ICampaignCatalog catalog,
        ILogger<CampaignService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _catalog = Guard.NotNull(catalog);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CampaignListItem>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var items = await context.Campaigns
                .AsNoTracking()
                .OrderByDescending(campaign => campaign.IsActive)
                .ThenBy(campaign => campaign.Name)
                .Select(campaign => new CampaignListItem(
                    campaign.Id,
                    campaign.Name,
                    campaign.World,
                    campaign.IsActive,
                    campaign.Members.Count,
                    campaign.Events.Count))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return Result.Success<IReadOnlyList<CampaignListItem>>(items);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CampaignLog.CampaignsReadFailed(_logger, exception);
            return Result.Failure<IReadOnlyList<CampaignListItem>>("Не удалось прочитать список кампаний.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<CampaignCard>> GetAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            // Состав и хронология читаются отдельными запросами: в одном они дали бы
            // произведение строк — каждый участник повторился бы столько раз, сколько
            // у кампании событий.
            var campaign = await context.Campaigns
                .AsNoTracking()
                .Include(item => item.Members)
                .Include(item => item.Events)
                .FirstOrDefaultAsync(item => item.Id == campaignId, cancellationToken)
                .ConfigureAwait(false);

            if (campaign is null)
            {
                return Result.Failure<CampaignCard>("Кампания не найдена: возможно, она уже удалена.");
            }

            var groups = await BuildGroupsAsync(campaign.Members, cancellationToken).ConfigureAwait(false);

            var events = campaign.Events
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.CreatedAt)
                .Select(item => new CampaignEventInfo(
                    item.Id,
                    item.Title,
                    item.Description,
                    item.GameDate,
                    item.SortOrder))
                .ToList();

            return Result.Success(new CampaignCard(campaign.Id, ToDraft(campaign), groups, events));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CampaignLog.CampaignReadFailed(_logger, exception, campaignId);
            return Result.Failure<CampaignCard>("Не удалось прочитать кампанию.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveAsync(
        CampaignDraft draft,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(draft);

        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            return Result.Failure<Guid>("Не задано название кампании.");
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            Campaign campaign;

            if (draft.Id is { } id)
            {
                var existing = await context.Campaigns
                    .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    return Result.Failure<Guid>("Кампания не найдена: возможно, она уже удалена.");
                }

                campaign = existing;
            }
            else
            {
                campaign = new Campaign();
                context.Campaigns.Add(campaign);
            }

            Apply(draft, campaign);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            CampaignLog.CampaignSaved(_logger, campaign.Name, campaign.Id);

            return Result.Success(campaign.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CampaignLog.CampaignSaveFailed(_logger, exception, draft.Name);
            return Result.Failure<Guid>("Не удалось сохранить кампанию.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var campaign = await context.Campaigns
                .FirstOrDefaultAsync(item => item.Id == campaignId, cancellationToken)
                .ConfigureAwait(false);

            if (campaign is null)
            {
                return Result.Failure("Кампания не найдена: возможно, она уже удалена.");
            }

            context.Campaigns.Remove(campaign);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            CampaignLog.CampaignDeleted(_logger, campaign.Name, campaign.Id);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CampaignLog.CampaignDeleteFailed(_logger, exception, campaignId);
            return Result.Failure("Не удалось удалить кампанию.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> AddMemberAsync(
        Guid campaignId,
        string kindId,
        Guid objectId,
        string? role = null,
        CancellationToken cancellationToken = default)
    {
        var kind = _catalog.FindKind(kindId);

        if (kind is null)
        {
            return Result.Failure<Guid>($"Неизвестный вид объектов «{kindId}».");
        }

        var names = await _catalog.GetNamesAsync(kindId, [objectId], cancellationToken).ConfigureAwait(false);

        if (!names.TryGetValue(objectId, out var objectName))
        {
            return Result.Failure<Guid>($"{kind.SingularName} не найден: возможно, объект уже удалён.");
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var campaign = await context.Campaigns
                .Include(item => item.Members)
                .FirstOrDefaultAsync(item => item.Id == campaignId, cancellationToken)
                .ConfigureAwait(false);

            if (campaign is null)
            {
                return Result.Failure<Guid>("Кампания не найдена: возможно, она уже удалена.");
            }

            if (campaign.Members.Any(member =>
                    string.Equals(member.ObjectKind, kindId, StringComparison.Ordinal)
                    && member.ObjectId == objectId))
            {
                return Result.Failure<Guid>($"«{objectName}» уже входит в состав этой кампании.");
            }

            var order = campaign.Members
                .Where(member => string.Equals(member.ObjectKind, kindId, StringComparison.Ordinal))
                .Select(member => member.SortOrder)
                .DefaultIfEmpty(-1)
                .Max() + 1;

            var added = new CampaignMember
            {
                CampaignId = campaign.Id,
                ObjectKind = kindId,
                ObjectId = objectId,
                Role = Trim(role),
                SortOrder = order,
            };

            // Ключ записи задан конструктором, поэтому состояние указывается явно:
            // иначе EF Core принял бы её за изменение существующей строки (решение Р-28).
            context.Add(added);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            CampaignLog.MemberAdded(_logger, objectName, kind.Title, campaign.Name);

            return Result.Success(added.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CampaignLog.MemberAddFailed(_logger, exception, objectName, campaignId);
            return Result.Failure<Guid>("Не удалось добавить участника кампании.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> UpdateMemberAsync(
        Guid memberId,
        string? role,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var member = await context.CampaignMembers
                .FirstOrDefaultAsync(item => item.Id == memberId, cancellationToken)
                .ConfigureAwait(false);

            if (member is null)
            {
                return Result.Failure("Участник не найден: возможно, он уже убран из состава.");
            }

            member.Role = Trim(role);
            member.Notes = Trim(notes);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CampaignLog.MemberSaveFailed(_logger, exception, memberId);
            return Result.Failure("Не удалось сохранить участника кампании.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> RemoveMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var member = await context.CampaignMembers
                .FirstOrDefaultAsync(item => item.Id == memberId, cancellationToken)
                .ConfigureAwait(false);

            if (member is null)
            {
                return Result.Failure("Участник не найден: возможно, он уже убран из состава.");
            }

            context.CampaignMembers.Remove(member);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            CampaignLog.MemberRemoved(_logger, member.ObjectKind, member.ObjectId);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CampaignLog.MemberRemoveFailed(_logger, exception, memberId);
            return Result.Failure("Не удалось убрать участника из состава.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveEventAsync(
        CampaignEventDraft draft,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(draft);

        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            return Result.Failure<Guid>("Не задано название события.");
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            CampaignEvent item;

            if (draft.Id is { } id)
            {
                var existing = await context.CampaignEvents
                    .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    return Result.Failure<Guid>("Событие не найдено: возможно, оно уже удалено.");
                }

                item = existing;
            }
            else
            {
                var campaignExists = await context.Campaigns
                    .AnyAsync(campaign => campaign.Id == draft.CampaignId, cancellationToken)
                    .ConfigureAwait(false);

                if (!campaignExists)
                {
                    return Result.Failure<Guid>("Кампания не найдена: возможно, она уже удалена.");
                }

                var order = await context.CampaignEvents
                    .Where(entry => entry.CampaignId == draft.CampaignId)
                    .Select(entry => (int?)entry.SortOrder)
                    .MaxAsync(cancellationToken)
                    .ConfigureAwait(false);

                item = new CampaignEvent
                {
                    CampaignId = draft.CampaignId,
                    SortOrder = (order ?? -1) + 1,
                };

                context.CampaignEvents.Add(item);
            }

            item.Title = draft.Title.Trim();
            item.Description = Trim(draft.Description);
            item.GameDate = Trim(draft.GameDate);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            CampaignLog.EventSaved(_logger, item.Title, item.CampaignId);

            return Result.Success(item.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CampaignLog.EventSaveFailed(_logger, exception, draft.Title);
            return Result.Failure<Guid>("Не удалось сохранить событие кампании.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> MoveEventAsync(
        Guid eventId,
        int offset,
        CancellationToken cancellationToken = default)
    {
        if (offset == 0)
        {
            return Result.Success();
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var item = await context.CampaignEvents
                .FirstOrDefaultAsync(entry => entry.Id == eventId, cancellationToken)
                .ConfigureAwait(false);

            if (item is null)
            {
                return Result.Failure("Событие не найдено: возможно, оно уже удалено.");
            }

            var ordered = await context.CampaignEvents
                .Where(entry => entry.CampaignId == item.CampaignId)
                .OrderBy(entry => entry.SortOrder)
                .ThenBy(entry => entry.CreatedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var index = ordered.FindIndex(entry => entry.Id == eventId);
            var target = index + Math.Sign(offset);

            if (target < 0 || target >= ordered.Count)
            {
                return Result.Success();
            }

            // Порядок пересчитывается целиком: значения могли совпасть, если события
            // добавлялись до появления хронологии либо переносились между кампаниями.
            (ordered[index], ordered[target]) = (ordered[target], ordered[index]);

            for (var position = 0; position < ordered.Count; position++)
            {
                ordered[position].SortOrder = position;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CampaignLog.EventMoveFailed(_logger, exception, eventId);
            return Result.Failure("Не удалось переместить событие.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var item = await context.CampaignEvents
                .FirstOrDefaultAsync(entry => entry.Id == eventId, cancellationToken)
                .ConfigureAwait(false);

            if (item is null)
            {
                return Result.Failure("Событие не найдено: возможно, оно уже удалено.");
            }

            context.CampaignEvents.Remove(item);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            CampaignLog.EventDeleted(_logger, item.Title, item.CampaignId);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CampaignLog.EventDeleteFailed(_logger, exception, eventId);
            return Result.Failure("Не удалось удалить событие.");
        }
    }

    /// <summary>
    /// Раскладывает состав кампании по видам объектов и подставляет их названия.
    /// </summary>
    /// <param name="members">Записи состава.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Группы состава в порядке видов каталога.</returns>
    private async Task<IReadOnlyList<CampaignGroup>> BuildGroupsAsync(
        IEnumerable<CampaignMember> members,
        CancellationToken cancellationToken)
    {
        var groups = new List<CampaignGroup>();

        var byKind = members
            .GroupBy(member => member.ObjectKind, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        foreach (var kind in _catalog.Kinds)
        {
            if (!byKind.TryGetValue(kind.Id, out var kindMembers))
            {
                continue;
            }

            // Названия объектов вида читаются одним запросом, а не по одному
            // на участника: состав кампании может насчитывать сотни записей.
            var names = await _catalog
                .GetNamesAsync(kind.Id, kindMembers.Select(member => member.ObjectId).ToList(), cancellationToken)
                .ConfigureAwait(false);

            var infos = kindMembers
                .Select(member => ToInfo(member, names))
                .OrderBy(info => info.SortKey, StringComparer.CurrentCulture)
                .Select(info => info.Member)
                .ToList();

            groups.Add(new CampaignGroup(kind, infos));
        }

        // Вид объекта мог исчезнуть вместе с подсистемой, которая его давала:
        // такие записи всё равно показываются, иначе они пропали бы молча.
        foreach (var orphan in byKind.Where(pair => _catalog.FindKind(pair.Key) is null))
        {
            var kind = new CampaignKind(orphan.Key, orphan.Key, orphan.Key, "Роль", int.MaxValue);

            groups.Add(new CampaignGroup(
                kind,
                orphan.Value.Select(member => ToInfo(member, new Dictionary<Guid, string>()).Member).ToList()));
        }

        return groups;
    }

    private static (CampaignMemberInfo Member, string SortKey) ToInfo(
        CampaignMember member,
        IReadOnlyDictionary<Guid, string> names)
    {
        var found = names.TryGetValue(member.ObjectId, out var name);

        return (
            new CampaignMemberInfo(
                member.Id,
                member.ObjectKind,
                member.ObjectId,
                found ? name! : MissingObjectName,
                member.Role,
                member.Notes,
                !found),
            found ? name! : MissingObjectName);
    }

    private static CampaignDraft ToDraft(Campaign campaign) => new()
    {
        Id = campaign.Id,
        Name = campaign.Name,
        Description = campaign.Description,
        World = campaign.World,
        StartDate = campaign.StartDate,
        Notes = campaign.Notes,
        GameSystemId = campaign.GameSystemId,
        IsActive = campaign.IsActive,
    };

    private static void Apply(CampaignDraft draft, Campaign campaign)
    {
        campaign.Name = draft.Name.Trim();
        campaign.Description = Trim(draft.Description);
        campaign.World = Trim(draft.World);
        campaign.StartDate = Trim(draft.StartDate);
        campaign.Notes = Trim(draft.Notes);
        campaign.GameSystemId = draft.GameSystemId;
        campaign.IsActive = draft.IsActive;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
