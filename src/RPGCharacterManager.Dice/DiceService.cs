using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Dice;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Models.Dice;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Dice;

/// <summary>
/// Броски кубиков.
///
/// Служба не знает ни одной игровой механики: бросок — это выражение, вычисленное
/// единым движком формул. Поэтому «1d20 + Ловкость», «3d6 + Уровень» и бросок
/// пользовательского «Кристалла судьбы d777» выполняются одним и тем же путём,
/// а преимущество и помеха применимы к любому из них.
/// </summary>
public sealed class DiceService : IDiceService
{
    /// <summary>Количество попыток обычного броска.</summary>
    private const int SingleAttempt = 1;

    /// <summary>Количество попыток броска с преимуществом или помехой.</summary>
    private const int PairedAttempts = 2;

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly ICharacterBuilderService _builder;
    private readonly IFormulaEngine _formulas;
    private readonly IRandomSource _random;
    private readonly ISettingsService _settings;
    private readonly ILogger<DiceService> _logger;

    /// <summary>
    /// Создаёт службу бросков.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="builder">Мастер создания персонажа: источник значений переменных.</param>
    /// <param name="formulas">Единый движок вычислений.</param>
    /// <param name="random">Источник случайных значений.</param>
    /// <param name="settings">Служба настроек: предел размера журнала бросков.</param>
    /// <param name="logger">Журналировщик.</param>
    public DiceService(
        IDbContextFactory<RpgDbContext> contextFactory,
        ICharacterBuilderService builder,
        IFormulaEngine formulas,
        IRandomSource random,
        ISettingsService settings,
        ILogger<DiceService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _builder = Guard.NotNull(builder);
        _formulas = Guard.NotNull(formulas);
        _random = Guard.NotNull(random);
        _settings = Guard.NotNull(settings);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<DieDefinition>>> GetDiceAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var custom = await context.DieTypes
            .AsNoTracking()
            .OrderBy(die => die.SortOrder)
            .ThenBy(die => die.Sides)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dice = new List<DieDefinition>(StandardDice.Sides.Count + custom.Count);

        foreach (var sides in StandardDice.Sides)
        {
            dice.Add(new DieDefinition(null, DiceNotation.Die(sides), sides, null, null));
        }

        foreach (var die in custom)
        {
            dice.Add(new DieDefinition(die.Id, die.Name, die.Sides, die.Color, die.Description));
        }

        return Result.Success<IReadOnlyList<DieDefinition>>(dice);
    }

    /// <inheritdoc />
    public async Task<Result<RollOutcome>> RollAsync(
        RollRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        var expression = request.Expression?.Trim();

        if (string.IsNullOrEmpty(expression))
        {
            return Result.Failure<RollOutcome>("Выражение броска не задано.");
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var contextResult = await CreateFormulaContextAsync(context, request.CharacterId, cancellationToken)
                .ConfigureAwait(false);

            if (contextResult.IsFailure)
            {
                return Result.Failure<RollOutcome>(contextResult.Error!);
            }

            var attempts = new List<RollAttempt>(PairedAttempts);
            var count = request.Mode == RollMode.Normal ? SingleAttempt : PairedAttempts;

            for (var index = 0; index < count; index++)
            {
                // Каждая попытка получает собственный записывающий источник: иначе
                // кости второй попытки смешались бы с костями первой.
                var recorder = new RecordingRandomSource(_random);
                var evaluated = _formulas.Evaluate(expression, contextResult.Value, recorder);

                if (evaluated.IsFailure)
                {
                    DiceLog.RollFailed(_logger, expression, evaluated.Error!);
                    return Result.Failure<RollOutcome>(evaluated.Error!);
                }

                attempts.Add(new RollAttempt(evaluated.Value.AsNumber(), recorder.Casts, false));
            }

            var chosen = Mark(attempts, Choose(attempts, request.Mode));
            var record = CreateRecord(request, expression, chosen);

            context.Add(record);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await TrimHistoryAsync(context, cancellationToken).ConfigureAwait(false);

            DiceLog.RollPerformed(_logger, expression, Describe(request.Mode), record.Result);

            return Result.Success(ToOutcome(record, chosen));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            DiceLog.DiceOperationFailed(_logger, exception);
            return Result.Failure<RollOutcome>("Не удалось выполнить бросок. Подробности записаны в журнал.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<RollOutcome>>> GetHistoryAsync(
        Guid? characterId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return Result.Success<IReadOnlyList<RollOutcome>>([]);
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var records = await Filter(context.DiceHistory.AsNoTracking(), characterId)
            .OrderByDescending(record => record.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<RollOutcome>>(records.Select(Restore).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<RollOutcome>>> GetFavoritesAsync(
        Guid? characterId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var records = await Filter(context.DiceHistory.AsNoTracking(), characterId)
            .Where(record => record.IsFavorite)
            .OrderBy(record => record.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<RollOutcome>>(records.Select(Restore).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<RollOutcome>> SetFavoriteAsync(
        Guid rollId,
        bool isFavorite,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var record = await context.DiceHistory
            .FirstOrDefaultAsync(entity => entity.Id == rollId, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return Result.Failure<RollOutcome>("Бросок не найден: возможно, запись уже удалена.");
        }

        record.IsFavorite = isFavorite;
        record.ModifiedAt = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(title))
        {
            record.Label = title.Trim();
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(Restore(record));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid rollId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var removed = await context.DiceHistory
            .Where(record => record.Id == rollId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return removed > 0
            ? Result.Success()
            : Result.Failure("Бросок не найден: возможно, запись уже удалена.");
    }

    /// <inheritdoc />
    public async Task<Result<int>> ClearHistoryAsync(
        Guid? characterId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Любимые броски переживают очистку: пользователь сохранил их именно затем,
        // чтобы не собирать выражение заново.
        var removed = await Filter(context.DiceHistory, characterId)
            .Where(record => !record.IsFavorite)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        DiceLog.HistoryCleared(_logger, removed);

        return Result.Success(removed);
    }

    /// <summary>
    /// Отбирает записи журнала, относящиеся к персонажу.
    /// </summary>
    /// <param name="query">Исходный запрос.</param>
    /// <param name="characterId">Персонаж; <see langword="null"/> — все записи.</param>
    /// <returns>Отобранный запрос.</returns>
    private static IQueryable<DiceRoll> Filter(IQueryable<DiceRoll> query, Guid? characterId) =>
        characterId is { } id ? query.Where(record => record.CharacterId == id) : query;

    /// <summary>
    /// Выбирает попытку, итог которой становится результатом броска.
    /// </summary>
    /// <param name="attempts">Выполненные попытки.</param>
    /// <param name="mode">Способ выполнения броска.</param>
    /// <returns>Номер выбранной попытки.</returns>
    private static int Choose(IReadOnlyList<RollAttempt> attempts, RollMode mode)
    {
        var chosen = 0;

        for (var index = 1; index < attempts.Count; index++)
        {
            var better = mode == RollMode.Disadvantage
                ? attempts[index].Total < attempts[chosen].Total
                : attempts[index].Total > attempts[chosen].Total;

            if (better)
            {
                chosen = index;
            }
        }

        return chosen;
    }

    /// <summary>
    /// Создаёт источник значений переменных персонажа.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="characterId">Персонаж или <see langword="null"/>.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Источник значений либо описание ошибки.</returns>
    private async Task<Result<IFormulaContext?>> CreateFormulaContextAsync(
        RpgDbContext context,
        Guid? characterId,
        CancellationToken cancellationToken)
    {
        if (characterId is not { } id)
        {
            return Result.Success<IFormulaContext?>(null);
        }

        var character = await context.Characters
            .AsNoTracking()
            .Include(entity => entity.GameSystem)
            .Include(entity => entity.Race)
            .Include(entity => entity.Class)
            .Include(entity => entity.Subclass)
            .Include(entity => entity.Background)
            .Include(entity => entity.Attributes)
            .Include(entity => entity.Skills)
            .Include(entity => entity.Traits)
            .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (character is null)
        {
            return Result.Failure<IFormulaContext?>("Персонаж не найден: возможно, он был удалён.");
        }

        var draft = _builder.CreateDraft(character);
        var formulaContext = await _builder.CreateContextAsync(draft, cancellationToken).ConfigureAwait(false);

        return Result.Success<IFormulaContext?>(formulaContext);
    }

    /// <summary>
    /// Создаёт запись журнала бросков.
    /// </summary>
    /// <param name="request">Запрос броска.</param>
    /// <param name="expression">Выражение броска.</param>
    /// <param name="attempts">Выполненные попытки с отмеченной выбранной.</param>
    /// <returns>Запись журнала.</returns>
    private static DiceRoll CreateRecord(
        RollRequest request,
        string expression,
        IReadOnlyList<RollAttempt> attempts) =>
        new()
        {
            CharacterId = request.CharacterId,
            Label = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            Formula = expression,
            Result = attempts.First(attempt => attempt.IsChosen).Total,
            HasAdvantage = request.Mode == RollMode.Advantage,
            HasDisadvantage = request.Mode == RollMode.Disadvantage,
            DetailsJson = RollDetails.Write(attempts),
        };

    /// <summary>
    /// Отмечает выбранную попытку.
    /// </summary>
    /// <param name="attempts">Выполненные попытки.</param>
    /// <param name="chosen">Номер выбранной попытки.</param>
    /// <returns>Попытки с отметкой выбора.</returns>
    private static List<RollAttempt> Mark(IReadOnlyList<RollAttempt> attempts, int chosen) =>
        attempts
            .Select((attempt, index) => attempt with { IsChosen = index == chosen })
            .ToList();

    /// <summary>
    /// Собирает результат броска по записи журнала и выполненным попыткам.
    /// </summary>
    /// <param name="record">Запись журнала.</param>
    /// <param name="attempts">Выполненные попытки с отмеченной выбранной.</param>
    /// <returns>Результат броска.</returns>
    private static RollOutcome ToOutcome(DiceRoll record, IReadOnlyList<RollAttempt> attempts) =>
        new(
            record.Id,
            record.Label,
            record.Formula,
            ReadMode(record),
            record.Result,
            attempts,
            record.CreatedAt,
            record.IsFavorite,
            record.CharacterId);

    /// <summary>
    /// Восстанавливает результат броска из записи журнала.
    /// </summary>
    /// <param name="record">Запись журнала.</param>
    /// <returns>Результат броска.</returns>
    private static RollOutcome Restore(DiceRoll record) =>
        new(
            record.Id,
            record.Label,
            record.Formula,
            ReadMode(record),
            record.Result,
            RollDetails.Read(record.DetailsJson, record.Result),
            record.CreatedAt,
            record.IsFavorite,
            record.CharacterId);

    private static RollMode ReadMode(DiceRoll record) => record switch
    {
        { HasAdvantage: true } => RollMode.Advantage,
        { HasDisadvantage: true } => RollMode.Disadvantage,
        _ => RollMode.Normal,
    };

    private static string Describe(RollMode mode) => mode switch
    {
        RollMode.Advantage => "преимущество",
        RollMode.Disadvantage => "помеха",
        _ => "обычный",
    };

    /// <summary>
    /// Удаляет из журнала записи, вышедшие за установленный предел.
    ///
    /// Журнал рассчитан на тысячи бросков, но неограниченный рост никому не нужен:
    /// предел задаётся в настройках, а любимые броски не вытесняются никогда.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после очистки.</returns>
    private async Task TrimHistoryAsync(RpgDbContext context, CancellationToken cancellationToken)
    {
        var limit = _settings.Current.DiceHistoryLimit;

        if (limit <= 0)
        {
            return;
        }

        var stale = await context.DiceHistory
            .Where(record => !record.IsFavorite)
            .OrderByDescending(record => record.CreatedAt)
            .Skip(limit)
            .Select(record => record.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (stale.Count == 0)
        {
            return;
        }

        await context.DiceHistory
            .Where(record => stale.Contains(record.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
