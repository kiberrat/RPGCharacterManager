using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.Engine;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Characters;

/// <summary>
/// Отдых персонажа: восстановление ресурсов и течение времени.
///
/// Служба не знает ни короткого, ни длительного отдыха: любой отдых — это запись
/// игрового контента со своим списком восстановлений, требованием и длительностью.
/// Величины восстановления вычисляет единый движок формул, поэтому «половина
/// максимума», «уровень костей хитов» и «полностью» описываются одинаково.
/// </summary>
public sealed class RestService : IRestService
{
    /// <summary>Описание восстановления ресурса до максимума.</summary>
    public const string FullRestoreDescription = "до максимума";

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly ICharacterBuilderService _builder;
    private readonly IEffectService _effects;
    private readonly IFormulaEngine _formulas;
    private readonly IEventBus _eventBus;
    private readonly ILogger<RestService> _logger;

    /// <summary>
    /// Создаёт службу отдыха.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="builder">Мастер создания персонажа: расчёт, требования и правила событий.</param>
    /// <param name="effects">Служба эффектов: продвижение таймеров на время отдыха.</param>
    /// <param name="formulas">Единый движок вычислений.</param>
    /// <param name="eventBus">Шина событий приложения.</param>
    /// <param name="logger">Журналировщик.</param>
    public RestService(
        IDbContextFactory<RpgDbContext> contextFactory,
        ICharacterBuilderService builder,
        IEffectService effects,
        IFormulaEngine formulas,
        IEventBus eventBus,
        ILogger<RestService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _builder = Guard.NotNull(builder);
        _effects = Guard.NotNull(effects);
        _formulas = Guard.NotNull(formulas);
        _eventBus = Guard.NotNull(eventBus);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<RestState>> GetAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var character = await LoadCharacterAsync(context, characterId, tracked: false, cancellationToken)
            .ConfigureAwait(false);

        if (character is null)
        {
            return Result.Failure<RestState>("Персонаж не найден: возможно, он был удалён.");
        }

        var draft = _builder.CreateDraft(character);
        var formulaContext = await _builder.CreateContextAsync(draft, cancellationToken).ConfigureAwait(false);

        var rests = await LoadRestTypesAsync(context, character.GameSystemId, cancellationToken)
            .ConfigureAwait(false);

        var options = new List<RestOption>(rests.Count);

        foreach (var rest in rests)
        {
            var reason = _builder.CheckRequirement(rest.Requirements, formulaContext);

            options.Add(new RestOption(
                rest.Id,
                rest.Name,
                rest.Description,
                DescribeDuration(rest),
                reason is null,
                reason,
                DescribeRestores(rest)));
        }

        return Result.Success(new RestState(options));
    }

    /// <inheritdoc />
    public async Task<Result<RestResult>> RestAsync(
        Guid characterId,
        Guid restTypeId,
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
                return Result.Failure<RestResult>("Персонаж не найден: возможно, он был удалён.");
            }

            var rest = await context.RestTypes
                .Include(entity => entity.Restores)
                    .ThenInclude(restore => restore.Resource)
                .FirstOrDefaultAsync(entity => entity.Id == restTypeId, cancellationToken)
                .ConfigureAwait(false);

            if (rest is null)
            {
                return Result.Failure<RestResult>("Вид отдыха не найден: возможно, он был удалён.");
            }

            var draft = _builder.CreateDraft(character);
            var formulaContext = await _builder.CreateContextAsync(draft, cancellationToken).ConfigureAwait(false);

            if (_builder.CheckRequirement(rest.Requirements, formulaContext) is { } reason)
            {
                return Result.Failure<RestResult>($"«{rest.Name}» сейчас недоступен. {reason}");
            }

            var issues = new List<string>();
            var before = HistoryEntries.SnapshotResources(character);

            // Правила события отдыха применяются к базовым значениям, как правила
            // повышения уровня: механика вида «после длительного отдыха истощение
            // уменьшается на единицу» описывается правилом, а не кодом приложения.
            var appliedRules = await _builder
                .ApplyEventAsync(draft, RuleTriggers.Rest(rest.SystemName), cancellationToken)
                .ConfigureAwait(false);

            // Пересчёт выполняется до восстановления, а не после: сохранённый
            // максимум ресурса успевает устареть — например, после надевания брони,
            // повышающей запас здоровья, — и отдых «до максимума» поднял бы ресурс
            // до прежнего значения. Кроме того, правила события уже изменили
            // базовые значения, и максимумы обязаны их учитывать.
            var calculation = await _builder.CalculateAsync(draft, cancellationToken).ConfigureAwait(false);

            var added = new List<object>();
            CharacterWriter.ApplyCalculation(character, calculation, added);

            // Значения переменных берутся заново, только если правила их изменили:
            // формула восстановления должна видеть уже изменённого персонажа.
            var restoreContext = appliedRules.Count > 0
                ? await _builder.CreateContextAsync(draft, cancellationToken).ConfigureAwait(false)
                : formulaContext;

            Restore(character, rest, restoreContext, issues);

            var changes = Describe(character, before);

            context.AddRange(added);
            context.Add(CreateHistoryEntry(character, rest, changes));
            context.AddRange(HistoryEntries.ResourceChanges(character, before, rest.Name));

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Время идёт после восстановления: эффект, срок которого истекает
            // за время отдыха, не должен успеть повлиять на восстановленные значения.
            var expired = await AdvanceTimeAsync(characterId, rest, cancellationToken).ConfigureAwait(false);

            CharacterLog.CharacterRested(_logger, character.Name, rest.Name);

            await _eventBus
                .PublishAsync(
                    new CharacterChangedEvent(characterId, CharacterChangeKind.Recalculated),
                    cancellationToken)
                .ConfigureAwait(false);

            return Result.Success(new RestResult(
                rest.Name,
                changes,
                expired,
                [.. appliedRules, .. calculation.AppliedRules],
                [.. issues, .. calculation.Issues.Select(issue => issue.Message)]));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CharacterLog.RestOperationFailed(_logger, exception, characterId);

            return Result.Failure<RestResult>("Не удалось выполнить отдых. Подробности записаны в журнал.");
        }
    }

    /// <summary>
    /// Восстанавливает ресурсы персонажа по списку восстановлений отдыха.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="rest">Вид отдыха.</param>
    /// <param name="formulaContext">Источник значений переменных персонажа.</param>
    /// <param name="issues">Замечания вычисления.</param>
    private void Restore(
        Character character,
        RestType rest,
        IFormulaContext formulaContext,
        List<string> issues)
    {
        foreach (var restore in rest.Restores.OrderBy(entry => entry.SortOrder))
        {
            // Восстановление без выбранного ресурса относится ко всем ресурсам
            // персонажа: длительный отдых обычно восстанавливает всё сразу.
            var targets = restore.ResourceId is { } resourceId
                ? character.Resources.Where(entry => entry.ResourceId == resourceId)
                : character.Resources;

            foreach (var resource in targets.ToList())
            {
                var name = resource.Resource?.Name ?? restore.Resource?.Name ?? "ресурс";
                var scoped = new LocalFormulaContext(formulaContext)
                    .With(RestVariables.Maximum, resource.Maximum)
                    .With(RestVariables.Current, resource.Current);

                if (!Allowed(restore, scoped, name, issues))
                {
                    continue;
                }

                resource.Current = restore.Mode == RestRestoreMode.Full
                    ? resource.Maximum
                    : Math.Clamp(
                        resource.Current + Evaluate(restore.Formula, scoped, name, issues),
                        0,
                        resource.Maximum);
            }
        }
    }

    /// <summary>
    /// Проверяет условие восстановления.
    /// </summary>
    /// <param name="restore">Восстановление.</param>
    /// <param name="formulaContext">Источник значений переменных.</param>
    /// <param name="resourceName">Название ресурса для замечания.</param>
    /// <param name="issues">Замечания вычисления.</param>
    /// <returns><see langword="true"/>, если восстановление происходит.</returns>
    private bool Allowed(
        RestRestore restore,
        IFormulaContext formulaContext,
        string resourceName,
        List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(restore.Condition))
        {
            return true;
        }

        var result = _formulas.Evaluate(restore.Condition, formulaContext);

        if (result.IsFailure)
        {
            issues.Add($"Условие восстановления «{resourceName}» не вычислено: {result.Error}");

            return false;
        }

        return result.Value.AsBoolean();
    }

    /// <summary>
    /// Вычисляет величину восстановления.
    /// </summary>
    /// <param name="formula">Формула величины.</param>
    /// <param name="formulaContext">Источник значений переменных.</param>
    /// <param name="resourceName">Название ресурса для замечания.</param>
    /// <param name="issues">Замечания вычисления.</param>
    /// <returns>Величина восстановления.</returns>
    private double Evaluate(
        string? formula,
        IFormulaContext formulaContext,
        string resourceName,
        List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            issues.Add($"У восстановления «{resourceName}» не задана формула величины.");

            return 0;
        }

        var result = _formulas.Evaluate(formula, formulaContext);

        if (result.IsFailure)
        {
            issues.Add($"Формула восстановления «{resourceName}» не вычислена: {result.Error}");

            return 0;
        }

        return result.Value.AsNumber();
    }

    /// <summary>
    /// Продвигает таймеры эффектов на длительность отдыха.
    ///
    /// Время идёт только в той единице, в которой измерен сам отдых: сколько
    /// раундов в часе, знает игровая система, а не приложение. Эффект, измеренный
    /// в другой единице, отдых не затрагивает.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="rest">Вид отдыха.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Названия эффектов, срок которых истёк.</returns>
    private async Task<IReadOnlyList<string>> AdvanceTimeAsync(
        Guid characterId,
        RestType rest,
        CancellationToken cancellationToken)
    {
        if (rest.Duration is not { } duration
            || duration <= 0
            || string.IsNullOrWhiteSpace(rest.DurationUnit))
        {
            return [];
        }

        var advanced = await _effects
            .AdvanceAsync(characterId, rest.DurationUnit, duration, cancellationToken)
            .ConfigureAwait(false);

        return advanced.IsSuccess ? advanced.Value.Expired : [];
    }

    /// <summary>
    /// Собирает изменения ресурсов для отчёта.
    /// </summary>
    /// <param name="character">Персонаж после отдыха.</param>
    /// <param name="before">Значения ресурсов до отдыха.</param>
    /// <returns>Изменения ресурсов.</returns>
    private static List<RestResourceChange> Describe(
        Character character,
        Dictionary<Guid, double> before) =>
        character.Resources
            .Where(resource => before.TryGetValue(resource.ResourceId, out var previous)
                && Math.Abs(previous - resource.Current) > double.Epsilon)
            .Select(resource => new RestResourceChange(
                resource.Resource?.Name ?? "Ресурс",
                before[resource.ResourceId],
                resource.Current))
            .ToList();

    /// <summary>
    /// Описывает длительность отдыха.
    /// </summary>
    /// <param name="rest">Вид отдыха.</param>
    /// <returns>Длительность в виде текста либо <see langword="null"/>.</returns>
    private static string? DescribeDuration(RestType rest) =>
        rest.Duration is { } duration && !string.IsNullOrWhiteSpace(rest.DurationUnit)
            ? $"{Format(duration)} {rest.DurationUnit.Trim()}"
            : null;

    /// <summary>
    /// Описывает, что отдых восстановит.
    /// </summary>
    /// <param name="rest">Вид отдыха.</param>
    /// <returns>Описания восстановлений.</returns>
    private static List<RestRestorePreview> DescribeRestores(RestType rest) =>
        rest.Restores
            .OrderBy(restore => restore.SortOrder)
            .Select(restore => new RestRestorePreview(
                restore.Resource?.Name ?? "Все ресурсы",
                restore.Mode == RestRestoreMode.Full
                    ? FullRestoreDescription
                    : restore.Formula ?? "формула не задана",
                restore.Condition))
            .ToList();

    /// <summary>
    /// Создаёт запись журнала об отдыхе.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="rest">Вид отдыха.</param>
    /// <param name="changes">Изменения ресурсов.</param>
    /// <returns>Запись журнала.</returns>
    private static HistoryEntry CreateHistoryEntry(
        Character character,
        RestType rest,
        IReadOnlyList<RestResourceChange> changes) => new()
        {
            CharacterId = character.Id,
            Action = HistoryActions.Rest,
            Subject = rest.Name,
            Description = DescribeDuration(rest) is { } duration
                ? $"Отдых «{rest.Name}» ({duration})."
                : $"Отдых «{rest.Name}».",
            NewValue = changes.Count == 0
                ? null
                : string.Join("; ", changes.Select(change =>
                    $"{change.ResourceName}: {Format(change.Before)} → {Format(change.After)}")),
        };

    /// <summary>
    /// Загружает виды отдыха, доступные игровой системе персонажа.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="gameSystemId">Игровая система персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Виды отдыха в заданном пользователем порядке.</returns>
    private static async Task<List<RestType>> LoadRestTypesAsync(
        RpgDbContext context,
        Guid? gameSystemId,
        CancellationToken cancellationToken)
    {
        var query = context.RestTypes
            .AsNoTracking()
            .Include(rest => rest.Restores)
                .ThenInclude(restore => restore.Resource)
            .AsQueryable();

        // Вид отдыха без игровой системы доступен всем: так пользователь может
        // описать отдых один раз и пользоваться им в любой своей системе.
        if (gameSystemId is { } systemId)
        {
            query = query.Where(rest => rest.GameSystemId == null || rest.GameSystemId == systemId);
        }

        var rests = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return rests
            .OrderBy(rest => rest.SortOrder)
            .ThenBy(rest => rest.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>
    /// Загружает персонажа вместе с ресурсами и их описаниями.
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
                .ThenInclude(record => record.Resource);

        return tracked
            ? query.FirstOrDefaultAsync(character => character.Id == characterId, cancellationToken)
            : query.AsNoTracking()
                .FirstOrDefaultAsync(character => character.Id == characterId, cancellationToken);
    }

    private static string Format(double value) =>
        value.ToString("0.##", CultureInfo.CurrentCulture);
}
