namespace RPGCharacterManager.Core.Models.History;

/// <summary>
/// Вид события журнала.
///
/// Вид не хранится в базе данных: он выводится из кода действия. Так новая
/// подсистема добавляет свои события, дописав код в <see cref="HistoryActions"/>,
/// и не требует изменения схемы.
/// </summary>
public enum HistoryKind
{
    /// <summary>Любое событие. Используется только для отбора.</summary>
    Any = 0,

    /// <summary>Бросок кубиков.</summary>
    Roll = 1,

    /// <summary>Изменение ресурса: здоровья, заряда, ячейки заклинаний.</summary>
    Resource = 2,

    /// <summary>Применение заклинания.</summary>
    Spell = 3,

    /// <summary>Изменение экипировки.</summary>
    Equipment = 4,

    /// <summary>Действие с предметом.</summary>
    Item = 5,

    /// <summary>Событие самого персонажа: создание, повышение уровня.</summary>
    Character = 6,

    /// <summary>Событие, не отнесённое ни к одному из видов.</summary>
    Other = 7,

    /// <summary>Отдых персонажа.</summary>
    Rest = 8,
}

/// <summary>
/// Коды событий журнала и их разбиение по видам.
///
/// Перечень собран в одном месте, чтобы подсистемы записывали события одинаково,
/// а раздел журнала отбирал их, не зная, какая именно служба сделала запись.
/// </summary>
public static class HistoryActions
{
    /// <summary>Создание персонажа.</summary>
    public const string CharacterCreated = "создание_персонажа";

    /// <summary>Повышение уровня персонажа.</summary>
    public const string LevelGained = "повышение_уровня";

    /// <summary>Пересчёт параметров персонажа.</summary>
    public const string Recalculated = "пересчёт";

    /// <summary>Изменение текущего значения ресурса.</summary>
    public const string ResourceChanged = "изменение_ресурса";

    /// <summary>Отдых персонажа.</summary>
    public const string Rest = "отдых";

    /// <summary>Применение заклинания.</summary>
    public const string SpellCast = "применение_заклинания";

    /// <summary>Использование предмета.</summary>
    public const string ItemUsed = "использование_предмета";

    /// <summary>Атака оружием.</summary>
    public const string WeaponAttack = "атака_оружием";

    /// <summary>
    /// Критическое попадание оружием.
    ///
    /// Записано отдельным кодом, а не признаком у атаки: приложение не знает
    /// правил вашей игры и само крит не определяет — порог задан в оружии, и
    /// критом считается то, что признало критом оружие. Отдельный код делает
    /// это видимым и в журнале, и в статистике, не требуя разбирать описание.
    /// </summary>
    public const string CriticalHit = "критическое_попадание";

    /// <summary>Надевание предмета.</summary>
    public const string ItemEquipped = "надевание_предмета";

    /// <summary>Снятие предмета.</summary>
    public const string ItemUnequipped = "снятие_предмета";

    /// <summary>Бросок кубиков. Записи бросков хранятся отдельно, в журнале бросков.</summary>
    public const string Roll = "бросок";

    /// <summary>
    /// Возвращает вид события по коду действия.
    /// </summary>
    /// <param name="action">Код действия.</param>
    /// <returns>Вид события.</returns>
    public static HistoryKind KindOf(string? action) => action switch
    {
        Roll => HistoryKind.Roll,

        // Атака оружием ведёт к броску и потому отбирается вместе с бросками:
        // игрок ищет её там же, где остальные броски.
        WeaponAttack or CriticalHit => HistoryKind.Roll,
        ResourceChanged => HistoryKind.Resource,
        Rest => HistoryKind.Rest,
        SpellCast => HistoryKind.Spell,
        ItemEquipped or ItemUnequipped => HistoryKind.Equipment,
        ItemUsed => HistoryKind.Item,
        CharacterCreated or LevelGained or Recalculated => HistoryKind.Character,
        _ => HistoryKind.Other,
    };

    /// <summary>
    /// Возвращает название события для строки журнала.
    ///
    /// Название вида отвечает на вопрос «что показывать», а название события —
    /// «что произошло», поэтому оно единственного числа: «Применение заклинания»,
    /// а не «Заклинания».
    /// </summary>
    /// <param name="action">Код действия.</param>
    /// <returns>Название события.</returns>
    public static string Name(string? action) => action switch
    {
        Roll => "Бросок",
        WeaponAttack => "Атака оружием",
        CriticalHit => "Критическое попадание",
        ResourceChanged => "Изменение ресурса",
        Rest => "Отдых",
        SpellCast => "Применение заклинания",
        ItemUsed => "Использование предмета",
        ItemEquipped => "Надевание предмета",
        ItemUnequipped => "Снятие предмета",
        CharacterCreated => "Создание персонажа",
        LevelGained => "Повышение уровня",
        Recalculated => "Пересчёт параметров",
        _ => string.IsNullOrWhiteSpace(action) ? "Событие" : action,
    };

    /// <summary>
    /// Возвращает название вида события для интерфейса.
    /// </summary>
    /// <param name="kind">Вид события.</param>
    /// <returns>Название вида.</returns>
    public static string Title(HistoryKind kind) => kind switch
    {
        HistoryKind.Any => "Все события",
        HistoryKind.Roll => "Броски",
        HistoryKind.Resource => "Ресурсы",
        HistoryKind.Rest => "Отдых",
        HistoryKind.Spell => "Заклинания",
        HistoryKind.Equipment => "Экипировка",
        HistoryKind.Item => "Предметы",
        HistoryKind.Character => "Персонаж",
        _ => "Прочее",
    };

    /// <summary>
    /// Возвращает коды действий, относящиеся к виду события.
    ///
    /// Пустой перечень означает «без отбора по коду» и возвращается для
    /// <see cref="HistoryKind.Any"/> и <see cref="HistoryKind.Other"/>: «прочее»
    /// определяется отсутствием в остальных перечнях, поэтому условием отбора
    /// не предлагается.
    /// </summary>
    /// <param name="kind">Вид события.</param>
    /// <returns>Коды действий вида.</returns>
    public static IReadOnlyList<string> CodesOf(HistoryKind kind) => kind switch
    {
        HistoryKind.Roll => [WeaponAttack, CriticalHit],
        HistoryKind.Resource => [ResourceChanged],
        HistoryKind.Rest => [Rest],
        HistoryKind.Spell => [SpellCast],
        HistoryKind.Equipment => [ItemEquipped, ItemUnequipped],
        HistoryKind.Item => [ItemUsed],
        HistoryKind.Character => [CharacterCreated, LevelGained, Recalculated],
        _ => [],
    };
}
