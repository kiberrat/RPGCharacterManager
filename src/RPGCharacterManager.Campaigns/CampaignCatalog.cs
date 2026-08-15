using Microsoft.EntityFrameworkCore;
using RPGCharacterManager.Core.Abstractions.Campaigns;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Campaigns;

/// <summary>
/// Перечень объектов, доступных кампании.
///
/// Каталог не содержит собственного списка видов: их дают персонажи и описания
/// видов контента. Поэтому вид объектов, добавленный на будущих этапах, войдёт
/// в кампанию сам, без изменения этого класса.
/// </summary>
public sealed class CampaignCatalog : ICampaignCatalog
{
    /// <summary>
    /// Идентификатор вида «персонажи игроков».
    ///
    /// Объявлен в контрактах: на него ссылается и состав кампании, и режим мастера.
    /// </summary>
    public const string CharacterKindId = CampaignObjectKinds.Characters;

    /// <summary>
    /// Виды, с которых начинается перечень: из них состоит кампания в первую очередь.
    ///
    /// Порядок влияет только на отображение. Вид, которого здесь нет, остаётся
    /// доступным кампании и показывается следом за перечисленными.
    /// </summary>
    private static readonly string[] PreferredKinds =
    [
        CharacterKindId,
        ContentTypeIds.Npcs,
        ContentTypeIds.Monsters,
        ContentTypeIds.Quests,
        ContentTypeIds.Locations,
    ];

    private readonly IContentService _content;
    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly Dictionary<string, CampaignKind> _kinds;

    /// <summary>
    /// Создаёт каталог объектов кампании.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    public CampaignCatalog(IContentService content, IDbContextFactory<RpgDbContext> contextFactory)
    {
        _content = Guard.NotNull(content);
        _contextFactory = Guard.NotNull(contextFactory);

        var kinds = new List<CampaignKind>
        {
            // Роль персонажа в кампании — это игрок, который им играет.
            new(CharacterKindId, "Игроки", "Персонаж", "Игрок", 0),
        };

        kinds.AddRange(_content.Types.Select(type => new CampaignKind(
            type.Id,
            type.DisplayName,
            type.SingularName,
            "Роль",
            type.Order + PreferredKinds.Length + 1)));

        Kinds = kinds
            .OrderBy(GetDisplayOrder)
            .ThenBy(kind => kind.Title, StringComparer.CurrentCulture)
            .ToList();

        _kinds = Kinds.ToDictionary(kind => kind.Id, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public IReadOnlyList<CampaignKind> Kinds { get; }

    /// <inheritdoc />
    public CampaignKind? FindKind(string kindId) =>
        !string.IsNullOrWhiteSpace(kindId) && _kinds.TryGetValue(kindId, out var kind) ? kind : null;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CampaignObject>> SearchAsync(
        string kindId,
        string? search,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0 || FindKind(kindId) is null)
        {
            return [];
        }

        if (string.Equals(kindId, CharacterKindId, StringComparison.Ordinal))
        {
            return await SearchCharactersAsync(search, limit, cancellationToken).ConfigureAwait(false);
        }

        var page = await _content
            .SearchAsync(kindId, search, 0, limit, cancellationToken)
            .ConfigureAwait(false);

        return page.Items.Select(item => new CampaignObject(item.Id, item.Name)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        string kindId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(ids);

        if (ids.Count == 0 || FindKind(kindId) is null)
        {
            return new Dictionary<Guid, string>();
        }

        if (string.Equals(kindId, CharacterKindId, StringComparison.Ordinal))
        {
            return await GetCharacterNamesAsync(ids, cancellationToken).ConfigureAwait(false);
        }

        var items = await _content.GetItemsAsync(kindId, ids, cancellationToken).ConfigureAwait(false);

        return items.ToDictionary(item => item.Id, item => item.Name);
    }

    /// <summary>
    /// Возвращает место вида в перечне: сначала виды, из которых состоит кампания.
    /// </summary>
    /// <param name="kind">Вид объектов.</param>
    /// <returns>Значение для сортировки.</returns>
    private static int GetDisplayOrder(CampaignKind kind)
    {
        var preferred = Array.IndexOf(PreferredKinds, kind.Id);

        return preferred >= 0 ? preferred : kind.Order;
    }

    private async Task<IReadOnlyList<CampaignObject>> SearchCharactersAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = context.Characters.AsNoTracking().Where(character => !character.IsTemplate);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";

            query = query.Where(character => EF.Functions.Like(character.Name, pattern));
        }

        return await query
            .OrderBy(character => character.Name)
            .Take(limit)
            .Select(character => new CampaignObject(character.Id, character.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetCharacterNamesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var keys = ids.ToList();

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var found = await context.Characters
            .AsNoTracking()
            .Where(character => keys.Contains(character.Id))
            .Select(character => new { character.Id, character.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return found.ToDictionary(character => character.Id, character => character.Name);
    }
}
