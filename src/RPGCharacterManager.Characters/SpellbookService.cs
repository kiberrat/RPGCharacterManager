using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.Engine;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Characters;

/// <summary>
/// Книга заклинаний персонажа: изучение, подготовка и применение.
///
/// Служба не содержит правил ни одной игры. Что такое уровень, школа и компоненты,
/// решает пользователь; пределы известных и подготовленных заклинаний заданы
/// формулами игровой системы; стоимость, результат и усиление считает единый
/// движок вычислений.
/// </summary>
public sealed class SpellbookService : ISpellbookService
{
    /// <summary>Количество вариантов, загружаемых в список изучения за один раз.</summary>
    public const int AvailableSpellPageSize = 200;

    /// <summary>
    /// Код действия, под которым применение записывается в журнал изменений.
    /// Совпадает с общим перечнем событий журнала, поэтому раздел журнала
    /// отбирает применения заклинаний, ничего не зная об этой службе.
    /// </summary>
    public const string CastHistoryAction = HistoryActions.SpellCast;

    /// <summary>Количество записей истории, показываемых в книге заклинаний.</summary>
    public const int HistoryPageSize = 10;

    /// <summary>Имя переменной с уровнем, на котором применяется заклинание.</summary>
    public const string CastLevelVariable = "уровень_применения";

    /// <summary>Имя переменной с базовым уровнем заклинания.</summary>
    public const string SpellLevelVariable = "уровень_заклинания";

    /// <summary>Имя переменной с количеством уровней сверх базового.</summary>
    public const string ExtraLevelsVariable = "уровни_сверх";

    /// <summary>Имя переменной с результатом базовой формулы внутри формулы усиления.</summary>
    public const string BaseResultVariable = "результат";

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly ICharacterBuilderService _builder;
    private readonly IFormulaEngine _formulas;
    private readonly IEventBus _eventBus;
    private readonly ILogger<SpellbookService> _logger;

    /// <summary>
    /// Создаёт службу книги заклинаний.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="builder">Мастер создания персонажа: расчёт и проверка требований.</param>
    /// <param name="formulas">Единый движок вычислений.</param>
    /// <param name="eventBus">Шина событий приложения.</param>
    /// <param name="logger">Журналировщик.</param>
    public SpellbookService(
        IDbContextFactory<RpgDbContext> contextFactory,
        ICharacterBuilderService builder,
        IFormulaEngine formulas,
        IEventBus eventBus,
        ILogger<SpellbookService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _builder = Guard.NotNull(builder);
        _formulas = Guard.NotNull(formulas);
        _eventBus = Guard.NotNull(eventBus);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<SpellbookState>> GetAsync(
        Guid characterId,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var character = await LoadCharacterAsync(context, characterId, tracked: false, cancellationToken)
            .ConfigureAwait(false);

        if (character is null)
        {
            return Result.Failure<SpellbookState>("Персонаж не найден: возможно, он был удалён.");
        }

        var formulaContext = await CreateContextAsync(character, cancellationToken).ConfigureAwait(false);
        var usesPreparation = !string.IsNullOrWhiteSpace(character.GameSystem?.PreparedSpellsFormula);
        var aliasTargets = await context.FindAliasTargetsAsync(
            ContentTypeIds.Spells, character.GameSystemId, search, cancellationToken).ConfigureAwait(false);


        var entries = character.Spells
            .Where(record => record.Spell is not null)
            .Where(record => Matches(record.Spell!, search, aliasTargets))
            .Select(record => BuildEntry(record, record.Spell!, usesPreparation, formulaContext))
            .ToList();

        var levels = entries
            .GroupBy(entry => entry.Level)
            .OrderBy(group => group.Key)
            .Select(group => new SpellbookLevel(
                group.Key,
                LevelTitle(group.Key),
                group.OrderBy(entry => entry.Name, StringComparer.CurrentCulture).ToList()))
            .ToList();

        var history = await LoadHistoryAsync(context, characterId, cancellationToken).ConfigureAwait(false);

        var state = new SpellbookState(
            levels,
            new SpellbookLimit(
                character.Spells.Count,
                EvaluateLimit(character.GameSystem?.KnownSpellsFormula, formulaContext)),
            new SpellbookLimit(
                character.Spells.Count(record => record.IsPrepared),
                EvaluateLimit(character.GameSystem?.PreparedSpellsFormula, formulaContext)),
            usesPreparation,
            character.Spells.FirstOrDefault(record => record.IsConcentrating)?.Spell?.Name,
            history);

        return Result.Success(state);
    }

    /// <inheritdoc />
    public async Task<CharacterOptionPage> GetAvailableSpellsAsync(
        Guid characterId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var character = await LoadCharacterAsync(context, characterId, tracked: false, cancellationToken)
            .ConfigureAwait(false);

        if (character is null)
        {
            return new CharacterOptionPage([], 0);
        }

        var known = character.Spells.Select(record => record.SpellId).ToHashSet();
        var systemId = character.GameSystemId;

        var query = context.Spells
            .AsNoTracking()
            .Where(spell => spell.GameSystemId == null || spell.GameSystemId == systemId)
            .Where(spell => !known.Contains(spell.Id));

        query = query.WhereNameOrAlias(context, ContentTypeIds.Spells, search);
        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var spells = await query
            .OrderBy(spell => spell.Level)
            .ThenBy(spell => spell.Name)
            .Take(AvailableSpellPageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var formulaContext = await CreateContextAsync(character, cancellationToken).ConfigureAwait(false);

        var options = spells
            .Select(spell =>
            {
                var reason = _builder.CheckRequirement(spell.Requirements, formulaContext);

                return new CharacterOption(
                    spell.Id,
                    spell.Name,
                    spell.Description,
                    reason is null,
                    reason,
                    BuildOptionDetails(spell),
                    spell.Image);
            })
            .ToList();

        return new CharacterOptionPage(options, totalCount);
    }

    /// <inheritdoc />
    public async Task<Result> LearnAsync(
        Guid characterId,
        Guid spellId,
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

            if (character.Spells.Any(record => record.SpellId == spellId))
            {
                return Result.Failure("Это заклинание уже есть в книге персонажа.");
            }

            var spell = await context.Spells
                .FirstOrDefaultAsync(entity => entity.Id == spellId, cancellationToken)
                .ConfigureAwait(false);

            if (spell is null)
            {
                return Result.Failure("Заклинание не найдено: возможно, оно было удалено.");
            }

            var formulaContext = await CreateContextAsync(character, cancellationToken).ConfigureAwait(false);

            if (_builder.CheckRequirement(spell.Requirements, formulaContext) is { } reason)
            {
                return Result.Failure($"Персонаж не может выучить «{spell.Name}». {reason}");
            }

            var limit = EvaluateLimit(character.GameSystem?.KnownSpellsFormula, formulaContext);

            if (limit is { } known && character.Spells.Count >= known)
            {
                return Result.Failure(
                    $"Предел известных заклинаний исчерпан: {Format(known)}. "
                    + "Сначала забудьте одно из выученных.");
            }

            var record = new CharacterSpell
            {
                CharacterId = character.Id,
                SpellId = spell.Id,
                Source = "Изучение",
            };

            character.Spells.Add(record);

            // Запись создаётся с уже заданным идентификатором, поэтому передаётся
            // контексту явно: иначе она была бы принята за изменение (решение Р-28).
            context.Add(record);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            CharacterLog.SpellLearned(_logger, character.Name, spell.Name);

            await PublishChangedAsync(character.Id, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CharacterLog.SpellbookOperationFailed(_logger, exception, characterId);

            return Result.Failure($"Не удалось выучить заклинание: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> ForgetAsync(
        Guid characterId,
        Guid characterSpellId,
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

            if (Find(character, characterSpellId) is not { } record)
            {
                return Result.Failure("Заклинание не найдено в книге: возможно, оно уже забыто.");
            }

            character.Spells.Remove(record);
            context.Remove(record);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await PublishChangedAsync(character.Id, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CharacterLog.SpellbookOperationFailed(_logger, exception, characterId);

            return Result.Failure($"Не удалось забыть заклинание: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> SetPreparedAsync(
        Guid characterId,
        Guid characterSpellId,
        bool prepared,
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

            if (Find(character, characterSpellId) is not { } record)
            {
                return Result.Failure("Заклинание не найдено в книге: возможно, оно было забыто.");
            }

            if (record.IsPrepared == prepared)
            {
                return Result.Success();
            }

            if (prepared)
            {
                var formulaContext = await CreateContextAsync(character, cancellationToken)
                    .ConfigureAwait(false);

                var limit = EvaluateLimit(character.GameSystem?.PreparedSpellsFormula, formulaContext);
                var count = character.Spells.Count(entry => entry.IsPrepared);

                if (limit is { } allowed && count >= allowed)
                {
                    return Result.Failure(
                        $"Предел подготовленных заклинаний исчерпан: {Format(allowed)}. "
                        + "Сначала снимите подготовку с другого заклинания.");
                }
            }

            record.IsPrepared = prepared;

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await PublishChangedAsync(character.Id, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CharacterLog.SpellbookOperationFailed(_logger, exception, characterId);

            return Result.Failure($"Не удалось изменить подготовку: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<SpellCastResult>> CastAsync(
        Guid characterId,
        Guid characterSpellId,
        int? castLevel = null,
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
                return Result.Failure<SpellCastResult>("Персонаж не найден: возможно, он был удалён.");
            }

            if (Find(character, characterSpellId) is not { Spell: { } spell } record)
            {
                return Result.Failure<SpellCastResult>(
                    "Заклинание не найдено в книге: возможно, оно было забыто.");
            }

            var level = castLevel ?? spell.Level;

            if (level < spell.Level)
            {
                return Result.Failure<SpellCastResult>(
                    $"«{spell.Name}» нельзя применить ниже его уровня: {Format(spell.Level)}.");
            }

            var formulaContext = await CreateContextAsync(character, cancellationToken).ConfigureAwait(false);
            var usesPreparation = !string.IsNullOrWhiteSpace(character.GameSystem?.PreparedSpellsFormula);

            if (Blocked(spell, record, usesPreparation, formulaContext) is { } reason)
            {
                return Result.Failure<SpellCastResult>(reason);
            }

            var castContext = CreateCastContext(formulaContext, spell, level);
            var issues = new List<string>();

            // Ресурс списывается до вычисления результата: заклинание, на которое
            // не хватает ресурса, не должно давать результат даже с замечаниями.
            var spending = SpendResource(character, spell, castContext, issues);

            if (spending.IsFailure)
            {
                return Result.Failure<SpellCastResult>(spending.Error!);
            }

            var result = EvaluateResult(spell, castContext, level, issues);
            var brokeConcentration = SwitchConcentration(character, record, spell);

            record.TimesUsed += 1;

            context.Add(CreateHistoryEntry(character, spell, level, result));

            // Расход ресурса записывается отдельным событием: в журнале его ищут
            // среди изменений ресурсов наравне с лечением зельем и правкой на листе.
            if (spending.Value is { ResourceName: { } spentResource, Spent: > 0, Remaining: { } left })
            {
                context.Add(HistoryEntries.ResourceChanged(
                    character.Id,
                    spentResource,
                    left + spending.Value.Spent,
                    left,
                    spell.Name));
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            CharacterLog.SpellCast(_logger, character.Name, spell.Name, level);

            await PublishChangedAsync(character.Id, cancellationToken).ConfigureAwait(false);

            return Result.Success(new SpellCastResult(
                spell.Name,
                level,
                result,
                spending.Value.ResourceName,
                spending.Value.Spent,
                spending.Value.Remaining,
                brokeConcentration,
                record.IsConcentrating,
                issues));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CharacterLog.SpellbookOperationFailed(_logger, exception, characterId);

            return Result.Failure<SpellCastResult>($"Не удалось применить заклинание: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> StopConcentrationAsync(
        Guid characterId,
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

            var active = character.Spells.Where(record => record.IsConcentrating).ToList();

            if (active.Count == 0)
            {
                return Result.Failure("Персонаж ни на чём не концентрируется.");
            }

            foreach (var record in active)
            {
                record.IsConcentrating = false;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await PublishChangedAsync(character.Id, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CharacterLog.SpellbookOperationFailed(_logger, exception, characterId);

            return Result.Failure($"Не удалось прервать концентрацию: {exception.Message}");
        }
    }

    /// <summary>
    /// Итог списания ресурса заклинания.
    /// </summary>
    /// <param name="ResourceName">Название ресурса либо <see langword="null"/>.</param>
    /// <param name="Spent">Списанное количество.</param>
    /// <param name="Remaining">Остаток ресурса.</param>
    private sealed record ResourceSpending(string? ResourceName, double Spent, double? Remaining);

    /// <summary>
    /// Списывает ресурс заклинания.
    ///
    /// Стоимость задаётся формулой; пустая формула при выбранном ресурсе означает
    /// стоимость 1. Заклинание без ресурса — кантрип или способность без затрат —
    /// не списывает ничего.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="spell">Применяемое заклинание.</param>
    /// <param name="castContext">Значения переменных с уровнем применения.</param>
    /// <param name="issues">Замечания вычисления.</param>
    /// <returns>Итог списания либо описание ошибки.</returns>
    private Result<ResourceSpending> SpendResource(
        Character character,
        Spell spell,
        IFormulaContext castContext,
        List<string> issues)
    {
        if (spell.ResourceId is not { } resourceId)
        {
            return Result.Success(new ResourceSpending(null, 0, null));
        }

        var resource = character.Resources.FirstOrDefault(entry => entry.ResourceId == resourceId);
        var resourceName = spell.Resource?.Name ?? "ресурс";

        if (resource is null)
        {
            return Result.Failure<ResourceSpending>(
                $"У персонажа нет ресурса «{resourceName}», который расходует «{spell.Name}».");
        }

        var cost = string.IsNullOrWhiteSpace(spell.ResourceCostFormula)
            ? 1
            : EvaluateNumber(spell.ResourceCostFormula, castContext, "стоимость", issues);

        if (cost < 0)
        {
            cost = 0;
        }

        if (resource.Current < cost)
        {
            return Result.Failure<ResourceSpending>(
                $"Не хватает ресурса «{resourceName}»: нужно {Format(cost)}, "
                + $"осталось {Format(resource.Current)}.");
        }

        resource.Current -= cost;

        return Result.Success(new ResourceSpending(resourceName, cost, resource.Current));
    }

    /// <summary>
    /// Вычисляет результат заклинания с учётом усиления.
    ///
    /// Формула результата видит уровень применения. Формула усиления выполняется
    /// только при применении выше базового уровня и получает базовый результат
    /// в переменной «результат» — по образцу формулы критического урона оружия.
    /// </summary>
    /// <param name="spell">Применяемое заклинание.</param>
    /// <param name="castContext">Значения переменных с уровнем применения.</param>
    /// <param name="castLevel">Уровень применения.</param>
    /// <param name="issues">Замечания вычисления.</param>
    /// <returns>Итоговый результат либо <see langword="null"/>, если формулы нет.</returns>
    private double? EvaluateResult(
        Spell spell,
        IFormulaContext castContext,
        int castLevel,
        List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(spell.Formula))
        {
            return null;
        }

        var result = EvaluateNumber(spell.Formula, castContext, "результат", issues);

        if (castLevel <= spell.Level || string.IsNullOrWhiteSpace(spell.ScalingFormula))
        {
            return result;
        }

        var scalingContext = new LocalFormulaContext(castContext).With(BaseResultVariable, result);

        return EvaluateNumber(spell.ScalingFormula, scalingContext, "усиление", issues);
    }

    /// <summary>
    /// Переключает концентрацию на применённое заклинание.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="record">Запись применённого заклинания.</param>
    /// <param name="spell">Применённое заклинание.</param>
    /// <returns>Название прерванного заклинания либо <see langword="null"/>.</returns>
    private static string? SwitchConcentration(Character character, CharacterSpell record, Spell spell)
    {
        if (!spell.RequiresConcentration)
        {
            return null;
        }

        string? broken = null;

        // Концентрация возможна только на одном заклинании: предыдущая прерывается.
        foreach (var other in character.Spells.Where(entry => entry.IsConcentrating))
        {
            if (other.Id != record.Id)
            {
                broken = other.Spell?.Name;
            }

            other.IsConcentrating = false;
        }

        record.IsConcentrating = true;

        return broken;
    }

    /// <summary>
    /// Возвращает причину, по которой заклинание нельзя применить.
    /// </summary>
    /// <param name="spell">Заклинание.</param>
    /// <param name="record">Запись книги заклинаний.</param>
    /// <param name="usesPreparation">Игровая система пользуется подготовкой.</param>
    /// <param name="formulaContext">Значения переменных персонажа.</param>
    /// <returns>Причина отказа либо <see langword="null"/>.</returns>
    private string? Blocked(
        Spell spell,
        CharacterSpell record,
        bool usesPreparation,
        IFormulaContext formulaContext)
    {
        if (_builder.CheckRequirement(spell.Requirements, formulaContext) is { } reason)
        {
            return $"Персонаж не может применить «{spell.Name}». {reason}";
        }

        // Кантрип всегда наготове: подготовка относится к заклинаниям с уровнем.
        if (usesPreparation && spell.Level > 0 && !record.IsPrepared)
        {
            return $"«{spell.Name}» не подготовлено. Подготовьте его перед применением.";
        }

        return null;
    }

    /// <summary>
    /// Собирает заклинание книги к показу.
    /// </summary>
    /// <param name="record">Запись книги заклинаний.</param>
    /// <param name="spell">Заклинание.</param>
    /// <param name="usesPreparation">Игровая система пользуется подготовкой.</param>
    /// <param name="formulaContext">Значения переменных персонажа.</param>
    /// <returns>Заклинание к показу.</returns>
    private SpellbookEntry BuildEntry(
        CharacterSpell record,
        Spell spell,
        bool usesPreparation,
        IFormulaContext formulaContext)
    {
        var castContext = CreateCastContext(formulaContext, spell, spell.Level);
        var issues = new List<string>();

        double? cost = null;

        if (spell.ResourceId is not null)
        {
            cost = string.IsNullOrWhiteSpace(spell.ResourceCostFormula)
                ? 1
                : EvaluateNumber(spell.ResourceCostFormula, castContext, "стоимость", issues);
        }

        var reason = Blocked(spell, record, usesPreparation, formulaContext);

        return new SpellbookEntry(
            record.Id,
            spell.Id,
            spell.Name,
            spell.Description,
            spell.Level,
            spell.School,
            spell.Category,
            spell.CastingTime,
            spell.Range,
            spell.Duration,
            spell.Components,
            spell.RequiresConcentration,
            spell.IsRitual,
            record.IsPrepared,
            record.IsConcentrating,
            record.TimesUsed,
            record.Source,
            spell.Resource?.Name,
            cost,
            DescribeRange(spell.Formula, castContext),
            !string.IsNullOrWhiteSpace(spell.ScalingFormula),
            reason is null,
            reason);
    }

    /// <summary>
    /// Описывает границы результата заклинания: «8–48» или «12».
    /// </summary>
    /// <param name="formula">Формула результата.</param>
    /// <param name="castContext">Значения переменных с уровнем применения.</param>
    /// <returns>Описание границ либо <see langword="null"/>.</returns>
    private string? DescribeRange(string? formula, IFormulaContext castContext)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return null;
        }

        var range = _formulas.EvaluateRange(formula, castContext);

        if (range.IsFailure)
        {
            return null;
        }

        return range.Value.IsExact
            ? Format(range.Value.Minimum)
            : $"{Format(range.Value.Minimum)}–{Format(range.Value.Maximum)}";
    }

    /// <summary>
    /// Создаёт источник значений с переменными уровня применения.
    /// </summary>
    /// <param name="formulaContext">Значения переменных персонажа.</param>
    /// <param name="spell">Заклинание.</param>
    /// <param name="castLevel">Уровень применения.</param>
    /// <returns>Источник значений для формул заклинания.</returns>
    private static LocalFormulaContext CreateCastContext(
        IFormulaContext formulaContext,
        Spell spell,
        int castLevel) =>
        new LocalFormulaContext(formulaContext)
            .With(CastLevelVariable, castLevel)
            .With(SpellLevelVariable, spell.Level)
            .With(ExtraLevelsVariable, Math.Max(0, castLevel - spell.Level));

    /// <summary>
    /// Создаёт запись журнала о применении заклинания.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="spell">Применённое заклинание.</param>
    /// <param name="castLevel">Уровень применения.</param>
    /// <param name="result">Вычисленный результат.</param>
    /// <returns>Запись журнала.</returns>
    private static HistoryEntry CreateHistoryEntry(
        Character character,
        Spell spell,
        int castLevel,
        double? result)
    {
        var level = spell.Level == 0
            ? "кантрип"
            : $"уровень {Format(castLevel)}";

        return new HistoryEntry
        {
            CharacterId = character.Id,
            Action = CastHistoryAction,

            // Название отдельно от описания: одно и то же заклинание на разных
            // уровнях даёт разные описания, а считается оно как одно.
            Subject = spell.Name,
            Description = $"Применено «{spell.Name}» ({level}).",
            NewValue = result is { } value ? Format(value) : null,
            Amount = result,
        };
    }

    /// <summary>
    /// Загружает последние применения заклинаний персонажа.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Записи истории, новые сверху.</returns>
    private static async Task<List<SpellCastRecord>> LoadHistoryAsync(
        RpgDbContext context,
        Guid characterId,
        CancellationToken cancellationToken)
    {
        var entries = await context.History
            .AsNoTracking()
            .Where(entry => entry.CharacterId == characterId && entry.Action == CastHistoryAction)
            .OrderByDescending(entry => entry.CreatedAt)
            .Take(HistoryPageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entries
            .Select(entry => new SpellCastRecord(
                entry.Description ?? "Заклинание применено.",
                entry.NewValue,
                entry.CreatedAt))
            .ToList();
    }

    /// <summary>
    /// Вычисляет предел книги заклинаний по формуле игровой системы.
    /// </summary>
    /// <param name="formula">Формула предела.</param>
    /// <param name="formulaContext">Значения переменных персонажа.</param>
    /// <returns>Предел либо <see langword="null"/>, если он не задан.</returns>
    private int? EvaluateLimit(string? formula, IFormulaContext formulaContext)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return null;
        }

        var result = _formulas.Evaluate(formula, formulaContext);

        return result.IsSuccess
            ? Math.Max(0, (int)Math.Round(result.Value.AsNumber(), MidpointRounding.AwayFromZero))
            : null;
    }

    private double EvaluateNumber(
        string? formula,
        IFormulaContext formulaContext,
        string description,
        List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return 0;
        }

        var result = _formulas.Evaluate(formula, formulaContext);

        if (result.IsSuccess)
        {
            return result.Value.AsNumber();
        }

        issues.Add($"Формула «{description}»: {result.Error}");

        return 0;
    }

    /// <summary>
    /// Проверяет, подходит ли заклинание под строку поиска.
    /// </summary>
    /// <param name="spell">Заклинание.</param>
    /// <param name="search">Строка поиска.</param>
    /// <returns><see langword="true"/>, если заклинание подходит.</returns>
    private static bool Matches(
        Spell spell, string? search, IReadOnlySet<string> aliasTargets)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var needle = search.Trim();

        return aliasTargets.Contains(spell.SystemName)
            || Contains(spell.Name, needle)
            || Contains(spell.School, needle)
            || Contains(spell.Category, needle)
            || Contains(spell.Description, needle);
    }

    private static bool Contains(string? value, string needle) =>
        value is not null && value.Contains(needle, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    /// Возвращает название раздела уровня: «Кантрипы», «1 уровень» и далее.
    /// </summary>
    /// <param name="level">Уровень заклинаний.</param>
    /// <returns>Название раздела.</returns>
    private static string LevelTitle(int level) =>
        level == 0 ? "Кантрипы" : $"{Format(level)} уровень";

    /// <summary>
    /// Создаёт источник значений переменных персонажа.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Источник значений переменных.</returns>
    private async Task<IFormulaContext> CreateContextAsync(
        Character character,
        CancellationToken cancellationToken)
    {
        var draft = _builder.CreateDraft(character);

        return await _builder.CreateContextAsync(draft, cancellationToken).ConfigureAwait(false);
    }

    private static CharacterSpell? Find(Character character, Guid characterSpellId) =>
        character.Spells.FirstOrDefault(record => record.Id == characterSpellId);

    private static List<CharacterOptionDetail> BuildOptionDetails(Spell spell)
    {
        var details = new List<CharacterOptionDetail>
        {
            new("Уровень", spell.Level == 0 ? "кантрип" : Format(spell.Level)),
        };

        if (!string.IsNullOrWhiteSpace(spell.School))
        {
            details.Add(new CharacterOptionDetail("Школа", spell.School));
        }

        if (!string.IsNullOrWhiteSpace(spell.Category))
        {
            details.Add(new CharacterOptionDetail("Категория", spell.Category));
        }

        if (spell.RequiresConcentration)
        {
            details.Add(new CharacterOptionDetail("Концентрация", "да"));
        }

        return details;
    }

    /// <summary>
    /// Загружает персонажа вместе с книгой заклинаний, ресурсами и игровой системой.
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
            .Include(character => character.GameSystem)
            .Include(character => character.Race)
            .Include(character => character.Class)
            .Include(character => character.Subclass)
            .Include(character => character.Background)
            .Include(character => character.Attributes)
            .Include(character => character.Skills)
            .Include(character => character.Traits)
            .Include(character => character.Resources)
                .ThenInclude(record => record.Resource)
            .Include(character => character.Spells)
                .ThenInclude(record => record.Spell)
                .ThenInclude(spell => spell!.Resource);

        return tracked
            ? query.FirstOrDefaultAsync(character => character.Id == characterId, cancellationToken)
            : query.AsNoTracking()
                .FirstOrDefaultAsync(character => character.Id == characterId, cancellationToken);
    }

    private static string Format(double value) =>
        value.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>
    /// Сообщает приложению, что состояние персонажа изменилось: расход ресурса
    /// и концентрация должны попасть на лист персонажа.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после публикации события.</returns>
    private Task PublishChangedAsync(Guid characterId, CancellationToken cancellationToken) =>
        _eventBus.PublishAsync(
            new CharacterChangedEvent(characterId, CharacterChangeKind.Recalculated),
            cancellationToken);
}
