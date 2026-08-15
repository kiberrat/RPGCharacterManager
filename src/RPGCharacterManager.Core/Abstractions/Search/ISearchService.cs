using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Search;

/// <summary>
/// Найденный объект.
/// </summary>
/// <param name="Title">Название объекта.</param>
/// <param name="Subtitle">Пояснение: вид, уровень, дата — то, что различает похожие.</param>
/// <param name="DocumentId">Идентификатор документа, показывающего объект.</param>
/// <param name="Parameter">Значение, с которым открывается документ.</param>
public sealed record SearchHit(string Title, string? Subtitle, string DocumentId, object? Parameter)
{
    /// <summary>У находки есть пояснение.</summary>
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>
/// Группа находок одного вида.
/// </summary>
/// <param name="Title">Название группы: «Заклинания», «Персонажи», «Журнал».</param>
/// <param name="Order">Порядок группы в списке.</param>
/// <param name="Hits">Находки в порядке названий.</param>
/// <param name="Total">Сколько объектов подошло под запрос всего.</param>
public sealed record SearchGroup(string Title, int Order, IReadOnlyList<SearchHit> Hits, int Total)
{
    /// <summary>Найдено больше, чем показано.</summary>
    public bool HasMore => Total > Hits.Count;

    /// <summary>Подпись о количестве находок.</summary>
    public string Caption => HasMore ? $"показано {Hits.Count} из {Total}" : $"найдено: {Total}";
}

/// <summary>
/// Итог поиска.
/// </summary>
/// <param name="Query">Запрос, по которому искали.</param>
/// <param name="Groups">Группы находок в порядке своих номеров.</param>
public sealed record SearchResult(string Query, IReadOnlyList<SearchGroup> Groups)
{
    /// <summary>Общее количество показанных находок.</summary>
    public int Count => Groups.Sum(group => group.Hits.Count);

    /// <summary>Не найдено ничего.</summary>
    public bool IsEmpty => Count == 0;
}

/// <summary>
/// Поставщик находок одного рода: контент, персонажи, кампании, журнал.
///
/// Поиск не знает, где что лежит: каждая подсистема отвечает за себя сама,
/// поэтому подсистема, добавленная на будущем этапе, попадает в глобальный
/// поиск регистрацией своего поставщика и ничего в поиске не меняет
/// (решение Р-96).
/// </summary>
public interface ISearchProvider
{
    /// <summary>Порядок групп этого поставщика в списке находок.</summary>
    int Order { get; }

    /// <summary>
    /// Ищет объекты, подходящие под запрос.
    /// </summary>
    /// <param name="query">Строка запроса; не пустая.</param>
    /// <param name="limit">Наибольшее количество находок в каждой группе.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Группы находок; пустые группы поставщик не возвращает.</returns>
    Task<IReadOnlyList<SearchGroup>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Глобальный поиск по всему, что есть в приложении.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Ищет объекты по запросу.
    /// </summary>
    /// <param name="query">Строка запроса.</param>
    /// <param name="limit">Наибольшее количество находок в каждой группе.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итог поиска либо описание ошибки.</returns>
    Task<Result<SearchResult>> SearchAsync(
        string query,
        int limit = SearchDefaults.GroupLimit,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Пределы поиска.
/// </summary>
public static class SearchDefaults
{
    /// <summary>Сколько находок показывается в каждой группе.</summary>
    public const int GroupLimit = 8;

    /// <summary>Наименьшая длина запроса, с которой начинается поиск.</summary>
    public const int MinimumQueryLength = 2;
}
