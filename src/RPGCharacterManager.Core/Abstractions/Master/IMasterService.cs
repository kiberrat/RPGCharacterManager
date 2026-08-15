using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Master;

/// <summary>
/// Значение ресурса персонажа в сводке мастера.
/// </summary>
/// <param name="ResourceId">Идентификатор ресурса.</param>
/// <param name="Name">Название ресурса.</param>
/// <param name="Current">Текущее значение.</param>
/// <param name="Maximum">Максимальное значение.</param>
/// <param name="Color">Цвет полосы, заданный пользователем.</param>
public sealed record MasterResource(
    Guid ResourceId,
    string Name,
    double Current,
    double Maximum,
    string? Color)
{
    /// <summary>Доля заполнения от нуля до единицы.</summary>
    public double Share => Maximum > 0 ? Math.Clamp(Current / Maximum, 0, 1) : 0;

    /// <summary>Ресурс исчерпан.</summary>
    public bool IsEmpty => Current <= 0;
}

/// <summary>
/// Эффект, действующий на персонажа, в сводке мастера.
/// </summary>
/// <param name="EffectId">Идентификатор эффекта.</param>
/// <param name="Name">Название эффекта.</param>
/// <param name="Tone">Окраска эффекта.</param>
/// <param name="Color">Цвет значка, заданный пользователем.</param>
/// <param name="Stacks">Количество наложений.</param>
public sealed record MasterEffect(
    Guid EffectId,
    string Name,
    EffectTone Tone,
    string? Color,
    int Stacks)
{
    /// <summary>Название с количеством наложений, если их несколько.</summary>
    public string Caption => Stacks > 1 ? $"{Name} ×{Stacks}" : Name;

    /// <summary>Эффект полезен персонажу.</summary>
    public bool IsPositive => Tone == EffectTone.Positive;

    /// <summary>Эффект вреден персонажу.</summary>
    public bool IsNegative => Tone == EffectTone.Negative;

    /// <summary>Эффект не полезен и не вреден.</summary>
    public bool IsNeutral => Tone == EffectTone.Neutral;
}

/// <summary>
/// Строка сводки мастера: персонаж со всем, что нужно видеть за столом.
/// </summary>
/// <param name="Id">Идентификатор персонажа.</param>
/// <param name="Name">Имя персонажа.</param>
/// <param name="Level">Уровень.</param>
/// <param name="Player">Имя игрока, если персонаж состоит в кампании.</param>
/// <param name="RaceName">Название расы.</param>
/// <param name="ClassName">Название класса.</param>
/// <param name="Portrait">Путь к изображению персонажа.</param>
/// <param name="Resources">Ресурсы персонажа в порядке отображения.</param>
/// <param name="Effects">Действующие эффекты.</param>
/// <param name="Initiative">Значение инициативы; пусто — участник вне очереди.</param>
/// <param name="IsCurrentTurn">Сейчас ход этого персонажа.</param>
public sealed record MasterCharacter(
    Guid Id,
    string Name,
    int Level,
    string? Player,
    string? RaceName,
    string? ClassName,
    string? Portrait,
    IReadOnlyList<MasterResource> Resources,
    IReadOnlyList<MasterEffect> Effects,
    double? Initiative,
    bool IsCurrentTurn)
{
    /// <summary>Персонаж состоит в очереди хода.</summary>
    public bool HasInitiative => Initiative.HasValue;

    /// <summary>На персонажа что-то наложено.</summary>
    public bool HasEffects => Effects.Count > 0;

    /// <summary>Краткое описание: раса, класс и уровень.</summary>
    public string Summary => string.Join(
        " · ",
        new[] { RaceName, ClassName, $"уровень {Level}" }.Where(part => !string.IsNullOrWhiteSpace(part)));
}

/// <summary>
/// Вариант выбора в панелях мастера: ресурс, эффект или кампания.
/// </summary>
/// <param name="Id">Идентификатор объекта.</param>
/// <param name="Name">Название объекта.</param>
public sealed record MasterOption(Guid Id, string Name);

/// <summary>
/// Состояние очереди хода.
///
/// Очередь существует, только если игровая система задала формулу инициативы:
/// порядок хода есть не во всякой игре, и приложение не навязывает его
/// (решение Р-92).
/// </summary>
/// <param name="IsEnabled">Система определяет порядок хода.</param>
/// <param name="Formula">Формула инициативы игровой системы.</param>
/// <param name="Round">Номер текущего раунда.</param>
/// <param name="IsStarted">Очередь заполнена и бой идёт.</param>
/// <param name="DisabledReason">Почему очередь недоступна.</param>
public sealed record InitiativeState(
    bool IsEnabled,
    string? Formula,
    int Round,
    bool IsStarted,
    string? DisabledReason)
{
    /// <summary>Очередь, которой нет ни в одной игровой системе базы.</summary>
    /// <param name="reason">Причина недоступности.</param>
    /// <returns>Состояние выключенной очереди.</returns>
    public static InitiativeState Disabled(string reason) => new(false, null, 1, false, reason);
}

/// <summary>
/// Сводка мастера: всё, что показывает раздел за один запрос.
/// </summary>
/// <param name="Characters">Персонажи в порядке очереди хода, затем по имени.</param>
/// <param name="Resources">Ресурсы, встречающиеся у показанных персонажей.</param>
/// <param name="Campaigns">Кампании для отбора персонажей.</param>
/// <param name="Initiative">Состояние очереди хода.</param>
public sealed record MasterBoard(
    IReadOnlyList<MasterCharacter> Characters,
    IReadOnlyList<MasterOption> Resources,
    IReadOnlyList<MasterOption> Campaigns,
    InitiativeState Initiative)
{
    /// <summary>Показывать нечего.</summary>
    public bool IsEmpty => Characters.Count == 0;
}

/// <summary>
/// Итог массового действия.
///
/// Отказ по одному персонажу не отменяет остальных: за столом важнее применить
/// урон ко всем, кого он задел, чем сохранить всё-или-ничего.
/// </summary>
/// <param name="Changed">Сколько персонажей изменилось.</param>
/// <param name="Failures">Причины отказов с именами персонажей.</param>
public sealed record MassResult(int Changed, IReadOnlyList<string> Failures)
{
    /// <summary>Все действия удались.</summary>
    public bool IsComplete => Failures.Count == 0;
}

/// <summary>
/// Режим мастера: ведение игровой сессии за всех персонажей сразу.
///
/// Подсистема ничего не считает сама: изменение ресурса, наложение эффекта
/// и вычисление инициативы выполняют те же службы и тот же движок формул,
/// что и на листе одного персонажа. Режим мастера лишь повторяет действие
/// для нескольких персонажей и собирает общую сводку.
/// </summary>
public interface IMasterService
{
    /// <summary>
    /// Возвращает сводку по персонажам.
    /// </summary>
    /// <param name="campaignId">Кампания; <see langword="null"/> — все персонажи.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сводка мастера либо описание ошибки.</returns>
    Task<Result<MasterBoard>> GetBoardAsync(
        Guid? campaignId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает эффекты, которые можно наложить.
    /// </summary>
    /// <param name="search">Строка поиска по названию.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Варианты выбора в порядке названий.</returns>
    Task<IReadOnlyList<MasterOption>> GetEffectsAsync(
        string? search,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Изменяет ресурс у нескольких персонажей на одну и ту же величину.
    ///
    /// Так наносится урон и раздаётся лечение: «хиты» — обычный ресурс, поэтому
    /// тем же действием восполняется мана, ярость или заряды посоха.
    /// </summary>
    /// <param name="characterIds">Персонажи.</param>
    /// <param name="resourceId">Ресурс.</param>
    /// <param name="delta">На сколько изменить: отрицательное значение отнимает.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итог массового изменения.</returns>
    Task<Result<MassResult>> ChangeResourceAsync(
        IReadOnlyCollection<Guid> characterIds,
        Guid resourceId,
        double delta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Накладывает эффект на нескольких персонажей.
    /// </summary>
    /// <param name="characterIds">Персонажи.</param>
    /// <param name="effectId">Эффект.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итог массового наложения.</returns>
    Task<Result<MassResult>> ApplyEffectAsync(
        IReadOnlyCollection<Guid> characterIds,
        Guid effectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Снимает эффект с нескольких персонажей.
    /// </summary>
    /// <param name="characterIds">Персонажи.</param>
    /// <param name="effectId">Эффект.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итог массового снятия.</returns>
    Task<Result<MassResult>> RemoveEffectAsync(
        IReadOnlyCollection<Guid> characterIds,
        Guid effectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Бросает инициативу выбранным персонажам и строит очередь хода заново.
    /// </summary>
    /// <param name="campaignId">Кампания очереди.</param>
    /// <param name="characterIds">Участники боя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итог броска инициативы.</returns>
    Task<Result<MassResult>> RollInitiativeAsync(
        Guid? campaignId,
        IReadOnlyCollection<Guid> characterIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Задаёт значение инициативы вручную и переставляет участника в очереди.
    /// </summary>
    /// <param name="campaignId">Кампания очереди.</param>
    /// <param name="characterId">Персонаж.</param>
    /// <param name="value">Значение инициативы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат изменения.</returns>
    Task<Result> SetInitiativeAsync(
        Guid? campaignId,
        Guid characterId,
        double value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Передаёт ход следующему участнику очереди, начиная новый раунд после последнего.
    /// </summary>
    /// <param name="campaignId">Кампания очереди.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат передачи хода.</returns>
    Task<Result> NextTurnAsync(Guid? campaignId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Завершает бой: очищает очередь хода и возвращает счётчик раундов к первому.
    /// </summary>
    /// <param name="campaignId">Кампания очереди.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат завершения боя.</returns>
    Task<Result> EndCombatAsync(Guid? campaignId, CancellationToken cancellationToken = default);
}
