using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Statistics;
using RPGCharacterManager.Core.Models.Dice;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Statistics;

/// <summary>
/// Статистика игры: считает то, что уже записано в журналах.
///
/// Своих счётчиков подсистема не ведёт (решение Р-99). События приходят из тех же
/// двух хранилищ, что и в журнал: действия — из журнала событий, броски — из
/// журнала бросков. Сводки по действиям считает сама база данных, потому что
/// журнал рассчитан на сотни тысяч записей и переносить его в память ради
/// нескольких сумм незачем.
/// </summary>
public sealed class StatisticsService : IStatisticsService
{
    /// <summary>Название строки для событий, у которых название объекта не записано.</summary>
    public const string UnnamedSubject = "Без названия";

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly ILogger<StatisticsService> _logger;

    /// <summary>
    /// Создаёт службу статистики.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="logger">Журналировщик.</param>
    public StatisticsService(
        IDbContextFactory<RpgDbContext> contextFactory,
        ILogger<StatisticsService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<StatisticsReport>> GetAsync(
        StatisticsQuery query,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(query);

        var start = query.StartOf(DateTimeOffset.UtcNow);

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var rolls = await BuildRollsAsync(context, query, start, cancellationToken)
                .ConfigureAwait(false);

            var attacks = await BuildAttacksAsync(context, query, start, cancellationToken)
                .ConfigureAwait(false);

            var spells = await BuildSpellsAsync(context, query, start, cancellationToken)
                .ConfigureAwait(false);

            var resources = await BuildResourcesAsync(context, query, start, cancellationToken)
                .ConfigureAwait(false);

            StatisticsLog.ReportBuilt(_logger, rolls.Count, attacks.Attacks);

            return Result.Success(new StatisticsReport(rolls, attacks, spells, resources));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatisticsLog.ReportFailed(_logger, exception);

            return Result.Failure<StatisticsReport>(
                "Не удалось собрать статистику. Подробности записаны в журнал.");
        }
    }

    /// <summary>
    /// Считает статистику бросков.
    ///
    /// Записи читаются потоком, а не списком: выпавшие кости хранятся внутри записи
    /// броска, поэтому разобрать их может только приложение, и журнал в миллион
    /// бросков не должен для этого целиком оказаться в памяти.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="query">Отбор событий.</param>
    /// <param name="start">Начало периода.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Статистика бросков.</returns>
    private static async Task<RollStatistics> BuildRollsAsync(
        RpgDbContext context,
        StatisticsQuery query,
        DateTimeOffset? start,
        CancellationToken cancellationToken)
    {
        var records = Filter(context.DiceHistory.AsNoTracking(), query, start)
            .Select(roll => new RollRow(
                roll.Result,
                roll.DetailsJson,
                roll.HasAdvantage,
                roll.HasDisadvantage));

        var count = 0;
        var total = 0d;
        double? best = null;
        double? worst = null;
        var advantage = 0;
        var disadvantage = 0;
        var dice = new Dictionary<int, DieCounter>();

        await foreach (var roll in records.AsAsyncEnumerable()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            count++;
            total += roll.Result;

            best = best is { } previousBest ? Math.Max(previousBest, roll.Result) : roll.Result;
            worst = worst is { } previousWorst ? Math.Min(previousWorst, roll.Result) : roll.Result;

            if (roll.HasAdvantage)
            {
                advantage++;
            }

            if (roll.HasDisadvantage)
            {
                disadvantage++;
            }

            foreach (var cast in RollDetails.ChosenDice(roll.DetailsJson, roll.Result))
            {
                if (cast.Sides <= 0)
                {
                    continue;
                }

                if (!dice.TryGetValue(cast.Sides, out var counter))
                {
                    counter = new DieCounter();
                    dice[cast.Sides] = counter;
                }

                counter.Add(cast.Value, cast.Sides);
            }
        }

        var byDie = dice
            .Select(pair => new DieStatistics(
                pair.Key,
                pair.Value.Casts,
                pair.Value.Total,
                pair.Value.Maximums,
                pair.Value.Minimums))
            .OrderByDescending(die => die.Casts)
            .ThenBy(die => die.Sides)
            .ToList();

        return new RollStatistics(count, total, best, worst, advantage, disadvantage, byDie);
    }

    /// <summary>
    /// Считает статистику атак: криты и урон.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="query">Отбор событий.</param>
    /// <param name="start">Начало периода.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Статистика атак.</returns>
    private static async Task<AttackStatistics> BuildAttacksAsync(
        RpgDbContext context,
        StatisticsQuery query,
        DateTimeOffset? start,
        CancellationToken cancellationToken)
    {
        // Критическое попадание — отдельное действие журнала, поэтому и криты,
        // и урон считаются одним обходом записей об атаках.
        var codes = HistoryActions.CodesOf(Core.Models.History.HistoryKind.Roll);

        var weapons = await Filter(context.History.AsNoTracking(), query, start)
            .Where(entry => codes.Contains(entry.Action))
            .GroupBy(entry => entry.Subject)
            .Select(group => new
            {
                Name = group.Key,
                Attacks = group.Count(),
                Criticals = group.Sum(entry => entry.Action == HistoryActions.CriticalHit ? 1 : 0),
                Damage = group.Sum(entry => entry.Amount ?? 0),
                Best = group.Max(entry => entry.Amount ?? 0),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (weapons.Count == 0)
        {
            return AttackStatistics.Empty;
        }

        var rows = weapons
            .Select(weapon => new WeaponStatistics(
                Name(weapon.Name),
                weapon.Attacks,
                weapon.Criticals,
                weapon.Damage,
                weapon.Best))
            .OrderByDescending(weapon => weapon.Attacks)
            .ThenBy(weapon => weapon.Name, StringComparer.CurrentCulture)
            .ToList();

        return new AttackStatistics(
            rows.Sum(weapon => weapon.Attacks),
            rows.Sum(weapon => weapon.Criticals),
            rows.Sum(weapon => weapon.Damage),
            rows.Max(weapon => weapon.Best),
            rows);
    }

    /// <summary>
    /// Считает использование заклинаний.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="query">Отбор событий.</param>
    /// <param name="start">Начало периода.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Заклинания от частых к редким.</returns>
    private static async Task<IReadOnlyList<SpellUsage>> BuildSpellsAsync(
        RpgDbContext context,
        StatisticsQuery query,
        DateTimeOffset? start,
        CancellationToken cancellationToken)
    {
        var spells = await Filter(context.History.AsNoTracking(), query, start)
            .Where(entry => entry.Action == HistoryActions.SpellCast)
            .GroupBy(entry => entry.Subject)
            .Select(group => new { Name = group.Key, Casts = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return spells
            .Select(spell => new SpellUsage(Name(spell.Name), spell.Casts))
            .OrderByDescending(spell => spell.Casts)
            .ThenBy(spell => spell.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>
    /// Считает использование ресурсов.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="query">Отбор событий.</param>
    /// <param name="start">Начало периода.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ресурсы от частых к редким.</returns>
    private static async Task<IReadOnlyList<ResourceUsage>> BuildResourcesAsync(
        RpgDbContext context,
        StatisticsQuery query,
        DateTimeOffset? start,
        CancellationToken cancellationToken)
    {
        var resources = await Filter(context.History.AsNoTracking(), query, start)
            .Where(entry => entry.Action == HistoryActions.ResourceChanged)
            .GroupBy(entry => entry.Subject)
            .Select(group => new
            {
                Name = group.Key,
                Changes = group.Count(),

                // Расход и восстановление разделены знаком изменения: журнал
                // хранит одно событие и на то, и на другое.
                Spent = group.Sum(entry => entry.Amount < 0 ? -(entry.Amount ?? 0) : 0),
                Restored = group.Sum(entry => entry.Amount > 0 ? entry.Amount ?? 0 : 0),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return resources
            .Select(resource => new ResourceUsage(
                Name(resource.Name),
                resource.Changes,
                resource.Spent,
                resource.Restored))
            .OrderByDescending(resource => resource.Changes)
            .ThenBy(resource => resource.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>
    /// Отбирает записи журнала событий по персонажу и периоду.
    /// </summary>
    /// <param name="query">Исходный запрос.</param>
    /// <param name="filter">Отбор событий.</param>
    /// <param name="start">Начало периода.</param>
    /// <returns>Отобранный запрос.</returns>
    private static IQueryable<HistoryEntry> Filter(
        IQueryable<HistoryEntry> query,
        StatisticsQuery filter,
        DateTimeOffset? start)
    {
        if (filter.CharacterId is { } characterId)
        {
            query = query.Where(entry => entry.CharacterId == characterId);
        }

        return start is { } from ? query.Where(entry => entry.CreatedAt >= from) : query;
    }

    /// <summary>
    /// Отбирает записи журнала бросков по персонажу и периоду.
    /// </summary>
    /// <param name="query">Исходный запрос.</param>
    /// <param name="filter">Отбор событий.</param>
    /// <param name="start">Начало периода.</param>
    /// <returns>Отобранный запрос.</returns>
    private static IQueryable<DiceRoll> Filter(
        IQueryable<DiceRoll> query,
        StatisticsQuery filter,
        DateTimeOffset? start)
    {
        if (filter.CharacterId is { } characterId)
        {
            query = query.Where(roll => roll.CharacterId == characterId);
        }

        return start is { } from ? query.Where(roll => roll.CreatedAt >= from) : query;
    }

    /// <summary>
    /// Возвращает название объекта события.
    /// </summary>
    /// <param name="subject">Название из записи журнала.</param>
    /// <returns>Название либо подпись для записей без него.</returns>
    private static string Name(string? subject) =>
        string.IsNullOrWhiteSpace(subject) ? UnnamedSubject : subject;

    /// <summary>
    /// Запись журнала бросков в объёме, нужном статистике.
    /// </summary>
    /// <param name="Result">Итог броска.</param>
    /// <param name="DetailsJson">Подробности броска с выпавшими костями.</param>
    /// <param name="HasAdvantage">Бросок выполнен с преимуществом.</param>
    /// <param name="HasDisadvantage">Бросок выполнен с помехой.</param>
    private sealed record RollRow(
        double Result,
        string? DetailsJson,
        bool HasAdvantage,
        bool HasDisadvantage);

    /// <summary>
    /// Накопитель статистики одного вида кости.
    /// </summary>
    private sealed class DieCounter
    {
        /// <summary>Сколько раз кость бросалась.</summary>
        public int Casts { get; private set; }

        /// <summary>Сумма выпавших значений.</summary>
        public double Total { get; private set; }

        /// <summary>Сколько раз выпала наибольшая грань.</summary>
        public int Maximums { get; private set; }

        /// <summary>Сколько раз выпала наименьшая грань.</summary>
        public int Minimums { get; private set; }

        /// <summary>
        /// Учитывает одну выпавшую кость.
        /// </summary>
        /// <param name="value">Выпавшее значение.</param>
        /// <param name="sides">Количество граней кости.</param>
        public void Add(int value, int sides)
        {
            Casts++;
            Total += value;

            if (value >= sides)
            {
                Maximums++;
            }

            if (value <= 1)
            {
                Minimums++;
            }
        }
    }
}
