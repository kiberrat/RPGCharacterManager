using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Dice;
using RPGCharacterManager.Core.Abstractions.History;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.History;

/// <summary>
/// Журнал событий: чтение и очистка.
///
/// События приходят из двух хранилищ. Действия — расход ресурса, применение
/// заклинания, смена экипировки — лежат в журнале действий; броски лежат в журнале
/// бросков, где у них есть выражение и выпавшие кости. Служба сводит их в один
/// поток по времени, а не копирует броски в журнал действий: копия рано или поздно
/// разошлась бы с исходной записью.
/// </summary>
public sealed class HistoryService : IHistoryService
{
    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly IDiceService _dice;
    private readonly ILogger<HistoryService> _logger;

    /// <summary>
    /// Создаёт службу журнала событий.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="dice">Подсистема бросков: ей принадлежит правило сохранения любимых бросков.</param>
    /// <param name="logger">Журналировщик.</param>
    public HistoryService(
        IDbContextFactory<RpgDbContext> contextFactory,
        IDiceService dice,
        ILogger<HistoryService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _dice = Guard.NotNull(dice);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<HistoryPage>> GetAsync(
        HistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(query);

        if (query.Limit <= 0)
        {
            return Result.Success(new HistoryPage([], 0));
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var search = string.IsNullOrWhiteSpace(query.Search) ? null : $"%{query.Search.Trim()}%";

            var actions = FilterActions(context.History.AsNoTracking(), query, search);
            var total = await actions.CountAsync(cancellationToken).ConfigureAwait(false);

            var records = new List<HistoryRecord>(query.Limit * 2);

            foreach (var entry in await actions
                .OrderByDescending(entry => entry.CreatedAt)
                .Take(query.Limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                records.Add(ToRecord(entry));
            }

            if (IncludesRolls(query.Kind))
            {
                var rolls = FilterRolls(context.DiceHistory.AsNoTracking(), query, search);
                total += await rolls.CountAsync(cancellationToken).ConfigureAwait(false);

                foreach (var roll in await rolls
                    .OrderByDescending(roll => roll.CreatedAt)
                    .Take(query.Limit)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                {
                    records.Add(ToRecord(roll));
                }
            }

            // Из каждого хранилища взято не больше запрошенного, поэтому после
            // слияния лишнее отбрасывается: первыми идут самые свежие события.
            var page = records
                .OrderByDescending(record => record.Timestamp)
                .Take(query.Limit)
                .ToList();

            return Result.Success(new HistoryPage(
                await NameCharactersAsync(context, page, cancellationToken).ConfigureAwait(false),
                total));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HistoryLog.JournalOperationFailed(_logger, exception);

            return Result.Failure<HistoryPage>("Не удалось прочитать журнал. Подробности записаны в журнал.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<HistoryCharacter>>> GetCharactersAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var fromActions = await context.History
            .AsNoTracking()
            .Where(entry => entry.CharacterId != null)
            .Select(entry => entry.CharacterId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var fromRolls = await context.DiceHistory
            .AsNoTracking()
            .Where(roll => roll.CharacterId != null)
            .Select(roll => roll.CharacterId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var identifiers = fromActions.Union(fromRolls).ToList();

        var characters = await context.Characters
            .AsNoTracking()
            .Where(character => identifiers.Contains(character.Id))
            .Select(character => new HistoryCharacter(character.Id, character.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<HistoryCharacter>>(
            characters.OrderBy(character => character.Name, StringComparer.CurrentCulture).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<int>> ClearAsync(
        Guid? characterId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var removed = await Filter(context.History, characterId is { } id ? [id] : null)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // Броски убирает их собственная служба: правило «любимые остаются»
            // принадлежит ей, и повторять его здесь значило бы завести второе.
            var rolls = await _dice.ClearHistoryAsync(characterId, cancellationToken).ConfigureAwait(false);

            if (rolls.IsFailure)
            {
                return Result.Failure<int>(rolls.Error!);
            }

            removed += rolls.Value;

            HistoryLog.JournalCleared(_logger, removed);

            return Result.Success(removed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HistoryLog.JournalOperationFailed(_logger, exception);

            return Result.Failure<int>("Не удалось очистить журнал. Подробности записаны в журнал.");
        }
    }

    /// <summary>
    /// Проверяет, входят ли броски в выбранный вид событий.
    /// </summary>
    /// <param name="kind">Вид событий.</param>
    /// <returns><see langword="true"/>, если броски нужно читать.</returns>
    private static bool IncludesRolls(HistoryKind kind) =>
        kind is HistoryKind.Any or HistoryKind.Roll;

    /// <summary>
    /// Отбирает записи журнала действий.
    /// </summary>
    /// <param name="query">Исходный запрос.</param>
    /// <param name="filter">Отбор записей.</param>
    /// <param name="search">Строка поиска.</param>
    /// <returns>Отобранный запрос.</returns>
    private static IQueryable<HistoryEntry> FilterActions(
        IQueryable<HistoryEntry> query,
        HistoryQuery filter,
        string? search)
    {
        query = Filter(query, filter.Characters);

        var codes = HistoryActions.CodesOf(filter.Kind);

        if (codes.Count > 0)
        {
            query = query.Where(entry => codes.Contains(entry.Action));
        }

        // EF.Functions.Like переносится в SQL, в отличие от сравнения строк
        // в памяти: журнал рассчитан на десятки тысяч записей.
        if (search is not null)
        {
            query = query.Where(entry =>
                (entry.Description != null && EF.Functions.Like(entry.Description, search))
                || EF.Functions.Like(entry.Action, search));
        }

        return query;
    }

    /// <summary>
    /// Отбирает записи журнала бросков.
    /// </summary>
    /// <param name="query">Исходный запрос.</param>
    /// <param name="filter">Отбор записей.</param>
    /// <param name="search">Строка поиска.</param>
    /// <returns>Отобранный запрос.</returns>
    private static IQueryable<DiceRoll> FilterRolls(
        IQueryable<DiceRoll> query,
        HistoryQuery filter,
        string? search)
    {
        if (Selected(filter.Characters) is { } characters)
        {
            query = query.Where(roll =>
                roll.CharacterId != null && characters.Contains(roll.CharacterId.Value));
        }

        if (search is not null)
        {
            query = query.Where(roll =>
                (roll.Label != null && EF.Functions.Like(roll.Label, search))
                || EF.Functions.Like(roll.Formula, search));
        }

        return query;
    }

    /// <summary>
    /// Отбирает записи журнала действий по персонажам.
    /// </summary>
    /// <param name="query">Исходный запрос.</param>
    /// <param name="characters">Персонажи; пустой список — все записи.</param>
    /// <returns>Отобранный запрос.</returns>
    private static IQueryable<HistoryEntry> Filter(
        IQueryable<HistoryEntry> query,
        IReadOnlyCollection<Guid>? characters) =>
        Selected(characters) is { } selected
            ? query.Where(entry => entry.CharacterId != null && selected.Contains(entry.CharacterId.Value))
            : query;

    /// <summary>
    /// Приводит отбор по персонажам к списку, пригодному для переноса в SQL.
    /// </summary>
    /// <param name="characters">Персонажи отбора.</param>
    /// <returns>Список персонажей либо <see langword="null"/>, если отбора нет.</returns>
    private static List<Guid>? Selected(IReadOnlyCollection<Guid>? characters) =>
        characters is { Count: > 0 } ? [.. characters] : null;

    /// <summary>
    /// Подставляет имена персонажей в записи журнала.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="records">Записи журнала.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Записи с именами персонажей.</returns>
    private static async Task<IReadOnlyList<HistoryRecord>> NameCharactersAsync(
        RpgDbContext context,
        List<HistoryRecord> records,
        CancellationToken cancellationToken)
    {
        var identifiers = records
            .Where(record => record.CharacterId.HasValue)
            .Select(record => record.CharacterId!.Value)
            .Distinct()
            .ToList();

        if (identifiers.Count == 0)
        {
            return records;
        }

        var names = await context.Characters
            .AsNoTracking()
            .Where(character => identifiers.Contains(character.Id))
            .Select(character => new { character.Id, character.Name })
            .ToDictionaryAsync(character => character.Id, character => character.Name, cancellationToken)
            .ConfigureAwait(false);

        for (var index = 0; index < records.Count; index++)
        {
            if (records[index].CharacterId is { } id && names.TryGetValue(id, out var name))
            {
                records[index] = records[index] with { CharacterName = name };
            }
        }

        return records;
    }

    /// <summary>
    /// Преобразует запись журнала действий.
    /// </summary>
    /// <param name="entry">Запись журнала действий.</param>
    /// <returns>Запись журнала событий.</returns>
    private static HistoryRecord ToRecord(HistoryEntry entry) => new(
        entry.Id,
        entry.CreatedAt,
        entry.Action,
        HistoryActions.KindOf(entry.Action),
        HistoryActions.Name(entry.Action),
        entry.Description,
        entry.OldValue,
        entry.NewValue,
        entry.CharacterId,
        null);

    /// <summary>
    /// Преобразует запись журнала бросков.
    ///
    /// Выпавшие кости в журнале событий не разбираются: их состав нужен панели
    /// бросков, а событию достаточно выражения и итога.
    /// </summary>
    /// <param name="roll">Запись журнала бросков.</param>
    /// <returns>Запись журнала событий.</returns>
    private static HistoryRecord ToRecord(DiceRoll roll)
    {
        var total = roll.Result.ToString("0.##", CultureInfo.CurrentCulture);

        var description = string.IsNullOrWhiteSpace(roll.Label)
            ? $"{roll.Formula} → {total}"
            : $"{roll.Label}: {roll.Formula} → {total}";

        return new HistoryRecord(
            roll.Id,
            roll.CreatedAt,
            HistoryActions.Roll,
            HistoryKind.Roll,
            HistoryActions.Name(HistoryActions.Roll),
            description,
            null,
            total,
            roll.CharacterId,
            null);
    }
}
