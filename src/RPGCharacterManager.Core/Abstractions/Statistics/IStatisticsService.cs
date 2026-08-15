using RPGCharacterManager.Core.Abstractions.Dice;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Statistics;

/// <summary>
/// Период наблюдения, за который считается статистика.
/// </summary>
/// <param name="Name">Название периода для интерфейса.</param>
/// <param name="Days">Длина периода в днях; <see langword="null"/> — за всё время.</param>
public sealed record StatisticsPeriod(string Name, int? Days)
{
    /// <summary>Периоды, предлагаемые разделом статистики.</summary>
    public static IReadOnlyList<StatisticsPeriod> All { get; } =
    [
        new("За всё время", null),
        new("За 30 дней", 30),
        new("За 7 дней", 7),
        new("За сутки", 1),
    ];
}

/// <summary>
/// Отбор событий, попадающих в статистику.
/// </summary>
/// <param name="CharacterId">Персонаж; <see langword="null"/> — все персонажи.</param>
/// <param name="Days">Сколько последних дней учитывать; <see langword="null"/> — все.</param>
public sealed record StatisticsQuery(Guid? CharacterId = null, int? Days = null)
{
    /// <summary>
    /// Возвращает момент, начиная с которого события учитываются.
    /// </summary>
    /// <param name="now">Текущий момент.</param>
    /// <returns>Начало периода либо <see langword="null"/>, если период не ограничен.</returns>
    public DateTimeOffset? StartOf(DateTimeOffset now) =>
        Days is { } days and > 0 ? now.AddDays(-days) : null;
}

/// <summary>
/// Статистика одного вида кости.
///
/// Считается по граням, а не по названиям кубиков: кость с двенадцатью гранями
/// остаётся одной и той же костью, как бы её ни назвали в игровой системе.
/// </summary>
/// <param name="Sides">Количество граней.</param>
/// <param name="Casts">Сколько раз кость бросалась.</param>
/// <param name="Total">Сумма выпавших значений.</param>
/// <param name="Maximums">Сколько раз выпала наибольшая грань.</param>
/// <param name="Minimums">Сколько раз выпала наименьшая грань.</param>
public sealed record DieStatistics(int Sides, int Casts, double Total, int Maximums, int Minimums)
{
    /// <summary>Обозначение кости: <c>d20</c>.</summary>
    public string Notation => DiceNotation.Die(Sides);

    /// <summary>Среднее выпавшее значение.</summary>
    public double Average => Casts > 0 ? Total / Casts : 0;

    /// <summary>
    /// Среднее значение честной кости.
    ///
    /// Показывается рядом с настоящим средним: расхождение сразу видно, и это
    /// единственный способ понять, везёт ли игроку или так и должно быть.
    /// </summary>
    public double Expected => (1 + Sides) / 2.0;

    /// <summary>Доля бросков, в которых выпала наибольшая грань.</summary>
    public double MaximumShare => Casts > 0 ? (double)Maximums / Casts : 0;

    /// <summary>Доля бросков, в которых выпала наименьшая грань.</summary>
    public double MinimumShare => Casts > 0 ? (double)Minimums / Casts : 0;
}

/// <summary>
/// Статистика бросков кубиков.
/// </summary>
/// <param name="Count">Количество бросков.</param>
/// <param name="Total">Сумма итогов бросков.</param>
/// <param name="Best">Наибольший итог.</param>
/// <param name="Worst">Наименьший итог.</param>
/// <param name="Advantage">Количество бросков с преимуществом.</param>
/// <param name="Disadvantage">Количество бросков с помехой.</param>
/// <param name="Dice">Статистика по видам костей от частых к редким.</param>
public sealed record RollStatistics(
    int Count,
    double Total,
    double? Best,
    double? Worst,
    int Advantage,
    int Disadvantage,
    IReadOnlyList<DieStatistics> Dice)
{
    /// <summary>Пустая статистика бросков.</summary>
    public static RollStatistics Empty { get; } = new(0, 0, null, null, 0, 0, []);

    /// <summary>Средний итог броска.</summary>
    public double Average => Count > 0 ? Total / Count : 0;

    /// <summary>Общее количество брошенных костей.</summary>
    public int Casts => Dice.Sum(die => die.Casts);

    /// <summary>Есть броски, у которых сохранены выпавшие кости.</summary>
    public bool HasDice => Dice.Count > 0;

    /// <summary>Бросков не было.</summary>
    public bool IsEmpty => Count == 0;
}

/// <summary>
/// Статистика атак одним оружием.
/// </summary>
/// <param name="Name">Название оружия.</param>
/// <param name="Attacks">Количество атак.</param>
/// <param name="Criticals">Количество критических попаданий.</param>
/// <param name="Damage">Суммарный урон.</param>
/// <param name="Best">Наибольший урон одной атаки.</param>
public sealed record WeaponStatistics(string Name, int Attacks, int Criticals, double Damage, double Best)
{
    /// <summary>Средний урон атаки.</summary>
    public double Average => Attacks > 0 ? Damage / Attacks : 0;

    /// <summary>Доля критических попаданий среди атак.</summary>
    public double CriticalShare => Attacks > 0 ? (double)Criticals / Attacks : 0;
}

/// <summary>
/// Статистика атак: криты и урон.
///
/// Критом считается то, что признало критом само оружие: порог критического
/// попадания задаёт пользователь, а приложение правил игры не знает
/// (решение Р-100).
/// </summary>
/// <param name="Attacks">Количество атак.</param>
/// <param name="Criticals">Количество критических попаданий.</param>
/// <param name="Damage">Суммарный урон.</param>
/// <param name="Best">Наибольший урон одной атаки.</param>
/// <param name="Weapons">Статистика по оружию от частого к редкому.</param>
public sealed record AttackStatistics(
    int Attacks,
    int Criticals,
    double Damage,
    double Best,
    IReadOnlyList<WeaponStatistics> Weapons)
{
    /// <summary>Пустая статистика атак.</summary>
    public static AttackStatistics Empty { get; } = new(0, 0, 0, 0, []);

    /// <summary>Средний урон атаки.</summary>
    public double Average => Attacks > 0 ? Damage / Attacks : 0;

    /// <summary>Доля критических попаданий среди атак.</summary>
    public double CriticalShare => Attacks > 0 ? (double)Criticals / Attacks : 0;

    /// <summary>Атак не было.</summary>
    public bool IsEmpty => Attacks == 0;
}

/// <summary>
/// Использование одного заклинания.
/// </summary>
/// <param name="Name">Название заклинания.</param>
/// <param name="Casts">Количество применений.</param>
public sealed record SpellUsage(string Name, int Casts);

/// <summary>
/// Использование одного ресурса.
///
/// Потраченное и восстановленное разделены: ресурс, который за вечер потратили
/// и вернули отдыхом, в сумме даёт ноль, и одно число скрыло бы, что им
/// пользовались вообще.
/// </summary>
/// <param name="Name">Название ресурса.</param>
/// <param name="Changes">Количество изменений.</param>
/// <param name="Spent">Сколько израсходовано.</param>
/// <param name="Restored">Сколько восстановлено.</param>
public sealed record ResourceUsage(string Name, int Changes, double Spent, double Restored)
{
    /// <summary>Итог: восстановлено минус израсходовано.</summary>
    public double Balance => Restored - Spent;
}

/// <summary>
/// Сводка статистики: всё, что показывает раздел за один запрос.
/// </summary>
/// <param name="Rolls">Статистика бросков.</param>
/// <param name="Attacks">Статистика атак: криты и урон.</param>
/// <param name="Spells">Использование заклинаний от частых к редким.</param>
/// <param name="Resources">Использование ресурсов от частых к редким.</param>
public sealed record StatisticsReport(
    RollStatistics Rolls,
    AttackStatistics Attacks,
    IReadOnlyList<SpellUsage> Spells,
    IReadOnlyList<ResourceUsage> Resources)
{
    /// <summary>Пустая сводка.</summary>
    public static StatisticsReport Empty { get; } =
        new(RollStatistics.Empty, AttackStatistics.Empty, [], []);

    /// <summary>Заклинания применялись.</summary>
    public bool HasSpells => Spells.Count > 0;

    /// <summary>Ресурсы менялись.</summary>
    public bool HasResources => Resources.Count > 0;

    /// <summary>Считать нечего: за выбранный период ничего не происходило.</summary>
    public bool IsEmpty => Rolls.IsEmpty && Attacks.IsEmpty && !HasSpells && !HasResources;
}

/// <summary>
/// Статистика игры: что бросали, чем били, что применяли и на что тратили.
///
/// Служба ничего не накапливает и не хранит собственных счётчиков: она считает
/// то, что уже записано в журналах событий и бросков. Свои счётчики рано или
/// поздно разошлись бы с журналом, а очистка журнала оставила бы числа о том,
/// чего в нём уже не видно (решение Р-99).
/// </summary>
public interface IStatisticsService
{
    /// <summary>
    /// Возвращает сводку статистики.
    /// </summary>
    /// <param name="query">Отбор событий.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сводка статистики либо описание ошибки.</returns>
    Task<Result<StatisticsReport>> GetAsync(
        StatisticsQuery query,
        CancellationToken cancellationToken = default);
}
