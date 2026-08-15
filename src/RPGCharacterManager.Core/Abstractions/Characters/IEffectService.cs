using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Characters;

/// <summary>
/// Изменение, которое эффект вносит в параметры персонажа.
/// </summary>
/// <param name="Description">Что изменяется.</param>
/// <param name="Value">Вычисленная величина с учётом наложений.</param>
/// <param name="Formula">Формула изменения.</param>
/// <param name="Condition">Условие, при котором изменение действует.</param>
/// <param name="IsApplied">Условие выполнено и изменение действует.</param>
public sealed record EffectChange(
    string Description,
    double Value,
    string? Formula,
    string? Condition,
    bool IsApplied);

/// <summary>
/// Эффект, действующий на персонажа, подготовленный к показу.
/// </summary>
/// <param name="CharacterEffectId">Идентификатор наложения.</param>
/// <param name="EffectId">Идентификатор эффекта.</param>
/// <param name="Name">Название эффекта.</param>
/// <param name="Description">Описание эффекта.</param>
/// <param name="Category">Категория эффекта.</param>
/// <param name="Tone">Окраска эффекта.</param>
/// <param name="Color">Цвет значка, заданный пользователем.</param>
/// <param name="Area">Область действия.</param>
/// <param name="Priority">Приоритет эффекта.</param>
/// <param name="Stacks">Количество наложений.</param>
/// <param name="MaximumStacks">Предел наложений.</param>
/// <param name="Stacking">Правило повторного наложения.</param>
/// <param name="Remaining">Оставшаяся длительность в единицах эффекта.</param>
/// <param name="DurationUnit">Единица длительности.</param>
/// <param name="EndCondition">Условие досрочного прекращения.</param>
/// <param name="Source">Источник наложения.</param>
/// <param name="AppliedAt">Момент наложения.</param>
/// <param name="Changes">Что эффект изменяет вместе с вычисленными величинами.</param>
public sealed record ActiveEffect(
    Guid CharacterEffectId,
    Guid EffectId,
    string Name,
    string? Description,
    string? Category,
    EffectTone Tone,
    string? Color,
    string? Area,
    int Priority,
    int Stacks,
    int? MaximumStacks,
    EffectStacking Stacking,
    double? Remaining,
    string? DurationUnit,
    string? EndCondition,
    string? Source,
    DateTimeOffset AppliedAt,
    IReadOnlyList<EffectChange> Changes)
{
    /// <summary>Эффект действует ограниченное время.</summary>
    public bool HasTimer => Remaining is not null;

    /// <summary>Эффект наложен несколько раз.</summary>
    public bool IsStacked => Stacks > 1;

    /// <summary>Эффект что-то изменяет.</summary>
    public bool HasChanges => Changes.Count > 0;
}

/// <summary>
/// Единица длительности, встречающаяся среди действующих эффектов.
///
/// Приложение не переводит одни единицы в другие: сколько раундов в минуте,
/// знает игровая система. Поэтому время продвигается отдельно по каждой единице.
/// </summary>
/// <param name="Unit">Название единицы.</param>
/// <param name="Count">Сколько эффектов измеряется в этой единице.</param>
public sealed record EffectTimerUnit(string Unit, int Count);

/// <summary>
/// Эффекты персонажа, подготовленные к показу.
/// </summary>
/// <param name="Effects">Действующие эффекты от большего приоритета к меньшему.</param>
/// <param name="Units">Единицы длительности, по которым можно продвинуть время.</param>
public sealed record EffectState(
    IReadOnlyList<ActiveEffect> Effects,
    IReadOnlyList<EffectTimerUnit> Units)
{
    /// <summary>На персонажа ничего не наложено.</summary>
    public bool IsEmpty => Effects.Count == 0;
}

/// <summary>
/// Итог продвижения времени.
/// </summary>
/// <param name="Unit">Единица, по которой продвинуто время.</param>
/// <param name="Amount">Насколько продвинуто время.</param>
/// <param name="Expired">Названия эффектов, которые закончились.</param>
public sealed record EffectAdvanceResult(string Unit, double Amount, IReadOnlyList<string> Expired);

/// <summary>
/// Эффекты персонажа: баффы, дебаффы, ауры, болезни, проклятия, благословения
/// и их таймеры.
///
/// Подсистема не содержит перечня эффектов и не различает болезнь и проклятие:
/// и то и другое описано категорией, окраской и списком изменений, которые
/// составляет пользователь. Все изменения попадают в расчёт персонажа тем же
/// путём, что и бонусы надетых предметов.
/// </summary>
public interface IEffectService
{
    /// <summary>
    /// Возвращает эффекты, действующие на персонажа, вместе с вычисленными
    /// изменениями и единицами длительности.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Состояние эффектов либо описание ошибки.</returns>
    Task<Result<EffectState>> GetAsync(
        Guid characterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает эффекты, которые можно наложить на персонажа.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="search">Строка поиска по названию.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница вариантов выбора.</returns>
    Task<CharacterOptionPage> GetAvailableEffectsAsync(
        Guid characterId,
        string? search,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Накладывает эффект на персонажа по правилу повторного наложения:
    /// обновляет длительность, добавляет наложение или отказывает.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="effectId">Идентификатор эффекта.</param>
    /// <param name="source">Источник наложения: заклинание, предмет, событие.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат наложения.</returns>
    Task<Result> ApplyAsync(
        Guid characterId,
        Guid effectId,
        string? source = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Снимает эффект с персонажа целиком, со всеми его наложениями.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="characterEffectId">Идентификатор наложения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат снятия.</returns>
    Task<Result> RemoveAsync(
        Guid characterId,
        Guid characterEffectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Убирает одно наложение складывающегося эффекта.
    /// Последнее наложение снимает эффект целиком.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="characterEffectId">Идентификатор наложения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат изменения.</returns>
    Task<Result> RemoveStackAsync(
        Guid characterId,
        Guid characterEffectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Продвигает время на заданное количество единиц.
    ///
    /// Убавляется длительность только тех эффектов, которые измеряются этой же
    /// единицей: приложение не знает, сколько раундов в минуте, и не пытается
    /// переводить одни единицы в другие. Эффекты с истёкшей длительностью снимаются.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="unit">Единица длительности.</param>
    /// <param name="amount">Насколько продвинуть время.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итог продвижения либо описание ошибки.</returns>
    Task<Result<EffectAdvanceResult>> AdvanceAsync(
        Guid characterId,
        string unit,
        double amount,
        CancellationToken cancellationToken = default);
}
