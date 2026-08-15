using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Characters;

/// <summary>
/// Заклинание персонажа, подготовленное к показу в книге заклинаний.
/// </summary>
/// <param name="CharacterSpellId">Идентификатор записи книги заклинаний.</param>
/// <param name="SpellId">Идентификатор заклинания.</param>
/// <param name="Name">Название заклинания.</param>
/// <param name="Description">Описание заклинания.</param>
/// <param name="Level">Уровень заклинания. Ноль — кантрип.</param>
/// <param name="School">Школа магии или её аналог.</param>
/// <param name="Category">Категория заклинания.</param>
/// <param name="CastingTime">Время применения.</param>
/// <param name="Range">Дальность.</param>
/// <param name="Duration">Длительность.</param>
/// <param name="Components">Компоненты применения.</param>
/// <param name="RequiresConcentration">Заклинание требует концентрации.</param>
/// <param name="IsRitual">Заклинание можно применить как ритуал.</param>
/// <param name="IsPrepared">Заклинание подготовлено.</param>
/// <param name="IsConcentrating">Персонаж концентрируется на этом заклинании.</param>
/// <param name="TimesUsed">Количество применений.</param>
/// <param name="Source">Источник получения заклинания.</param>
/// <param name="ResourceName">Название расходуемого ресурса.</param>
/// <param name="ResourceCost">Стоимость применения на базовом уровне.</param>
/// <param name="ExpectedRange">Границы результата на базовом уровне.</param>
/// <param name="HasScaling">У заклинания есть формула усиления.</param>
/// <param name="CanCast">Заклинание можно применить прямо сейчас.</param>
/// <param name="BlockedReason">Причина, по которой применение невозможно.</param>
public sealed record SpellbookEntry(
    Guid CharacterSpellId,
    Guid SpellId,
    string Name,
    string? Description,
    int Level,
    string? School,
    string? Category,
    string? CastingTime,
    string? Range,
    string? Duration,
    string? Components,
    bool RequiresConcentration,
    bool IsRitual,
    bool IsPrepared,
    bool IsConcentrating,
    int TimesUsed,
    string? Source,
    string? ResourceName,
    double? ResourceCost,
    string? ExpectedRange,
    bool HasScaling,
    bool CanCast,
    string? BlockedReason);

/// <summary>
/// Уровень книги заклинаний вместе с относящимися к нему заклинаниями.
/// </summary>
/// <param name="Level">Уровень заклинаний. Ноль — кантрипы.</param>
/// <param name="Title">Название раздела: «Кантрипы», «1 уровень» и далее.</param>
/// <param name="Spells">Заклинания уровня в алфавитном порядке.</param>
public sealed record SpellbookLevel(int Level, string Title, IReadOnlyList<SpellbookEntry> Spells);

/// <summary>
/// Предел книги заклинаний вместе с текущим количеством.
/// </summary>
/// <param name="Count">Сколько заклинаний занято.</param>
/// <param name="Limit">
/// Сколько разрешено игровой системой
/// либо <see langword="null"/>, если система не ограничивает.
/// </param>
public sealed record SpellbookLimit(int Count, int? Limit)
{
    /// <summary>Предел исчерпан.</summary>
    public bool IsReached => Limit is { } limit && Count >= limit;
}

/// <summary>
/// Запись истории применения заклинаний.
/// </summary>
/// <param name="Description">Что произошло.</param>
/// <param name="Value">Итог применения.</param>
/// <param name="CastAt">Момент применения.</param>
public sealed record SpellCastRecord(string Description, string? Value, DateTimeOffset CastAt);

/// <summary>
/// Книга заклинаний персонажа, подготовленная к показу.
/// </summary>
/// <param name="Levels">Уровни книги в порядке возрастания.</param>
/// <param name="Known">Предел известных заклинаний.</param>
/// <param name="Prepared">Предел подготовленных заклинаний.</param>
/// <param name="UsesPreparation">Игровая система пользуется подготовкой.</param>
/// <param name="ConcentratingOn">Название заклинания концентрации либо <see langword="null"/>.</param>
/// <param name="History">Последние применения заклинаний, новые сверху.</param>
public sealed record SpellbookState(
    IReadOnlyList<SpellbookLevel> Levels,
    SpellbookLimit Known,
    SpellbookLimit Prepared,
    bool UsesPreparation,
    string? ConcentratingOn,
    IReadOnlyList<SpellCastRecord> History)
{
    /// <summary>Персонаж концентрируется на заклинании.</summary>
    public bool IsConcentrating => ConcentratingOn is not null;

    /// <summary>Книга заклинаний пуста.</summary>
    public bool IsEmpty => Levels.Count == 0;
}

/// <summary>
/// Итог применения заклинания.
/// </summary>
/// <param name="SpellName">Название применённого заклинания.</param>
/// <param name="CastLevel">Уровень, на котором заклинание применено.</param>
/// <param name="Result">Вычисленный результат либо <see langword="null"/>, если формулы нет.</param>
/// <param name="ResourceName">Название израсходованного ресурса.</param>
/// <param name="ResourceSpent">Израсходованное количество ресурса.</param>
/// <param name="ResourceRemaining">Остаток ресурса после применения.</param>
/// <param name="BrokeConcentration">Название заклинания, концентрация на котором прервана.</param>
/// <param name="IsConcentrating">Персонаж концентрируется на применённом заклинании.</param>
/// <param name="Issues">Замечания вычисления формул.</param>
public sealed record SpellCastResult(
    string SpellName,
    int CastLevel,
    double? Result,
    string? ResourceName,
    double ResourceSpent,
    double? ResourceRemaining,
    string? BrokeConcentration,
    bool IsConcentrating,
    IReadOnlyList<string> Issues);

/// <summary>
/// Книга заклинаний персонажа: изучение, подготовка и применение.
///
/// Подсистема не знает правил ни одной игры. Уровни, школы, ресурсы и стоимость
/// задаёт пользователь; пределы известных и подготовленных заклинаний — формулы
/// игровой системы; результат и усиление считает единый движок формул.
/// </summary>
public interface ISpellbookService
{
    /// <summary>
    /// Возвращает книгу заклинаний персонажа: уровни, пределы,
    /// концентрацию и историю применения.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="search">Строка поиска по названию, школе и категории.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Книга заклинаний либо описание ошибки.</returns>
    Task<Result<SpellbookState>> GetAsync(
        Guid characterId,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает заклинания, которые персонаж может выучить.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="search">Строка поиска по названию.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница вариантов выбора.</returns>
    Task<CharacterOptionPage> GetAvailableSpellsAsync(
        Guid characterId,
        string? search,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Изучает заклинание, если предел известных заклинаний не исчерпан.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="spellId">Идентификатор заклинания.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат изучения.</returns>
    Task<Result> LearnAsync(
        Guid characterId,
        Guid spellId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Забывает заклинание: убирает его из книги персонажа.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="characterSpellId">Идентификатор записи книги заклинаний.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат удаления.</returns>
    Task<Result> ForgetAsync(
        Guid characterId,
        Guid characterSpellId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Подготавливает заклинание или снимает подготовку.
    /// Подготовка ограничена формулой игровой системы, если она задана.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="characterSpellId">Идентификатор записи книги заклинаний.</param>
    /// <param name="prepared">Заклинание должно стать подготовленным.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат изменения.</returns>
    Task<Result> SetPreparedAsync(
        Guid characterId,
        Guid characterSpellId,
        bool prepared,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Применяет заклинание: проверяет требования и подготовку, расходует ресурс,
    /// вычисляет результат с усилением, переключает концентрацию и записывает
    /// применение в историю.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="characterSpellId">Идентификатор записи книги заклинаний.</param>
    /// <param name="castLevel">
    /// Уровень применения. Значение <see langword="null"/> означает уровень заклинания.
    /// </param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итог применения либо описание ошибки.</returns>
    Task<Result<SpellCastResult>> CastAsync(
        Guid characterId,
        Guid characterSpellId,
        int? castLevel = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Прерывает концентрацию персонажа.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат прерывания.</returns>
    Task<Result> StopConcentrationAsync(
        Guid characterId,
        CancellationToken cancellationToken = default);
}
