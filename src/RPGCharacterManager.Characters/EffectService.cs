using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Characters;

/// <summary>
/// Эффекты персонажа: наложение, снятие и таймеры.
///
/// Служба не содержит правил ни одной игры и не различает болезнь, проклятие
/// и благословение: каждый эффект описан категорией, окраской и списком изменений,
/// составленным пользователем. Величины изменений вычисляет расчёт персонажа —
/// здесь они только читаются, чтобы показать их в панели эффектов.
/// </summary>
public sealed class EffectService : IEffectService
{
    /// <summary>Количество вариантов, загружаемых в список наложения за один раз.</summary>
    public const int AvailableEffectPageSize = 200;

    /// <summary>Источник наложения, если пользователь не указал свой.</summary>
    public const string ManualSource = "Наложено вручную";

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly ICharacterBuilderService _builder;
    private readonly IFormulaEngine _formulas;
    private readonly IEventBus _eventBus;
    private readonly ILogger<EffectService> _logger;

    /// <summary>
    /// Создаёт службу эффектов.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="builder">Мастер создания персонажа: расчёт и проверка требований.</param>
    /// <param name="formulas">Единый движок вычислений.</param>
    /// <param name="eventBus">Шина событий приложения.</param>
    /// <param name="logger">Журналировщик.</param>
    public EffectService(
        IDbContextFactory<RpgDbContext> contextFactory,
        ICharacterBuilderService builder,
        IFormulaEngine formulas,
        IEventBus eventBus,
        ILogger<EffectService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _builder = Guard.NotNull(builder);
        _formulas = Guard.NotNull(formulas);
        _eventBus = Guard.NotNull(eventBus);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<EffectState>> GetAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var character = await LoadCharacterAsync(context, characterId, tracked: false, cancellationToken)
            .ConfigureAwait(false);

        if (character is null)
        {
            return Result.Failure<EffectState>("Персонаж не найден: возможно, он был удалён.");
        }

        var draft = _builder.CreateDraft(character);
        var calculation = await _builder.CalculateAsync(draft, cancellationToken).ConfigureAwait(false);

        // Величины изменений уже вычислены расчётом персонажа: считать их здесь
        // заново означало бы завести второй вычислитель рядом с единым.
        var applied = calculation.Bonuses.ToDictionary(bonus => (bonus.SourceId, bonus.Id));

        var effects = character.Effects
            .Where(record => record.IsActive && record.Effect is not null)
            .OrderByDescending(record => record.Effect!.Priority)
            .ThenBy(record => record.Effect!.Name, StringComparer.CurrentCulture)
            .Select(record => BuildEffect(record, record.Effect!, applied))
            .ToList();

        var units = effects
            .Where(effect => effect.HasTimer && !string.IsNullOrWhiteSpace(effect.DurationUnit))
            .GroupBy(effect => effect.DurationUnit!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new EffectTimerUnit(group.Key, group.Count()))
            .OrderBy(unit => unit.Unit, StringComparer.CurrentCulture)
            .ToList();

        return Result.Success(new EffectState(effects, units));
    }

    /// <inheritdoc />
    public async Task<CharacterOptionPage> GetAvailableEffectsAsync(
        Guid characterId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var character = await context.Characters
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == characterId, cancellationToken)
            .ConfigureAwait(false);

        if (character is null)
        {
            return new CharacterOptionPage([], 0);
        }

        var systemId = character.GameSystemId;

        var query = context.Effects
            .AsNoTracking()
            .Include(effect => effect.Bonuses)
            .Where(effect => effect.GameSystemId == null || effect.GameSystemId == systemId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";

            query = query.Where(effect => EF.Functions.Like(effect.Name, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var effects = await query
            .OrderByDescending(effect => effect.Priority)
            .ThenBy(effect => effect.Name)
            .Take(AvailableEffectPageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var options = effects
            .Select(effect => new CharacterOption(
                effect.Id,
                effect.Name,
                effect.Description,
                true,
                null,
                BuildOptionDetails(effect),
                effect.Image))
            .ToList();

        return new CharacterOptionPage(options, totalCount);
    }

    /// <inheritdoc />
    public async Task<Result> ApplyAsync(
        Guid characterId,
        Guid effectId,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var character = await LoadCharacterAsync(context, characterId, tracked: true, cancellationToken)
                .ConfigureAwait(false);

            if (character is null)
            {
                return Result.Failure("Персонаж не найден: возможно, он был удалён.");
            }

            var effect = await context.Effects
                .FirstOrDefaultAsync(entity => entity.Id == effectId, cancellationToken)
                .ConfigureAwait(false);

            if (effect is null)
            {
                return Result.Failure("Эффект не найден: возможно, он был удалён.");
            }

            var formulaContext = await _builder
                .CreateContextAsync(_builder.CreateDraft(character), cancellationToken)
                .ConfigureAwait(false);

            var duration = EvaluateDuration(effect, formulaContext);
            var existing = character.Effects
                .FirstOrDefault(record => record.EffectId == effectId && record.IsActive);

            if (existing is not null)
            {
                var stacked = Stack(existing, effect, duration);

                if (stacked.IsFailure)
                {
                    return stacked;
                }
            }
            else
            {
                var created = new CharacterEffect
                {
                    CharacterId = character.Id,
                    EffectId = effect.Id,
                    RemainingTime = duration,
                    Stacks = 1,
                    Source = string.IsNullOrWhiteSpace(source) ? ManualSource : source.Trim(),
                    IsActive = true,
                };

                character.Effects.Add(created);

                // Запись создаётся с уже заданным идентификатором, поэтому передаётся
                // контексту явно: иначе она была бы принята за изменение (решение Р-28).
                context.Add(created);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            CharacterLog.EffectApplied(_logger, character.Name, effect.Name);

            await PublishChangedAsync(character.Id, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CharacterLog.EffectOperationFailed(_logger, exception, characterId);

            return Result.Failure($"Не удалось наложить эффект: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public Task<Result> RemoveAsync(
        Guid characterId,
        Guid characterEffectId,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(
            characterId,
            "Не удалось снять эффект",
            (context, character) =>
            {
                if (Find(character, characterEffectId) is not { } record)
                {
                    return Result.Failure("Эффект не найден: возможно, он уже снят.");
                }

                character.Effects.Remove(record);
                context.Remove(record);

                return Result.Success();
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<Result> RemoveStackAsync(
        Guid characterId,
        Guid characterEffectId,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(
            characterId,
            "Не удалось убрать наложение",
            (context, character) =>
            {
                if (Find(character, characterEffectId) is not { } record)
                {
                    return Result.Failure("Эффект не найден: возможно, он уже снят.");
                }

                if (record.Stacks <= 1)
                {
                    character.Effects.Remove(record);
                    context.Remove(record);

                    return Result.Success();
                }

                record.Stacks -= 1;

                return Result.Success();
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<Result<EffectAdvanceResult>> AdvanceAsync(
        Guid characterId,
        string unit,
        double amount,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return Result.Failure<EffectAdvanceResult>("Не указана единица длительности.");
        }

        if (amount <= 0)
        {
            return Result.Failure<EffectAdvanceResult>("Время продвигается только вперёд.");
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var character = await LoadCharacterAsync(context, characterId, tracked: true, cancellationToken)
                .ConfigureAwait(false);

            if (character is null)
            {
                return Result.Failure<EffectAdvanceResult>("Персонаж не найден: возможно, он был удалён.");
            }

            var expired = new List<string>();

            foreach (var record in character.Effects.ToList())
            {
                if (record.Effect is not { } effect || record.RemainingTime is not { } remaining)
                {
                    continue;
                }

                // Единица сравнивается порядково: это имя, введённое пользователем,
                // и оно должно совпасть с тем, по которому сгруппирована панель.
                if (!string.Equals(effect.DurationUnit?.Trim(), unit.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var left = remaining - amount;

                if (left > 0)
                {
                    record.RemainingTime = left;
                    continue;
                }

                expired.Add(effect.Name);

                character.Effects.Remove(record);
                context.Remove(record);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (expired.Count > 0)
            {
                CharacterLog.EffectsExpired(_logger, character.Name, expired.Count);
            }

            await PublishChangedAsync(character.Id, cancellationToken).ConfigureAwait(false);

            return Result.Success(new EffectAdvanceResult(unit.Trim(), amount, expired));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CharacterLog.EffectOperationFailed(_logger, exception, characterId);

            return Result.Failure<EffectAdvanceResult>($"Не удалось продвинуть время: {exception.Message}");
        }
    }

    /// <summary>
    /// Применяет правило повторного наложения к уже действующему эффекту.
    /// </summary>
    /// <param name="record">Действующее наложение.</param>
    /// <param name="effect">Накладываемый эффект.</param>
    /// <param name="duration">Вычисленная длительность.</param>
    /// <returns>Результат наложения.</returns>
    private static Result Stack(CharacterEffect record, Effect effect, double? duration)
    {
        switch (effect.Stacking)
        {
            case EffectStacking.Forbidden:
                return Result.Failure(
                    $"Эффект «{effect.Name}» уже наложен и не складывается сам с собой.");

            case EffectStacking.Sum:
                if (effect.MaximumStacks is { } maximum && record.Stacks >= maximum)
                {
                    return Result.Failure(
                        $"Эффект «{effect.Name}» уже наложен предельное число раз: {Format(maximum)}.");
                }

                record.Stacks += 1;

                // Новое наложение возобновляет отсчёт: иначе добавленный стак
                // исчез бы вместе с самым старым.
                record.RemainingTime = duration;

                return Result.Success();

            default:
                record.RemainingTime = duration;

                return Result.Success();
        }
    }

    /// <summary>
    /// Выполняет изменение эффектов персонажа: загружает его, применяет изменение,
    /// сохраняет и сообщает приложению о пересчёте.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="failureMessage">Начало сообщения об ошибке.</param>
    /// <param name="change">Изменение.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат изменения.</returns>
    private async Task<Result> ChangeAsync(
        Guid characterId,
        string failureMessage,
        Func<RpgDbContext, Character, Result> change,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var character = await LoadCharacterAsync(context, characterId, tracked: true, cancellationToken)
                .ConfigureAwait(false);

            if (character is null)
            {
                return Result.Failure("Персонаж не найден: возможно, он был удалён.");
            }

            var result = change(context, character);

            if (result.IsFailure)
            {
                return result;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await PublishChangedAsync(characterId, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CharacterLog.EffectOperationFailed(_logger, exception, characterId);

            return Result.Failure($"{failureMessage}: {exception.Message}");
        }
    }

    /// <summary>
    /// Собирает действующий эффект к показу.
    /// </summary>
    /// <param name="record">Наложение эффекта.</param>
    /// <param name="effect">Эффект.</param>
    /// <param name="applied">Вычисленные бонусы по наложению и описанию бонуса.</param>
    /// <returns>Эффект к показу.</returns>
    private static ActiveEffect BuildEffect(
        CharacterEffect record,
        Effect effect,
        IReadOnlyDictionary<(Guid SourceId, Guid BonusId), AppliedBonus> applied) =>
        new(
            record.Id,
            effect.Id,
            effect.Name,
            effect.Description,
            effect.Category,
            effect.Tone,
            effect.Color,
            effect.Area,
            effect.Priority,
            Math.Max(1, record.Stacks),
            effect.MaximumStacks,
            effect.Stacking,
            record.RemainingTime,
            effect.DurationUnit,
            effect.EndCondition,
            record.Source,
            record.CreatedAt,
            BuildChanges(effect, record.Id, applied));

    /// <summary>
    /// Сопоставляет изменения эффекта с их вычисленными величинами.
    /// Соответствие устанавливается по идентификатору наложения, поэтому два
    /// одинаковых эффекта из разных источников не путаются между собой.
    /// </summary>
    /// <param name="effect">Эффект с загруженными бонусами.</param>
    /// <param name="characterEffectId">Идентификатор наложения.</param>
    /// <param name="applied">Вычисленные бонусы.</param>
    /// <returns>Изменения для отображения.</returns>
    private static List<EffectChange> BuildChanges(
        Effect effect,
        Guid characterEffectId,
        IReadOnlyDictionary<(Guid SourceId, Guid BonusId), AppliedBonus> applied) =>
        effect.Bonuses
            .OrderBy(bonus => bonus.SortOrder)
            .Select(bonus =>
            {
                applied.TryGetValue((characterEffectId, bonus.Id), out var value);

                return new EffectChange(
                    value?.Description ?? bonus.Name ?? "изменение",
                    value?.Value ?? 0,
                    bonus.Formula,
                    bonus.Condition,
                    value?.IsApplied ?? false);
            })
            .ToList();

    /// <summary>
    /// Вычисляет длительность эффекта по его формуле.
    /// </summary>
    /// <param name="effect">Эффект.</param>
    /// <param name="formulaContext">Значения переменных персонажа.</param>
    /// <returns>Длительность в единицах эффекта либо <see langword="null"/>.</returns>
    private double? EvaluateDuration(Effect effect, IFormulaContext formulaContext)
    {
        if (string.IsNullOrWhiteSpace(effect.DurationFormula))
        {
            return null;
        }

        var result = _formulas.Evaluate(effect.DurationFormula, formulaContext);

        return result.IsSuccess ? Math.Max(0, result.Value.AsNumber()) : null;
    }

    private static List<CharacterOptionDetail> BuildOptionDetails(Effect effect)
    {
        var details = new List<CharacterOptionDetail>();

        if (!string.IsNullOrWhiteSpace(effect.Category))
        {
            details.Add(new CharacterOptionDetail("Категория", effect.Category));
        }

        if (!string.IsNullOrWhiteSpace(effect.DurationUnit))
        {
            details.Add(new CharacterOptionDetail("Длительность", effect.DurationUnit));
        }

        if (effect.Bonuses.Count > 0)
        {
            details.Add(new CharacterOptionDetail(
                "Изменений",
                effect.Bonuses.Count.ToString(CultureInfo.CurrentCulture)));
        }

        return details;
    }

    private static CharacterEffect? Find(Character character, Guid characterEffectId) =>
        character.Effects.FirstOrDefault(record => record.Id == characterEffectId);

    private static string Format(double value) =>
        value.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>
    /// Загружает персонажа вместе с эффектами и их бонусами.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="tracked">Изменения персонажа будут сохранены.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Персонаж либо <see langword="null"/>.</returns>
    private static Task<Character?> LoadCharacterAsync(
        RpgDbContext context,
        Guid characterId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = context.Characters
            .Include(character => character.Race)
            .Include(character => character.Class)
            .Include(character => character.Subclass)
            .Include(character => character.Background)
            .Include(character => character.Attributes)
            .Include(character => character.Skills)
            .Include(character => character.Traits)
            .Include(character => character.Spells)
            .Include(character => character.Resources)
            .Include(character => character.Effects)
                .ThenInclude(record => record.Effect)
                .ThenInclude(effect => effect!.Bonuses);

        return tracked
            ? query.FirstOrDefaultAsync(character => character.Id == characterId, cancellationToken)
            : query.AsNoTracking()
                .FirstOrDefaultAsync(character => character.Id == characterId, cancellationToken);
    }

    /// <summary>
    /// Сообщает приложению, что параметры персонажа изменились: наложенный эффект
    /// изменяет характеристики и ресурсы, поэтому лист должен перечитаться.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после публикации события.</returns>
    private Task PublishChangedAsync(Guid characterId, CancellationToken cancellationToken) =>
        _eventBus.PublishAsync(
            new CharacterChangedEvent(characterId, CharacterChangeKind.Recalculated),
            cancellationToken);
}
