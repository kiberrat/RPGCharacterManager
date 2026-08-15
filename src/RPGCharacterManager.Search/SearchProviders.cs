using System.Globalization;
using RPGCharacterManager.Core.Abstractions.Campaigns;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Abstractions.History;
using RPGCharacterManager.Core.Abstractions.Search;
using RPGCharacterManager.Core.Models.Shell;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Search;

/// <summary>
/// Находки среди персонажей.
/// </summary>
public sealed class CharacterSearchProvider : ISearchProvider
{
    private readonly ICharacterService _characters;

    /// <summary>
    /// Создаёт поставщика находок среди персонажей.
    /// </summary>
    /// <param name="characters">Служба персонажей.</param>
    public CharacterSearchProvider(ICharacterService characters) =>
        _characters = Guard.NotNull(characters);

    /// <inheritdoc />
    public int Order => 10;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchGroup>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var page = await _characters
            .SearchAsync(query, 0, limit, cancellationToken).ConfigureAwait(false);

        var hits = page.Items
            .Select(character => new SearchHit(
                character.Name,
                Describe(character),
                DocumentIds.CharacterSheet,
                character.Id))
            .ToList();

        return [new SearchGroup("Персонажи", Order, hits, page.TotalCount)];
    }

    /// <summary>
    /// Описывает персонажа строкой пояснения.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <returns>Раса, класс и уровень через точку.</returns>
    private static string Describe(CharacterListItem character) => string.Join(
        " · ",
        new[]
        {
            character.RaceName,
            character.ClassName,
            $"уровень {character.Level.ToString(CultureInfo.CurrentCulture)}",
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
}

/// <summary>
/// Находки среди игрового контента всех видов.
///
/// Поставщик не перечисляет виды: он спрашивает у службы контента все
/// зарегистрированные. Поэтому предметы, заклинания, черты и монстры,
/// названные в ROADMAP, ищутся вместе с остальными видами, а вид, добавленный
/// на будущем этапе, попадает в поиск сам.
/// </summary>
public sealed class ContentSearchProvider : ISearchProvider
{
    private readonly IContentService _content;

    /// <summary>
    /// Создаёт поставщика находок среди контента.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    public ContentSearchProvider(IContentService content) => _content = Guard.NotNull(content);

    /// <inheritdoc />
    public int Order => 20;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchGroup>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var groups = new List<SearchGroup>();

        foreach (var type in _content.Types)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = await _content
                .SearchAsync(type.Id, query, 0, limit, cancellationToken).ConfigureAwait(false);

            if (page.Items.Count == 0)
            {
                continue;
            }

            var hits = page.Items
                .Select(item => new SearchHit(
                    item.Name,
                    type.SingularName,
                    DocumentIds.Content,
                    type.Id))
                .ToList();

            groups.Add(new SearchGroup(type.DisplayName, Order + type.Order, hits, page.TotalCount));
        }

        return groups;
    }
}

/// <summary>
/// Находки среди кампаний.
/// </summary>
public sealed class CampaignSearchProvider : ISearchProvider
{
    private readonly ICampaignService _campaigns;

    /// <summary>
    /// Создаёт поставщика находок среди кампаний.
    /// </summary>
    /// <param name="campaigns">Менеджер кампаний.</param>
    public CampaignSearchProvider(ICampaignService campaigns) => _campaigns = Guard.NotNull(campaigns);

    /// <inheritdoc />
    public int Order => 5;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchGroup>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var result = await _campaigns.GetAllAsync(cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return [];
        }

        // Кампаний немного — десятки за всю историю игр, — поэтому отбор идёт
        // в памяти: отдельный запрос к базе ради этого не нужен.
        var found = result.Value
            .Where(campaign => Contains(campaign.Name, query) || Contains(campaign.World, query))
            .ToList();

        var hits = found
            .Take(limit)
            .Select(campaign => new SearchHit(
                campaign.Name,
                campaign.World,
                DocumentIds.Campaigns,
                campaign.Id))
            .ToList();

        return [new SearchGroup("Кампании", Order, hits, found.Count)];
    }

    /// <summary>
    /// Проверяет вхождение запроса без различения регистра.
    /// </summary>
    /// <param name="value">Проверяемое значение.</param>
    /// <param name="query">Запрос.</param>
    /// <returns><see langword="true"/>, если значение содержит запрос.</returns>
    private static bool Contains(string? value, string query) =>
        value is not null && value.Contains(query, StringComparison.CurrentCultureIgnoreCase);
}

/// <summary>
/// Находки среди записей журнала.
/// </summary>
public sealed class HistorySearchProvider : ISearchProvider
{
    private readonly IHistoryService _history;

    /// <summary>
    /// Создаёт поставщика находок среди записей журнала.
    /// </summary>
    /// <param name="history">Журнал событий.</param>
    public HistorySearchProvider(IHistoryService history) => _history = Guard.NotNull(history);

    /// <inheritdoc />
    public int Order => 900;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchGroup>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var page = await _history
            .GetAsync(new HistoryQuery(Search: query, Limit: limit), cancellationToken)
            .ConfigureAwait(false);

        if (page.IsFailure)
        {
            return [];
        }

        var hits = page.Value.Records
            .Select(record => new SearchHit(
                record.Title,
                Describe(record),
                DocumentIds.Journal,
                null))
            .ToList();

        return [new SearchGroup("Журнал", Order, hits, page.Value.Total)];
    }

    /// <summary>
    /// Описывает запись журнала строкой пояснения.
    /// </summary>
    /// <param name="record">Запись журнала.</param>
    /// <returns>Персонаж, описание и время.</returns>
    private static string Describe(HistoryRecord record) => string.Join(
        " · ",
        new[]
        {
            record.CharacterName,
            record.Description,
            record.Timestamp.ToString("d MMMM, HH:mm", CultureInfo.CurrentCulture),
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
}
