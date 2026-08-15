using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Campaigns;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Master;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Master;

/// <summary>
/// Режим мастера: ведение сессии за всех персонажей сразу.
///
/// Служба намеренно не содержит игровых правил. «Урон» здесь — уменьшение
/// ресурса, выбранного мастером, потому что хиты в этом приложении такой же
/// ресурс, как мана или заряды посоха (решение Р-91). Наложение эффекта
/// выполняет служба эффектов со всеми её правилами наложения, а инициативу
/// вычисляет единый движок формул по формуле игровой системы (решение Р-92).
/// </summary>
public sealed class MasterService : IMasterService
{
    /// <summary>Количество эффектов, загружаемых в список наложения за один раз.</summary>
    public const int EffectPageSize = 200;

    /// <summary>Источник наложения, записываемый массовым действием мастера.</summary>
    public const string MasterSource = "Наложено мастером";

    /// <summary>Причина изменения ресурса, записываемая массовым действием мастера.</summary>
    public const string MassChangeReason = "изменено мастером";

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly IEffectService _effects;
    private readonly ICharacterBuilderService _builder;
    private readonly IFormulaEngine _formulas;
    private readonly ILogger<MasterService> _logger;

    /// <summary>
    /// Создаёт службу режима мастера.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="effects">Служба эффектов персонажа.</param>
    /// <param name="builder">Мастер создания персонажа: источник значений переменных.</param>
    /// <param name="formulas">Единый движок вычислений.</param>
    /// <param name="logger">Журналировщик.</param>
    public MasterService(
        IDbContextFactory<RpgDbContext> contextFactory,
        IEffectService effects,
        ICharacterBuilderService builder,
        IFormulaEngine formulas,
        ILogger<MasterService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _effects = Guard.NotNull(effects);
        _builder = Guard.NotNull(builder);
        _formulas = Guard.NotNull(formulas);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<MasterBoard>> GetBoardAsync(
        Guid? campaignId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var campaigns = await context.Campaigns.AsNoTracking()
                .OrderBy(campaign => campaign.Name)
                .Select(campaign => new MasterOption(campaign.Id, campaign.Name))
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            var roles = campaignId is { } id
                ? await context.CampaignMembers.AsNoTracking()
                    .Where(member => member.CampaignId == id
                        && member.ObjectKind == CampaignObjectKinds.Characters)
                    .ToDictionaryAsync(member => member.ObjectId, member => member.Role, cancellationToken)
                    .ConfigureAwait(false)
                : null;

            var characters = await LoadCharactersAsync(context, roles?.Keys, cancellationToken)
                .ConfigureAwait(false);

            var tracker = await FindTrackerAsync(context, campaignId, tracked: false, cancellationToken)
                .ConfigureAwait(false);

            var order = tracker?.Entries.ToDictionary(entry => entry.CharacterId)
                ?? [];

            var rows = characters
                .Select(character => CreateRow(character, roles, order))
                .OrderBy(row => row.Initiative.HasValue ? 0 : 1)
                .ThenBy(row => order.TryGetValue(row.Id, out var entry) ? entry.SortOrder : 0)
                .ThenBy(row => row.Name, StringComparer.CurrentCulture)
                .ToList();

            var initiative = await DescribeInitiativeAsync(
                context, campaignId, characters, tracker, cancellationToken).ConfigureAwait(false);

            return Result.Success(new MasterBoard(
                rows,
                CollectResources(characters),
                campaigns,
                initiative));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MasterLog.ActionFailed(_logger, exception, "чтение сводки");
            return Result.Failure<MasterBoard>("Не удалось прочитать сводку по персонажам.");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MasterOption>> GetEffectsAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = context.Effects.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(effect => EF.Functions.Like(effect.Name, pattern));
        }

        return await query
            .OrderBy(effect => effect.Name)
            .Take(EffectPageSize)
            .Select(effect => new MasterOption(effect.Id, effect.Name))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<MassResult>> ChangeResourceAsync(
        IReadOnlyCollection<Guid> characterIds,
        Guid resourceId,
        double delta,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(characterIds);

        if (characterIds.Count == 0)
        {
            return Result.Failure<MassResult>("Не выбрано ни одного персонажа.");
        }

        if (Math.Abs(delta) < double.Epsilon)
        {
            return Result.Failure<MassResult>("Величина изменения не задана.");
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var ids = characterIds.ToList();

            var resource = await context.Resources.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == resourceId, cancellationToken)
                .ConfigureAwait(false);

            if (resource is null)
            {
                return Result.Failure<MassResult>("Ресурс не найден: возможно, он был удалён.");
            }

            var names = await context.Characters.AsNoTracking()
                .Where(character => ids.Contains(character.Id))
                .ToDictionaryAsync(character => character.Id, character => character.Name, cancellationToken)
                .ConfigureAwait(false);

            var values = await context.Set<CharacterResource>()
                .Where(value => ids.Contains(value.CharacterId) && value.ResourceId == resourceId)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            var changed = 0;
            var failures = new List<string>();

            foreach (var id in ids)
            {
                var name = names.GetValueOrDefault(id) ?? "Персонаж";
                var value = values.FirstOrDefault(item => item.CharacterId == id);

                if (value is null)
                {
                    failures.Add($"{name}: ресурса «{resource.Name}» нет.");
                    continue;
                }

                var previous = value.Current;

                // Ресурс не уходит ниже нуля и выше своего максимума: предел
                // задан игровой системой, и массовое действие не вправе его обойти.
                value.Current = Math.Clamp(previous + delta, 0, value.Maximum);

                if (Math.Abs(previous - value.Current) < double.Epsilon)
                {
                    failures.Add($"{name}: «{resource.Name}» и так {Format(previous)}.");
                    continue;
                }

                changed++;

                // Запись делается общей заготовкой: изменение ресурса выглядит
                // в журнале одинаково, кем бы оно ни было выполнено.
                context.Add(HistoryEntries.ResourceChanged(
                    id, resource.Name, previous, value.Current, MassChangeReason));
            }

            if (changed > 0)
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            MasterLog.ResourceChanged(_logger, resource.Name, delta, changed);

            return Result.Success(new MassResult(changed, failures));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MasterLog.ActionFailed(_logger, exception, "массовое изменение ресурса");
            return Result.Failure<MassResult>("Не удалось изменить ресурс.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<MassResult>> ApplyEffectAsync(
        IReadOnlyCollection<Guid> characterIds,
        Guid effectId,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(characterIds);

        if (characterIds.Count == 0)
        {
            return Result.Failure<MassResult>("Не выбрано ни одного персонажа.");
        }

        var names = await LoadNamesAsync(characterIds, cancellationToken).ConfigureAwait(false);
        var changed = 0;
        var failures = new List<string>();

        // Наложение выполняет служба эффектов: она знает правила повторного
        // наложения и длительность, и знать их дважды приложение не должно.
        foreach (var id in characterIds)
        {
            var result = await _effects.ApplyAsync(id, effectId, MasterSource, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                changed++;
            }
            else
            {
                failures.Add($"{names.GetValueOrDefault(id) ?? "Персонаж"}: {result.Error}");
            }
        }

        var effectName = await LoadEffectNameAsync(effectId, cancellationToken).ConfigureAwait(false);
        MasterLog.EffectApplied(_logger, effectName, changed);

        return Result.Success(new MassResult(changed, failures));
    }

    /// <inheritdoc />
    public async Task<Result<MassResult>> RemoveEffectAsync(
        IReadOnlyCollection<Guid> characterIds,
        Guid effectId,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(characterIds);

        if (characterIds.Count == 0)
        {
            return Result.Failure<MassResult>("Не выбрано ни одного персонажа.");
        }

        var names = await LoadNamesAsync(characterIds, cancellationToken).ConfigureAwait(false);

        List<CharacterEffect> applied;

        await using (var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
        {
            var ids = characterIds.ToList();

            applied = await context.Set<CharacterEffect>().AsNoTracking()
                .Where(record => ids.Contains(record.CharacterId)
                    && record.EffectId == effectId
                    && record.IsActive)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        var changed = 0;
        var failures = new List<string>();

        foreach (var id in characterIds)
        {
            var name = names.GetValueOrDefault(id) ?? "Персонаж";
            var record = applied.FirstOrDefault(item => item.CharacterId == id);

            if (record is null)
            {
                failures.Add($"{name}: эффект не наложен.");
                continue;
            }

            var result = await _effects.RemoveAsync(id, record.Id, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                changed++;
            }
            else
            {
                failures.Add($"{name}: {result.Error}");
            }
        }

        var effectName = await LoadEffectNameAsync(effectId, cancellationToken).ConfigureAwait(false);
        MasterLog.EffectRemoved(_logger, effectName, changed);

        return Result.Success(new MassResult(changed, failures));
    }

    /// <inheritdoc />
    public async Task<Result<MassResult>> RollInitiativeAsync(
        Guid? campaignId,
        IReadOnlyCollection<Guid> characterIds,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(characterIds);

        if (characterIds.Count == 0)
        {
            return Result.Failure<MassResult>("Не выбрано ни одного участника боя.");
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var ids = characterIds.ToList();

            var characters = await LoadCharactersAsync(context, ids, cancellationToken)
                .ConfigureAwait(false);

            var formula = await FindInitiativeFormulaAsync(context, campaignId, characters, cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(formula))
            {
                return Result.Failure<MassResult>(
                    "Порядок хода не задан игровой системой: заполните формулу инициативы.");
            }

            var tracker = await FindTrackerAsync(context, campaignId, tracked: true, cancellationToken)
                .ConfigureAwait(false);

            if (tracker is null)
            {
                tracker = new InitiativeTracker { CampaignId = campaignId };
                context.Add(tracker);
            }
            else
            {
                context.RemoveRange(tracker.Entries);
                tracker.Entries.Clear();
            }

            tracker.Round = 1;

            var rolled = new List<(Character Character, double Value)>();
            var failures = new List<string>();

            foreach (var character in characters)
            {
                var value = await EvaluateAsync(formula, character, cancellationToken).ConfigureAwait(false);

                if (value is null)
                {
                    failures.Add($"{character.Name}: формулу «{formula}» вычислить не удалось.");
                    continue;
                }

                rolled.Add((character, value.Value));
            }

            // Больший результат ходит раньше. Система с обратным порядком
            // записывает формулу, уже учитывающую это (решение Р-92).
            var ordered = rolled
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Character.Name, StringComparer.CurrentCulture)
                .ToList();

            for (var index = 0; index < ordered.Count; index++)
            {
                tracker.Entries.Add(new InitiativeEntry
                {
                    TrackerId = tracker.Id,
                    CharacterId = ordered[index].Character.Id,
                    Value = ordered[index].Value,
                    SortOrder = index,
                    IsCurrent = index == 0,
                });
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            MasterLog.InitiativeRolled(_logger, formula, ordered.Count);

            return Result.Success(new MassResult(ordered.Count, failures));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MasterLog.ActionFailed(_logger, exception, "бросок инициативы");
            return Result.Failure<MassResult>("Не удалось бросить инициативу.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> SetInitiativeAsync(
        Guid? campaignId,
        Guid characterId,
        double value,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var tracker = await FindTrackerAsync(context, campaignId, tracked: true, cancellationToken)
                .ConfigureAwait(false);

            var entry = tracker?.Entries.FirstOrDefault(item => item.CharacterId == characterId);

            if (tracker is null || entry is null)
            {
                return Result.Failure("Участник не состоит в очереди хода.");
            }

            entry.Value = value;
            Renumber(tracker);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MasterLog.ActionFailed(_logger, exception, "изменение инициативы");
            return Result.Failure("Не удалось изменить инициативу.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> NextTurnAsync(Guid? campaignId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var tracker = await FindTrackerAsync(context, campaignId, tracked: true, cancellationToken)
                .ConfigureAwait(false);

            if (tracker is null || tracker.Entries.Count == 0)
            {
                return Result.Failure("Очередь хода пуста: сначала бросьте инициативу.");
            }

            var order = tracker.Entries.OrderBy(entry => entry.SortOrder).ToList();
            var current = order.FindIndex(entry => entry.IsCurrent);
            var next = current + 1;

            // Круг замкнулся — начинается следующий раунд.
            if (next >= order.Count)
            {
                next = 0;
                tracker.Round++;
            }

            foreach (var entry in order)
            {
                entry.IsCurrent = false;
            }

            order[next].IsCurrent = true;

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var name = await context.Characters.AsNoTracking()
                .Where(character => character.Id == order[next].CharacterId)
                .Select(character => character.Name)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            MasterLog.TurnAdvanced(_logger, name ?? "участник", tracker.Round);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MasterLog.ActionFailed(_logger, exception, "передача хода");
            return Result.Failure("Не удалось передать ход.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> EndCombatAsync(Guid? campaignId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var tracker = await FindTrackerAsync(context, campaignId, tracked: true, cancellationToken)
                .ConfigureAwait(false);

            if (tracker is null)
            {
                return Result.Success();
            }

            context.RemoveRange(tracker.Entries);
            tracker.Entries.Clear();
            tracker.Round = 1;

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            MasterLog.CombatEnded(_logger);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MasterLog.ActionFailed(_logger, exception, "завершение боя");
            return Result.Failure("Не удалось завершить бой.");
        }
    }

    /// <summary>
    /// Загружает персонажей со всем, что показывает сводка.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="ids">Идентификаторы персонажей; <see langword="null"/> — все персонажи.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Персонажи в порядке имён.</returns>
    private static async Task<List<Character>> LoadCharactersAsync(
        RpgDbContext context,
        IEnumerable<Guid>? ids,
        CancellationToken cancellationToken)
    {
        // Заготовки персонажей — не участники игры, а образцы для создания новых.
        var query = context.Characters.AsNoTracking()
            .Where(character => !character.IsTemplate);

        if (ids is not null)
        {
            var selected = ids.ToList();
            query = query.Where(character => selected.Contains(character.Id));
        }

        return await query
            .Include(character => character.Race)
            .Include(character => character.Class)
            .Include(character => character.Attributes)
            .Include(character => character.Resources)
                .ThenInclude(value => value.Resource)
            .Include(character => character.Effects)
                .ThenInclude(record => record.Effect)
            .OrderBy(character => character.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Собирает строку сводки по персонажу.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="roles">Роли участников кампании; <see langword="null"/> — отбора нет.</param>
    /// <param name="order">Очередь хода по идентификатору персонажа.</param>
    /// <returns>Строка сводки.</returns>
    private static MasterCharacter CreateRow(
        Character character,
        IReadOnlyDictionary<Guid, string?>? roles,
        IReadOnlyDictionary<Guid, InitiativeEntry> order)
    {
        var entry = order.GetValueOrDefault(character.Id);

        return new MasterCharacter(
            character.Id,
            character.Name,
            character.Level,
            roles?.GetValueOrDefault(character.Id),
            character.Race?.Name,
            character.Class?.Name,
            character.Portrait,
            character.Resources
                .Where(value => value.Resource is not null)
                .OrderBy(value => value.Resource!.SortOrder)
                .ThenBy(value => value.Resource!.Name, StringComparer.CurrentCulture)
                .Select(value => new MasterResource(
                    value.ResourceId,
                    value.Resource!.Name,
                    value.Current,
                    value.Maximum,
                    value.Resource.Color))
                .ToList(),
            character.Effects
                .Where(record => record.IsActive && record.Effect is not null)
                .OrderByDescending(record => record.Effect!.Priority)
                .ThenBy(record => record.Effect!.Name, StringComparer.CurrentCulture)
                .Select(record => new MasterEffect(
                    record.EffectId,
                    record.Effect!.Name,
                    record.Effect.Tone,
                    record.Effect.Color,
                    record.Stacks))
                .ToList(),
            entry?.Value,
            entry?.IsCurrent ?? false);
    }

    /// <summary>
    /// Собирает ресурсы, встречающиеся у показанных персонажей.
    /// </summary>
    /// <param name="characters">Персонажи сводки.</param>
    /// <returns>Ресурсы в порядке отображения.</returns>
    private static List<MasterOption> CollectResources(IEnumerable<Character> characters) =>
        characters
            .SelectMany(character => character.Resources)
            .Where(value => value.Resource is not null)
            .GroupBy(value => value.ResourceId)
            .Select(group => group.First().Resource!)
            .OrderBy(resource => resource.SortOrder)
            .ThenBy(resource => resource.Name, StringComparer.CurrentCulture)
            .Select(resource => new MasterOption(resource.Id, resource.Name))
            .ToList();

    /// <summary>
    /// Находит очередь хода кампании.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="campaignId">Кампания; <see langword="null"/> — очередь вне кампаний.</param>
    /// <param name="tracked">Загружать для изменения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Очередь хода или <see langword="null"/>, если её ещё нет.</returns>
    private static Task<InitiativeTracker?> FindTrackerAsync(
        RpgDbContext context,
        Guid? campaignId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<InitiativeTracker> query = context.InitiativeTrackers
            .Include(tracker => tracker.Entries);

        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(tracker => tracker.CampaignId == campaignId, cancellationToken);
    }

    /// <summary>
    /// Описывает состояние очереди хода.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="campaignId">Кампания.</param>
    /// <param name="characters">Показанные персонажи.</param>
    /// <param name="tracker">Очередь хода.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Состояние очереди.</returns>
    private static async Task<InitiativeState> DescribeInitiativeAsync(
        RpgDbContext context,
        Guid? campaignId,
        IReadOnlyList<Character> characters,
        InitiativeTracker? tracker,
        CancellationToken cancellationToken)
    {
        var formula = await FindInitiativeFormulaAsync(context, campaignId, characters, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(formula))
        {
            return InitiativeState.Disabled(
                "Порядок хода не задан: в игровой системе нет формулы инициативы. "
                + "Заполните её в разделе «Контент» → «Игровые системы», если он есть в вашей игре.");
        }

        return new InitiativeState(
            true,
            formula,
            tracker?.Round ?? 1,
            tracker is { Entries.Count: > 0 },
            null);
    }

    /// <summary>
    /// Находит формулу инициативы: сначала у игровой системы кампании,
    /// затем у систем показанных персонажей.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="campaignId">Кампания.</param>
    /// <param name="characters">Показанные персонажи.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Формула инициативы либо <see langword="null"/>.</returns>
    private static async Task<string?> FindInitiativeFormulaAsync(
        RpgDbContext context,
        Guid? campaignId,
        IReadOnlyList<Character> characters,
        CancellationToken cancellationToken)
    {
        var systems = new List<Guid>();

        if (campaignId is { } id)
        {
            var campaignSystem = await context.Campaigns.AsNoTracking()
                .Where(campaign => campaign.Id == id)
                .Select(campaign => campaign.GameSystemId)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (campaignSystem is { } value)
            {
                systems.Add(value);
            }
        }

        systems.AddRange(characters
            .Select(character => character.GameSystemId)
            .OfType<Guid>()
            .Distinct());

        if (systems.Count == 0)
        {
            return null;
        }

        var formulas = await context.GameSystems.AsNoTracking()
            .Where(system => systems.Contains(system.Id) && system.InitiativeFormula != null)
            .ToDictionaryAsync(system => system.Id, system => system.InitiativeFormula, cancellationToken)
            .ConfigureAwait(false);

        // Порядок важен: система кампании перекрывает системы отдельных персонажей.
        return systems
            .Select(system => formulas.GetValueOrDefault(system))
            .FirstOrDefault(formula => !string.IsNullOrWhiteSpace(formula));
    }

    /// <summary>
    /// Перенумеровывает очередь хода по значениям инициативы.
    /// </summary>
    /// <param name="tracker">Очередь хода.</param>
    private static void Renumber(InitiativeTracker tracker)
    {
        var ordered = tracker.Entries
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.SortOrder)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].SortOrder = index;
        }
    }

    /// <summary>
    /// Вычисляет формулу в значениях персонажа.
    /// </summary>
    /// <param name="formula">Формула.</param>
    /// <param name="character">Персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат вычисления либо <see langword="null"/>, если формула неверна.</returns>
    private async Task<double?> EvaluateAsync(
        string formula,
        Character character,
        CancellationToken cancellationToken)
    {
        var context = await _builder
            .CreateContextAsync(_builder.CreateDraft(character), cancellationToken)
            .ConfigureAwait(false);

        var evaluated = _formulas.Evaluate(formula, context);

        return evaluated.IsSuccess ? evaluated.Value.AsNumber() : null;
    }

    /// <summary>
    /// Загружает имена персонажей.
    /// </summary>
    /// <param name="characterIds">Идентификаторы персонажей.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Имена, сопоставленные идентификаторам.</returns>
    private async Task<Dictionary<Guid, string>> LoadNamesAsync(
        IReadOnlyCollection<Guid> characterIds,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var ids = characterIds.ToList();

        return await context.Characters.AsNoTracking()
            .Where(character => ids.Contains(character.Id))
            .ToDictionaryAsync(character => character.Id, character => character.Name, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Загружает название эффекта для журнала.
    /// </summary>
    /// <param name="effectId">Идентификатор эффекта.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Название эффекта.</returns>
    private async Task<string> LoadEffectNameAsync(Guid effectId, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var name = await context.Effects.AsNoTracking()
            .Where(effect => effect.Id == effectId)
            .Select(effect => effect.Name)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return name ?? "эффект";
    }

    /// <summary>
    /// Записывает число без лишних нулей.
    /// </summary>
    /// <param name="value">Значение.</param>
    /// <returns>Текст числа.</returns>
    private static string Format(double value) =>
        value.ToString("0.##", CultureInfo.CurrentCulture);
}
