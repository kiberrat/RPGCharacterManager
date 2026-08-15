using RPGCharacterManager.Core.Abstractions.Data;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Characters;

/// <summary>
/// Строка списка персонажей.
/// Содержит только отображаемые сведения, чтобы список оставался быстрым
/// при десятках тысяч персонажей.
/// </summary>
/// <param name="Id">Идентификатор персонажа.</param>
/// <param name="Name">Имя персонажа.</param>
/// <param name="Level">Текущий уровень.</param>
/// <param name="GameSystemName">Название игровой системы.</param>
/// <param name="RaceName">Название расы.</param>
/// <param name="ClassName">Название класса.</param>
/// <param name="Portrait">Путь к изображению персонажа.</param>
/// <param name="ModifiedAt">Момент последнего изменения.</param>
public sealed record CharacterListItem(
    Guid Id,
    string Name,
    int Level,
    string? GameSystemName,
    string? RaceName,
    string? ClassName,
    string? Portrait,
    DateTimeOffset ModifiedAt);

/// <summary>
/// Отчёт об изменении персонажа: повышении уровня либо пересчёте.
/// </summary>
/// <param name="CharacterName">Имя персонажа.</param>
/// <param name="PreviousLevel">Уровень до изменения.</param>
/// <param name="CurrentLevel">Уровень после изменения.</param>
/// <param name="Changes">Описания изменившихся значений.</param>
/// <param name="AppliedRules">Названия применённых правил.</param>
/// <param name="Issues">Замечания, найденные при пересчёте.</param>
public sealed record CharacterUpdateReport(
    string CharacterName,
    int PreviousLevel,
    int CurrentLevel,
    IReadOnlyList<string> Changes,
    IReadOnlyList<string> AppliedRules,
    IReadOnlyList<CharacterIssue> Issues);

/// <summary>
/// Хранение и изменение созданных персонажей.
/// </summary>
public interface ICharacterService
{
    /// <summary>
    /// Возвращает страницу персонажей, отфильтрованных по имени.
    /// </summary>
    /// <param name="search">Строка поиска по имени.</param>
    /// <param name="pageIndex">Номер страницы, начиная с нуля.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница персонажей и общее количество найденных записей.</returns>
    Task<PagedResult<CharacterListItem>> SearchAsync(
        string? search,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Загружает персонажа вместе со связанными данными.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Персонаж или <see langword="null"/>, если он не найден.</returns>
    Task<Character?> GetAsync(Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет персонажа вместе со всеми его данными.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат удаления.</returns>
    Task<Result> DeleteAsync(Guid characterId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Развитие персонажа: повышение уровня и автоматическое обновление параметров.
/// </summary>
public interface ICharacterProgressionService
{
    /// <summary>
    /// Повышает уровень персонажа, применяет правила события повышения уровня
    /// и пересчитывает зависящие от уровня значения.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="levels">Количество уровней, на которое повышается персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Отчёт о произошедших изменениях.</returns>
    Task<Result<CharacterUpdateReport>> LevelUpAsync(
        Guid characterId,
        int levels = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Пересчитывает параметры персонажа по текущим формулам и правилам.
    /// Вызывается после изменения контента, правил или самого персонажа.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Отчёт о произошедших изменениях.</returns>
    Task<Result<CharacterUpdateReport>> RecalculateAsync(
        Guid characterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Применяет к персонажу заданный набор действий и сохраняет результат.
    ///
    /// Действия и условие описаны теми же структурами, что и в правилах,
    /// и выполняются тем же движком. Метод нужен подсистемам, которые сами
    /// решают, что выполнить, — например макросам: запись персонажа остаётся
    /// делом подсистемы персонажей, а состав действий приходит извне.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="name">Название выполняемого набора действий для отчёта и журнала.</param>
    /// <param name="trigger">Ключ события, записываемый в отчёт.</param>
    /// <param name="condition">Условие выполнения; <see langword="null"/> — выполнять всегда.</param>
    /// <param name="actions">Выполняемые действия в порядке применения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Отчёт о произошедших изменениях.</returns>
    Task<Result<CharacterUpdateReport>> ApplyActionsAsync(
        Guid characterId,
        string name,
        string trigger,
        Rules.RuleCondition? condition,
        IReadOnlyList<Rules.RuleAction> actions,
        CancellationToken cancellationToken = default);
}
