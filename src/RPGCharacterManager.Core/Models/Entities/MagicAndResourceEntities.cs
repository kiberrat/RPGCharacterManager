namespace RPGCharacterManager.Core.Models.Entities;

/// <summary>
/// Ресурс персонажа: здоровье, мана, ярость, выносливость, патроны,
/// очки действий и любой пользовательский ресурс.
/// </summary>
public class GameResource : ContentEntity
{
    /// <summary>Категория ресурса.</summary>
    public string? Category { get; set; }

    /// <summary>Формула вычисления максимального значения.</summary>
    public string? MaximumFormula { get; set; }

    /// <summary>Формула вычисления начального значения.</summary>
    public string? StartingFormula { get; set; }

    /// <summary>
    /// Правило восстановления ресурса: после короткого отдыха, длительного отдыха,
    /// начала хода или любое пользовательское условие.
    /// </summary>
    public string? RestoreRule { get; set; }

    /// <summary>Цвет полосы ресурса в интерфейсе.</summary>
    public string? Color { get; set; }

    /// <summary>Порядок отображения среди ресурсов.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Текущее состояние ресурса персонажа.
/// </summary>
public class CharacterResource : EntityBase
{
    /// <summary>Идентификатор персонажа.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Персонаж.</summary>
    public Character? Character { get; set; }

    /// <summary>Идентификатор ресурса.</summary>
    public Guid ResourceId { get; set; }

    /// <summary>Ресурс.</summary>
    public GameResource? Resource { get; set; }

    /// <summary>Текущее значение ресурса.</summary>
    public double Current { get; set; }

    /// <summary>Максимальное значение ресурса. Вычисляется движком.</summary>
    public double Maximum { get; set; }
}

/// <summary>
/// Заклинание, техника, ритуал или особое действие.
/// Система не ограничена магией конкретной игры.
/// </summary>
public class Spell : ContentEntity
{
    /// <summary>Уровень или ранг заклинания.</summary>
    public int Level { get; set; }

    /// <summary>Школа магии или её аналог.</summary>
    public string? School { get; set; }

    /// <summary>Категория заклинания: атака, защита, исцеление и другие.</summary>
    public string? Category { get; set; }

    /// <summary>Время применения.</summary>
    public string? CastingTime { get; set; }

    /// <summary>Дальность применения.</summary>
    public string? Range { get; set; }

    /// <summary>Область действия.</summary>
    public string? AreaOfEffect { get; set; }

    /// <summary>Цель заклинания.</summary>
    public string? Target { get; set; }

    /// <summary>Компоненты применения.</summary>
    public string? Components { get; set; }

    /// <summary>Длительность действия.</summary>
    public string? Duration { get; set; }

    /// <summary>Заклинание требует концентрации.</summary>
    public bool RequiresConcentration { get; set; }

    /// <summary>Заклинание может быть применено как ритуал.</summary>
    public bool IsRitual { get; set; }

    /// <summary>Формула вычисления результата: урона, лечения или иного эффекта.</summary>
    public string? Formula { get; set; }

    /// <summary>Формула усиления заклинания при применении на более высоком уровне.</summary>
    public string? ScalingFormula { get; set; }

    /// <summary>Идентификатор расходуемого ресурса.</summary>
    public Guid? ResourceId { get; set; }

    /// <summary>Расходуемый ресурс.</summary>
    public GameResource? Resource { get; set; }

    /// <summary>Формула количества расходуемого ресурса.</summary>
    public string? ResourceCostFormula { get; set; }

    /// <summary>Требования к применению в виде выражения.</summary>
    public string? Requirements { get; set; }
}

/// <summary>
/// Заклинание, известное персонажу.
/// </summary>
public class CharacterSpell : EntityBase
{
    /// <summary>Идентификатор персонажа.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Персонаж.</summary>
    public Character? Character { get; set; }

    /// <summary>Идентификатор заклинания.</summary>
    public Guid SpellId { get; set; }

    /// <summary>Заклинание.</summary>
    public Spell? Spell { get; set; }

    /// <summary>Заклинание подготовлено к применению.</summary>
    public bool IsPrepared { get; set; }

    /// <summary>
    /// Персонаж сейчас концентрируется на этом заклинании.
    /// Концентрация возможна только на одном заклинании: применение следующего
    /// заклинания с концентрацией прерывает предыдущую.
    /// </summary>
    public bool IsConcentrating { get; set; }

    /// <summary>Заклинание отмечено как избранное.</summary>
    public bool IsFavorite { get; set; }

    /// <summary>Количество применений с момента последнего восстановления.</summary>
    public int TimesUsed { get; set; }

    /// <summary>Источник получения заклинания: класс, черта, предмет.</summary>
    public string? Source { get; set; }
}

/// <summary>
/// Окраска эффекта: помогает отличить усиление от вреда с одного взгляда.
/// </summary>
public enum EffectTone
{
    /// <summary>Положительный эффект: бафф, благословение.</summary>
    Positive = 0,

    /// <summary>Отрицательный эффект: дебафф, болезнь, проклятие.</summary>
    Negative = 1,

    /// <summary>Нейтральный эффект: изменение правил, смена состояния.</summary>
    Neutral = 2,
}

/// <summary>
/// Что происходит при повторном наложении того же эффекта.
/// </summary>
public enum EffectStacking
{
    /// <summary>Повторное наложение обновляет длительность, оставляя один экземпляр.</summary>
    Refresh = 0,

    /// <summary>Наложения складываются и умножают величину бонусов эффекта.</summary>
    Sum = 1,

    /// <summary>Повторное наложение запрещено.</summary>
    Forbidden = 2,
}

/// <summary>
/// Эффект: бафф, дебафф, аура, болезнь, проклятие или благословение.
///
/// Приложение не содержит перечня эффектов и не знает, чем болезнь отличается от
/// проклятия: и то и другое описывается категорией, окраской и списком бонусов,
/// которые составляет пользователь.
/// </summary>
public class Effect : ContentEntity
{
    /// <summary>Категория эффекта: болезнь, проклятие, благословение, аура — задаёт пользователь.</summary>
    public string? Category { get; set; }

    /// <summary>Окраска эффекта для цветовой маркировки.</summary>
    public EffectTone Tone { get; set; }

    /// <summary>Формула вычисления величины эффекта.</summary>
    public string? Formula { get; set; }

    /// <summary>
    /// Формула длительности эффекта в единицах <see cref="DurationUnit"/>.
    /// Пустое значение означает эффект без срока: он действует, пока его не снимут
    /// вручную или пока не выполнится <see cref="EndCondition"/>.
    /// </summary>
    public string? DurationFormula { get; set; }

    /// <summary>
    /// Единица длительности: «раунд», «минута», «час», «день» — перечень задаёт
    /// пользователь. Приложение не переводит одни единицы в другие: сколько раундов
    /// в минуте, знает игровая система, а не программа.
    /// </summary>
    public string? DurationUnit { get; set; }

    /// <summary>Условие досрочного прекращения эффекта.</summary>
    public string? EndCondition { get; set; }

    /// <summary>Что происходит при повторном наложении эффекта.</summary>
    public EffectStacking Stacking { get; set; }

    /// <summary>
    /// Наибольшее количество наложений для складывающегося эффекта.
    /// Значение <see langword="null"/> означает, что количество не ограничено.
    /// </summary>
    public int? MaximumStacks { get; set; }

    /// <summary>
    /// Приоритет эффекта. Эффекты применяются и показываются от большего приоритета
    /// к меньшему, поэтому важное усиление видно в списке первым.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>Область действия: «сфера 10 м», «вся группа» — описание для игрока.</summary>
    public string? Area { get; set; }

    /// <summary>Цвет значка эффекта в интерфейсе.</summary>
    public string? Color { get; set; }

    /// <summary>
    /// Что эффект изменяет, пока действует.
    /// Описывается тем же списком бонусов, что и усиления предметов.
    /// </summary>
    public ICollection<EffectBonus> Bonuses { get; set; } = [];
}

/// <summary>
/// Бонус, который эффект даёт персонажу, пока действует.
///
/// Повторяет устройство бонуса предмета: цель, формула и условие. Благодаря этому
/// «+2 к Силе» от кольца и от благословения попадают в расчёт одним и тем же путём.
/// </summary>
public class EffectBonus : EntityBase
{
    /// <summary>Идентификатор эффекта.</summary>
    public Guid EffectId { get; set; }

    /// <summary>Эффект.</summary>
    public Effect? Effect { get; set; }

    /// <summary>Что изменяет бонус.</summary>
    public BonusTargetKind Target { get; set; }

    /// <summary>Идентификатор изменяемой характеристики.</summary>
    public Guid? AttributeId { get; set; }

    /// <summary>Изменяемая характеристика.</summary>
    public AttributeDefinition? Attribute { get; set; }

    /// <summary>Идентификатор изменяемого ресурса.</summary>
    public Guid? ResourceId { get; set; }

    /// <summary>Изменяемый ресурс.</summary>
    public GameResource? Resource { get; set; }

    /// <summary>Имя величины или признака, если бонус не привязан к объекту контента.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Формула величины бонуса. Признаку формула не нужна.
    /// Вычисляется по параметрам персонажа без учёта бонусов.
    /// </summary>
    public string? Formula { get; set; }

    /// <summary>
    /// Условие, при котором бонус действует.
    /// Пустое значение означает, что бонус действует всегда.
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>Порядок отображения бонуса.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Эффект, действующий на персонажа.
/// </summary>
public class CharacterEffect : EntityBase
{
    /// <summary>Идентификатор персонажа.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Персонаж.</summary>
    public Character? Character { get; set; }

    /// <summary>Идентификатор эффекта.</summary>
    public Guid EffectId { get; set; }

    /// <summary>Эффект.</summary>
    public Effect? Effect { get; set; }

    /// <summary>
    /// Оставшаяся длительность, выраженная в единицах самого эффекта:
    /// столько раундов, минут или часов ему ещё действовать.
    /// Значение <see langword="null"/> означает эффект без срока.
    /// </summary>
    public double? RemainingTime { get; set; }

    /// <summary>Количество наложений для складывающихся эффектов.</summary>
    public int Stacks { get; set; } = 1;

    /// <summary>Источник наложения эффекта: заклинание, предмет, черта.</summary>
    public string? Source { get; set; }

    /// <summary>Эффект активен.</summary>
    public bool IsActive { get; set; } = true;
}
