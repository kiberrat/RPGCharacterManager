using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.History;

/// <summary>
/// Запись журнала событий.
/// </summary>
/// <param name="Id">Идентификатор записи.</param>
/// <param name="Timestamp">Момент события.</param>
/// <param name="Action">Код действия.</param>
/// <param name="Kind">Вид события.</param>
/// <param name="Title">Название события для интерфейса.</param>
/// <param name="Description">Описание события.</param>
/// <param name="OldValue">Значение до изменения.</param>
/// <param name="NewValue">Значение после изменения.</param>
/// <param name="CharacterId">Персонаж, к которому относится событие.</param>
/// <param name="CharacterName">Имя персонажа.</param>
public sealed record HistoryRecord(
    Guid Id,
    DateTimeOffset Timestamp,
    string Action,
    HistoryKind Kind,
    string Title,
    string? Description,
    string? OldValue,
    string? NewValue,
    Guid? CharacterId,
    string? CharacterName)
{
    /// <summary>Событие относится к конкретному персонажу.</summary>
    public bool HasCharacter => CharacterId.HasValue;

    /// <summary>Событие содержит изменение значения.</summary>
    public bool HasChange => OldValue is not null || NewValue is not null;
}

/// <summary>
/// Отбор записей журнала.
/// </summary>
/// <param name="Characters">
/// Персонажи, чьи события нужны. Пустой список — события всех персонажей
/// и события, не связанные ни с одним из них.
///
/// Отбор задан набором, а не одним персонажем: лист персонажа показывает его
/// одного, а режим мастера — сразу всю партию, и оба случая должны обслуживаться
/// одним запросом, иначе у журнала появилось бы два способа отбора.
/// </param>
/// <param name="Kind">Вид события; <see cref="HistoryKind.Any"/> — любой.</param>
/// <param name="Search">Строка поиска по описанию и названию.</param>
/// <param name="Limit">Наибольшее количество записей.</param>
public sealed record HistoryQuery(
    IReadOnlyCollection<Guid>? Characters = null,
    HistoryKind Kind = HistoryKind.Any,
    string? Search = null,
    int Limit = HistoryQuery.DefaultLimit)
{
    /// <summary>Количество записей, показываемых без запроса продолжения.</summary>
    public const int DefaultLimit = 100;

    /// <summary>
    /// Создаёт отбор по одному персонажу.
    /// </summary>
    /// <param name="characterId">Персонаж; <see langword="null"/> — все записи.</param>
    /// <param name="kind">Вид события.</param>
    /// <param name="search">Строка поиска.</param>
    /// <param name="limit">Наибольшее количество записей.</param>
    /// <returns>Отбор записей журнала.</returns>
    public static HistoryQuery ForCharacter(
        Guid? characterId,
        HistoryKind kind = HistoryKind.Any,
        string? search = null,
        int limit = DefaultLimit) =>
        new(characterId is { } id ? [id] : null, kind, search, limit);
}

/// <summary>
/// Страница журнала.
/// </summary>
/// <param name="Records">Записи от новых к старым.</param>
/// <param name="Total">Общее количество записей, подходящих под отбор.</param>
public sealed record HistoryPage(IReadOnlyList<HistoryRecord> Records, int Total)
{
    /// <summary>Записей больше, чем показано.</summary>
    public bool HasMore => Records.Count < Total;
}

/// <summary>
/// Персонаж в списке отбора журнала.
/// </summary>
/// <param name="Id">Идентификатор персонажа.</param>
/// <param name="Name">Имя персонажа.</param>
public sealed record HistoryCharacter(Guid Id, string Name);

/// <summary>
/// Журнал событий: что происходило с персонажами и когда.
///
/// Служба только читает и очищает журнал. Записи создают сами подсистемы: бросок
/// записывает подсистема бросков, расход ресурса — та служба, которая его
/// израсходовала. Только она знает старое значение и причину изменения, и её
/// запись сохраняется той же операцией, что и само изменение, поэтому событие
/// не может попасть в журнал, не случившись, — и наоборот.
/// </summary>
public interface IHistoryService
{
    /// <summary>
    /// Возвращает страницу журнала.
    /// </summary>
    /// <param name="query">Отбор записей.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница журнала.</returns>
    Task<Result<HistoryPage>> GetAsync(HistoryQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает персонажей, события которых есть в журнале.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Персонажи в порядке имён.</returns>
    Task<Result<IReadOnlyList<HistoryCharacter>>> GetCharactersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Очищает журнал.
    ///
    /// Удаляются и события действий, и броски: пользователь видит их одним
    /// списком и вправе ожидать, что очистка убирает именно то, что показано.
    /// Любимые броски сохраняются — их отметили, чтобы к ним возвращаться.
    /// </summary>
    /// <param name="characterId">Персонаж; <see langword="null"/> — весь журнал.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество удалённых записей.</returns>
    Task<Result<int>> ClearAsync(Guid? characterId, CancellationToken cancellationToken = default);
}
