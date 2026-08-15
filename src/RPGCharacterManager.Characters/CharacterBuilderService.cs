using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Core.Models.Rules;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Characters;

/// <summary>
/// Мастер создания персонажа.
///
/// Служба собирает страницы мастера из описаний шагов, проверяет требования
/// выбранных объектов единым движком формул и создаёт персонажа со всеми
/// вычисленными значениями. Ни одно правило конкретной игры здесь не запрограммировано:
/// состав вариантов и их требования берутся из контента, созданного пользователем.
/// </summary>
public sealed class CharacterBuilderService : ICharacterBuilderService
{
    /// <summary>Наибольшее количество характеристик и ресурсов, участвующих в расчёте.</summary>
    public const int SupportingObjectLimit = 1000;

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly IFormulaEngine _formulas;
    private readonly IRuleService _ruleService;
    private readonly ICharacterCalculator _calculator;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CharacterBuilderService> _logger;

    private readonly Dictionary<string, IContentOptionSource> _sources;
    private readonly ContentOptionSource<AttributeDefinition> _attributeSource;
    private readonly ContentOptionSource<GameResource> _resourceSource;

    /// <summary>
    /// Создаёт мастер создания персонажа.
    /// </summary>
    /// <param name="stepProviders">Поставщики шагов мастера.</param>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="formulas">Единый движок вычислений.</param>
    /// <param name="ruleService">Хранилище игровых правил.</param>
    /// <param name="calculator">Служба расчёта параметров персонажа.</param>
    /// <param name="eventBus">Шина событий приложения.</param>
    /// <param name="logger">Журналировщик.</param>
    public CharacterBuilderService(
        IEnumerable<ICharacterStepProvider> stepProviders,
        IDbContextFactory<RpgDbContext> contextFactory,
        IFormulaEngine formulas,
        IRuleService ruleService,
        ICharacterCalculator calculator,
        IEventBus eventBus,
        ILogger<CharacterBuilderService> logger)
    {
        Guard.NotNull(stepProviders);

        _contextFactory = Guard.NotNull(contextFactory);
        _formulas = Guard.NotNull(formulas);
        _ruleService = Guard.NotNull(ruleService);
        _calculator = Guard.NotNull(calculator);
        _eventBus = Guard.NotNull(eventBus);
        _logger = Guard.NotNull(logger);

        Steps = stepProviders
            .SelectMany(provider => provider.GetSteps())
            .OrderBy(step => step.Order)
            .ThenBy(step => step.Title, StringComparer.CurrentCulture)
            .ToList();

        _sources = new Dictionary<string, IContentOptionSource>(StringComparer.Ordinal);

        foreach (var step in Steps)
        {
            if (ContentOptionSourceFactory.Create(step, _contextFactory) is { } source)
            {
                _sources[step.Id] = source;
            }
        }

        _attributeSource = new ContentOptionSource<AttributeDefinition>(_contextFactory, [], ContentTypeIds.Attributes);
        _resourceSource = new ContentOptionSource<GameResource>(_contextFactory, [], ContentTypeIds.Resources);
    }

    /// <inheritdoc />
    public IReadOnlyList<CharacterStepDefinition> Steps { get; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GameSystemOption>> GetGameSystemsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.GameSystems
            .AsNoTracking()
            .Where(system => system.Enabled)
            .OrderBy(system => system.Name)
            .Select(system => new GameSystemOption(
                system.Id,
                system.Name,
                system.Description,
                system.Version))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentSourceOption>> GetSourcesAsync(
        Guid? gameSystemId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.ContentPacks
            .AsNoTracking()
            .Where(pack => pack.Enabled)
            .Where(pack => pack.GameSystemId == null || pack.GameSystemId == gameSystemId)
            .OrderBy(pack => pack.Name)
            .Select(pack => new ContentSourceOption(pack.Id, pack.Name, pack.Description, pack.Version))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttributeDefinition>> GetAttributesAsync(
        CharacterDraft draft,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(draft);

        return await LoadAttributesAsync(
            draft,
            includeHidden: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Загружает характеристики игровой системы. Скрытые характеристики не показываются
    /// в мастере, но участвуют в расчёте как служебные переменные формул.
    /// </summary>
    private async Task<IReadOnlyList<AttributeDefinition>> LoadAttributesAsync(
        CharacterDraft draft,
        bool includeHidden,
        CancellationToken cancellationToken)
    {
        var loaded = await _attributeSource
            .LoadAsync(CreateQuery(draft, SupportingObjectLimit), cancellationToken)
            .ConfigureAwait(false);

        var attributes = loaded.Items.Cast<AttributeDefinition>();

        if (!includeHidden)
        {
            attributes = attributes.Where(attribute => !attribute.IsHidden);
        }

        return attributes
            .OrderBy(attribute => attribute.SortOrder)
            .ThenBy(attribute => attribute.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<CharacterOptionPage> GetOptionsAsync(
        CharacterStepDefinition step,
        CharacterDraft draft,
        string? search,
        bool includeUnavailable,
        int limit,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(step);
        Guard.NotNull(draft);

        if (!_sources.TryGetValue(step.Id, out var source))
        {
            return new CharacterOptionPage([], 0);
        }

        var parentId = step.ParentStepId is null ? null : draft.GetSelection(step.ParentStepId);

        // Шаг, зависящий от другого шага, не показывает вариантов, пока выбор не сделан:
        // подклассы существуют только у конкретного класса.
        if (step.ParentStepId is not null && parentId is null)
        {
            return new CharacterOptionPage([], 0);
        }

        var query = new ContentOptionQuery(
            draft.GameSystemId,
            draft.UseAllSources,
            draft.EnabledSourceIds,
            step.ParentPropertyName,
            parentId,
            search,
            limit);

        var loaded = await source.LoadAsync(query, cancellationToken).ConfigureAwait(false);

        var context = await CreateContextAsync(draft, cancellationToken).ConfigureAwait(false);
        var requiredNames = await LoadRequiredNamesAsync(step, source, loaded.Items, cancellationToken)
            .ConfigureAwait(false);

        var selected = GetSelectedIds(step, draft);
        var options = new List<CharacterOption>(loaded.Items.Count);

        foreach (var entity in loaded.Items)
        {
            var option = CreateOption(step, entity, context, requiredNames, selected);

            if (option.IsAvailable || includeUnavailable)
            {
                options.Add(option);
            }
        }

        return new CharacterOptionPage(options, loaded.TotalCount);
    }

    /// <inheritdoc />
    public void SetSelection(CharacterStepDefinition step, CharacterDraft draft, Guid? optionId)
    {
        Guard.NotNull(step);
        Guard.NotNull(draft);

        if (optionId is { } id)
        {
            draft.Selections[step.Id] = id;
        }
        else
        {
            draft.Selections.Remove(step.Id);
        }

        step.WriteSelection?.Invoke(draft.Character, optionId);

        // Смена выбора обесценивает выбор зависящих шагов: подкласс принадлежит
        // конкретному классу и при его смене становится недействительным.
        foreach (var dependent in Steps.Where(item =>
                     string.Equals(item.ParentStepId, step.Id, StringComparison.Ordinal)))
        {
            SetSelection(dependent, draft, null);
        }
    }

    /// <inheritdoc />
    public async Task<int?> GetSelectionLimitAsync(
        CharacterStepDefinition step,
        CharacterDraft draft,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(step);

        if (string.IsNullOrWhiteSpace(step.SelectionLimitFormula))
        {
            return null;
        }

        var context = await CreateContextAsync(draft, cancellationToken).ConfigureAwait(false);

        return EvaluateSelectionLimit(step, context);
    }

    /// <inheritdoc />
    public async Task<CharacterCalculation> CalculateAsync(
        CharacterDraft draft,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(draft);

        var input = await CreateInputAsync(draft, cancellationToken).ConfigureAwait(false);

        input = input with
        {
            RuleSets =
            [
                new RuleApplication(
                    RuleTriggers.CharacterRecalculated,
                    await _ruleService
                        .GetByTriggerAsync(RuleTriggers.CharacterRecalculated, cancellationToken)
                        .ConfigureAwait(false)),
            ],
        };

        return _calculator.Calculate(input);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ApplyEventAsync(
        CharacterDraft draft,
        string trigger,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(draft);
        Guard.NotNullOrWhiteSpace(trigger);

        var rules = await _ruleService.GetByTriggerAsync(trigger, cancellationToken).ConfigureAwait(false);

        return await ApplyRulesAsync(draft, trigger, rules, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ApplyRulesAsync(
        CharacterDraft draft,
        string trigger,
        IReadOnlyList<RuleDefinition> rules,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(draft);
        Guard.NotNullOrWhiteSpace(trigger);
        Guard.NotNull(rules);

        if (rules.Count == 0)
        {
            return [];
        }

        var input = await CreateInputAsync(draft, cancellationToken).ConfigureAwait(false);
        var result = _calculator.ApplyToBaseValues(input, new RuleApplication(trigger, rules));

        foreach (var pair in result.BaseValues)
        {
            draft.AttributeBaseValues[pair.Key] = pair.Value;
        }

        return result.AppliedRules;
    }

    /// <inheritdoc />
    public CharacterDraft CreateDraft(Character character)
    {
        Guard.NotNull(character);

        var draft = new CharacterDraft(character);

        foreach (var step in Steps)
        {
            if (step.ReadSelection?.Invoke(character) is { } single)
            {
                draft.Selections[step.Id] = single;
            }

            if (step.ReadSelections is not null)
            {
                var selected = draft.GetSelections(step.Id);

                foreach (var id in step.ReadSelections(character))
                {
                    selected.Add(id);
                }
            }
        }

        foreach (var value in character.Attributes)
        {
            draft.AttributeBaseValues[value.AttributeId] = value.BaseValue;

            if (value.OverrideValue is { } overrideValue)
            {
                draft.AttributeOverrides[value.AttributeId] = overrideValue;
            }
        }

        return draft;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CharacterIssue>> ValidateAsync(
        CharacterDraft draft,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(draft);

        var issues = new List<CharacterIssue>();

        ValidateRequiredFields(draft, issues);
        await ValidateGameSystemAsync(draft, issues, cancellationToken).ConfigureAwait(false);

        var attributes = await GetAttributesAsync(draft, cancellationToken).ConfigureAwait(false);
        ValidateAttributeRanges(draft, attributes, issues);

        // Источник значений создаётся один раз: он требует полного пересчёта,
        // а проверка обращается к нему для каждого шага.
        var context = await CreateContextAsync(draft, cancellationToken).ConfigureAwait(false);
        await ValidateSelectionsAsync(draft, context, issues, cancellationToken).ConfigureAwait(false);

        var calculation = await CalculateAsync(draft, cancellationToken).ConfigureAwait(false);
        issues.AddRange(calculation.Issues);

        return issues;
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(
        CharacterDraft draft,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(draft);

        var issues = await ValidateAsync(draft, cancellationToken).ConfigureAwait(false);
        var blocking = issues.Where(issue => issue.Severity == CharacterIssueSeverity.Error).ToList();

        if (blocking.Count > 0)
        {
            return Result.Failure<Guid>(
                string.Join(Environment.NewLine, blocking.Select(issue => issue.Message)));
        }

        var character = draft.Character;

        ApplyMultipleSelections(draft);

        // Правила создания персонажа изменяют его навсегда, поэтому применяются
        // к базовым значениям, а не поверх результата пересчёта.
        await ApplyEventAsync(draft, RuleTriggers.CharacterCreated, cancellationToken)
            .ConfigureAwait(false);

        var calculation = await CalculateAsync(draft, cancellationToken).ConfigureAwait(false);

        CharacterWriter.ApplyCalculation(character, calculation);

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            context.Characters.Add(character);
            context.History.Add(new HistoryEntry
            {
                CharacterId = character.Id,
                Action = HistoryActions.CharacterCreated,
                Description = $"Создан персонаж «{character.Name}».",
                NewValue = $"Уровень {character.Level.ToString(CultureInfo.CurrentCulture)}",
            });

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            CharacterLog.CharacterCreated(_logger, character.Name, character.Level);

            await _eventBus
                .PublishAsync(
                    new CharacterChangedEvent(character.Id, CharacterChangeKind.Created),
                    cancellationToken)
                .ConfigureAwait(false);

            return Result.Success(character.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CharacterLog.CharacterCreateFailed(_logger, exception, character.Name);

            return Result.Failure<Guid>($"Не удалось создать персонажа: {exception.Message}");
        }
    }

    /// <summary>
    /// Собирает исходные данные расчёта: характеристики, навыки, ресурсы,
    /// значения переменных и признаки персонажа.
    /// </summary>
    /// <param name="draft">Персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Исходные данные расчёта без наборов правил.</returns>
    private async Task<CharacterCalculationInput> CreateInputAsync(
        CharacterDraft draft,
        CancellationToken cancellationToken)
    {
        var attributes = await LoadAttributesAsync(
            draft,
            includeHidden: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var resources = await GetResourcesAsync(draft, cancellationToken).ConfigureAwait(false);
        var skills = await LoadSelectedEntitiesAsync<Skill>(draft, cancellationToken).ConfigureAwait(false);
        var selection = await LoadSelectionNamesAsync(draft, cancellationToken).ConfigureAwait(false);
        var bonuses = await LoadBonusesAsync(draft, cancellationToken).ConfigureAwait(false);

        return new CharacterCalculationInput
        {
            Attributes = attributes,
            Skills = skills,
            Resources = resources,
            DisplayName = draft.Character.Name,
            Level = draft.Level,
            BaseValues = new Dictionary<Guid, double>(draft.AttributeBaseValues),
            AttributeOverrides = new Dictionary<Guid, double>(draft.AttributeOverrides),
            SkillProficiencies = GetSkillProficiencies(draft, skills),
            TextVariables = selection.Variables,
            Tags = selection.Tags,
            Bonuses = bonuses,
        };
    }

    /// <summary>
    /// Загружает все усиления персонажа: бонусы надетых предметов и бонусы
    /// действующих эффектов.
    ///
    /// Бонус описан у своего источника, поэтому усиление от брони, кольца,
    /// благословения или проклятия попадает в расчёт одним и тем же путём.
    /// </summary>
    /// <param name="draft">Персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Бонусы предметов и эффектов.</returns>
    private async Task<IReadOnlyList<CharacterBonus>> LoadBonusesAsync(
        CharacterDraft draft,
        CancellationToken cancellationToken)
    {
        var characterId = draft.Character.Id;

        if (characterId == Guid.Empty)
        {
            return [];
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var result = await LoadEquipmentBonusesAsync(context, characterId, cancellationToken)
            .ConfigureAwait(false);

        result.AddRange(await LoadEffectBonusesAsync(context, characterId, cancellationToken)
            .ConfigureAwait(false));

        return result;
    }

    /// <summary>
    /// Загружает бонусы предметов, надетых персонажем.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Бонусы надетых предметов.</returns>
    private static async Task<List<CharacterBonus>> LoadEquipmentBonusesAsync(
        RpgDbContext context,
        Guid characterId,
        CancellationToken cancellationToken)
    {
        var equipped = await context.CharacterEquipment
            .AsNoTracking()
            .Where(record => record.CharacterId == characterId)
            .Select(record => new EquippedReference(record.InventoryItemId, record.InventoryItem!.ItemId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (equipped.Count == 0)
        {
            return [];
        }

        var itemIds = equipped.Select(record => record.ItemId).Distinct().ToList();

        var items = await context.Items
            .AsNoTracking()
            .Include(item => item.Bonuses)
            .Where(item => itemIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken)
            .ConfigureAwait(false);

        var result = new List<CharacterBonus>();

        // Обход идёт по надетым записям, а не по предметам: два одинаковых кольца
        // должны дать бонус дважды.
        foreach (var record in equipped)
        {
            if (!items.TryGetValue(record.ItemId, out var item))
            {
                continue;
            }

            foreach (var bonus in item.Bonuses.OrderBy(bonus => bonus.SortOrder))
            {
                result.Add(new CharacterBonus(
                    bonus.Id,
                    record.InventoryItemId,
                    item.Name,
                    bonus.Target,
                    bonus.Target == BonusTargetKind.Resource ? bonus.ResourceId : bonus.AttributeId,
                    bonus.Name,
                    bonus.Formula,
                    bonus.Condition));
            }
        }

        return result;
    }

    /// <summary>
    /// Загружает бонусы эффектов, действующих на персонажа.
    ///
    /// Эффекты обрабатываются от большего приоритета к меньшему, а количество
    /// наложений складывающегося эффекта передаётся множителем величины.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Бонусы действующих эффектов.</returns>
    private static async Task<List<CharacterBonus>> LoadEffectBonusesAsync(
        RpgDbContext context,
        Guid characterId,
        CancellationToken cancellationToken)
    {
        var active = await context.CharacterEffects
            .AsNoTracking()
            .Include(record => record.Effect)
                .ThenInclude(effect => effect!.Bonuses)
            .Where(record => record.CharacterId == characterId && record.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new List<CharacterBonus>();

        foreach (var record in active
                     .Where(record => record.Effect is not null)
                     .OrderByDescending(record => record.Effect!.Priority)
                     .ThenBy(record => record.Effect!.Name, StringComparer.CurrentCulture))
        {
            var effect = record.Effect!;
            var stacks = Math.Max(1, record.Stacks);

            foreach (var bonus in effect.Bonuses.OrderBy(bonus => bonus.SortOrder))
            {
                result.Add(new CharacterBonus(
                    bonus.Id,
                    record.Id,
                    effect.Name,
                    bonus.Target,
                    bonus.Target == BonusTargetKind.Resource ? bonus.ResourceId : bonus.AttributeId,
                    bonus.Name,
                    bonus.Formula,
                    bonus.Condition,
                    stacks));
            }
        }

        return result;
    }

    /// <summary>
    /// Надетый предмет: запись инвентаря и предмет, который в ней лежит.
    /// </summary>
    /// <param name="InventoryItemId">Идентификатор записи инвентаря.</param>
    /// <param name="ItemId">Идентификатор предмета.</param>
    private sealed record EquippedReference(Guid InventoryItemId, Guid ItemId);

    /// <summary>
    /// Определяет уровни владения выбранными навыками.
    /// Уже сохранённые уровни сохраняются, вновь выбранным навыкам назначается
    /// начальный уровень владения.
    /// </summary>
    /// <param name="draft">Персонаж.</param>
    /// <param name="skills">Выбранные навыки.</param>
    /// <returns>Уровни владения по идентификатору навыка.</returns>
    private static Dictionary<Guid, int> GetSkillProficiencies(
        CharacterDraft draft,
        IReadOnlyList<Skill> skills)
    {
        var stored = draft.Character.Skills.ToDictionary(skill => skill.SkillId, skill => skill.ProficiencyLevel);

        return skills.ToDictionary(
            skill => skill.Id,
            skill => stored.TryGetValue(skill.Id, out var level) && level > 0 ? level : 1);
    }

    /// <summary>
    /// Загружает ресурсы, доступные создаваемому персонажу.
    /// </summary>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список ресурсов.</returns>
    private async Task<IReadOnlyList<GameResource>> GetResourcesAsync(
        CharacterDraft draft,
        CancellationToken cancellationToken)
    {
        var loaded = await _resourceSource
            .LoadAsync(CreateQuery(draft, SupportingObjectLimit), cancellationToken)
            .ConfigureAwait(false);

        return loaded.Items.Cast<GameResource>().ToList();
    }

    /// <summary>
    /// Загружает объекты указанного вида, выбранные на любом из шагов мастера.
    /// </summary>
    /// <typeparam name="TEntity">Тип объектов.</typeparam>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список выбранных объектов.</returns>
    private async Task<IReadOnlyList<TEntity>> LoadSelectedEntitiesAsync<TEntity>(
        CharacterDraft draft,
        CancellationToken cancellationToken)
        where TEntity : ContentEntity
    {
        var result = new List<TEntity>();

        foreach (var step in Steps.Where(step => step.OptionEntityType == typeof(TEntity)))
        {
            if (!_sources.TryGetValue(step.Id, out var source))
            {
                continue;
            }

            var entities = await source
                .LoadByIdsAsync(GetSelectedIds(step, draft), cancellationToken)
                .ConfigureAwait(false);

            result.AddRange(entities.OfType<TEntity>());
        }

        return result;
    }

    /// <summary>
    /// Загружает внутренние имена выбранных объектов: они становятся значениями
    /// переменных формул и признаками персонажа.
    /// </summary>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Переменные и признаки персонажа.</returns>
    private async Task<(Dictionary<string, string> Variables, List<string> Tags)> LoadSelectionNamesAsync(
        CharacterDraft draft,
        CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tags = new List<string>();

        foreach (var step in Steps)
        {
            if (!_sources.TryGetValue(step.Id, out var source))
            {
                continue;
            }

            var ids = GetSelectedIds(step, draft);

            var entities = ids.Count == 0
                ? []
                : await source.LoadByIdsAsync(ids, cancellationToken).ConfigureAwait(false);

            foreach (var entity in entities)
            {
                tags.Add(entity.SystemName);
            }

            // Переменная объявляется всегда: требование «раса = "эльф"» должно
            // вычисляться и тогда, когда раса ещё не выбрана.
            if (step.VariableName is { } variable)
            {
                variables[variable] = entities.Count > 0 ? entities[0].SystemName : string.Empty;
            }
        }

        return (variables, tags);
    }

    /// <inheritdoc />
    public async Task<IRuleTarget> CreateContextAsync(
        CharacterDraft draft,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(draft);

        var calculation = await CalculateAsync(draft, cancellationToken).ConfigureAwait(false);
        var selection = await LoadSelectionNamesAsync(draft, cancellationToken).ConfigureAwait(false);

        var target = new RuleTarget(
            string.IsNullOrWhiteSpace(draft.Character.Name) ? "Персонаж" : draft.Character.Name);

        target.WithVariable(CharacterVariables.Level, draft.Level);

        foreach (var attribute in calculation.Attributes)
        {
            target.WithVariable(attribute.SystemName, attribute.Value);
        }

        foreach (var pair in selection.Variables)
        {
            target.SetVariable(pair.Key, FormulaValue.FromText(pair.Value));
        }

        foreach (var tag in selection.Tags)
        {
            target.AddTag(tag);
        }

        return target;
    }

    /// <summary>
    /// Загружает названия объектов, требуемых для выбора вариантов страницы.
    /// </summary>
    /// <param name="step">Описание шага.</param>
    /// <param name="source">Источник объектов шага.</param>
    /// <param name="entities">Загруженные варианты.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Названия требуемых объектов по идентификатору.</returns>
    private static async Task<IReadOnlyDictionary<Guid, string>> LoadRequiredNamesAsync(
        CharacterStepDefinition step,
        IContentOptionSource source,
        IReadOnlyList<ContentEntity> entities,
        CancellationToken cancellationToken)
    {
        if (step.ReadRequiredOption is null)
        {
            return new Dictionary<Guid, string>();
        }

        var required = entities
            .Select(entity => step.ReadRequiredOption(entity))
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (required.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var loaded = await source.LoadByIdsAsync(required, cancellationToken).ConfigureAwait(false);

        return loaded.ToDictionary(entity => entity.Id, entity => entity.Name);
    }

    /// <summary>
    /// Создаёт вариант выбора вместе с результатом проверки его требований.
    /// </summary>
    /// <param name="step">Описание шага.</param>
    /// <param name="entity">Игровой объект.</param>
    /// <param name="context">Источник значений переменных.</param>
    /// <param name="requiredNames">Названия требуемых объектов.</param>
    /// <param name="selected">Уже выбранные объекты шага.</param>
    /// <returns>Вариант выбора.</returns>
    private CharacterOption CreateOption(
        CharacterStepDefinition step,
        ContentEntity entity,
        IFormulaContext context,
        IReadOnlyDictionary<Guid, string> requiredNames,
        IReadOnlyCollection<Guid> selected)
    {
        var reason = GetUnavailableReason(step, entity, context, requiredNames, selected);

        return new CharacterOption(
            entity.Id,
            entity.Name,
            entity.Description,
            reason is null,
            reason,
            step.ReadDetails?.Invoke(entity) ?? [],
            entity.Image);
    }

    /// <summary>
    /// Проверяет требования объекта и возвращает причину, по которой он недоступен.
    /// </summary>
    /// <param name="step">Описание шага.</param>
    /// <param name="entity">Игровой объект.</param>
    /// <param name="context">Источник значений переменных.</param>
    /// <param name="requiredNames">Названия требуемых объектов.</param>
    /// <param name="selected">Уже выбранные объекты шага.</param>
    /// <returns>Причина недоступности либо <see langword="null"/>.</returns>
    private string? GetUnavailableReason(
        CharacterStepDefinition step,
        ContentEntity entity,
        IFormulaContext context,
        IReadOnlyDictionary<Guid, string> requiredNames,
        IReadOnlyCollection<Guid> selected)
    {
        if (step.ReadRequiredOption?.Invoke(entity) is { } requiredId && !selected.Contains(requiredId))
        {
            return requiredNames.TryGetValue(requiredId, out var requiredName)
                ? $"Сначала требуется выбрать «{requiredName}»."
                : "Требуется другой объект, который ещё не выбран.";
        }

        return CheckRequirement(step.ReadRequirements?.Invoke(entity), context);
    }

    /// <inheritdoc />
    public string? CheckRequirement(string? requirement, IFormulaContext context)
    {
        Guard.NotNull(context);

        if (string.IsNullOrWhiteSpace(requirement))
        {
            return null;
        }

        var result = _formulas.Evaluate(requirement, context);

        if (result.IsFailure)
        {
            return $"Требование «{requirement}» не удалось проверить: {result.Error}";
        }

        return result.Value.AsBoolean() ? null : $"Требование не выполнено: {requirement}";
    }

    /// <summary>
    /// Проверяет заполнение обязательных полей шагов формы.
    /// </summary>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="issues">Список замечаний.</param>
    private void ValidateRequiredFields(CharacterDraft draft, List<CharacterIssue> issues)
    {
        foreach (var step in Steps.Where(step => step.Kind == CharacterStepKind.Fields))
        {
            foreach (var field in step.Fields.Where(field => field.IsRequired))
            {
                if (string.IsNullOrWhiteSpace(field.GetText(draft.Character)))
                {
                    issues.Add(new CharacterIssue(
                        CharacterIssueSeverity.Error,
                        step.Id,
                        $"Не заполнено поле «{field.DisplayName}»."));
                }
            }
        }

        if (draft.Level < 1)
        {
            issues.Add(new CharacterIssue(
                CharacterIssueSeverity.Error,
                CharacterStepIds.Basics,
                "Уровень персонажа должен быть не меньше единицы."));
        }
    }

    /// <summary>
    /// Проверяет, что игровая система выбрана, если есть из чего выбирать.
    ///
    /// Без выбранной системы весь её контент — расы, классы и остальное —
    /// отфильтровывается по несовпадающему идентификатору и молча пропадает
    /// из списков мастера, без всякого объяснения причины. Но если в базе нет
    /// ни одной установленной системы (например, весь контент — самодельный,
    /// без расширений), выбирать попросту не из чего, и требовать выбор не нужно:
    /// весь контент в этом случае не привязан ни к одной системе и виден всегда.
    /// </summary>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="issues">Список замечаний.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    private async Task ValidateGameSystemAsync(
        CharacterDraft draft,
        List<CharacterIssue> issues,
        CancellationToken cancellationToken)
    {
        if (draft.GameSystemId is not null)
        {
            return;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var hasGameSystems = await context.GameSystems
            .AsNoTracking()
            .AnyAsync(system => system.Enabled, cancellationToken)
            .ConfigureAwait(false);

        if (hasGameSystems)
        {
            issues.Add(new CharacterIssue(
                CharacterIssueSeverity.Error,
                CharacterStepIds.GameSystem,
                "Не выбрана игровая система."));
        }
    }

    /// <summary>
    /// Проверяет соблюдение границ значений характеристик.
    /// </summary>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="attributes">Характеристики игровой системы.</param>
    /// <param name="issues">Список замечаний.</param>
    private static void ValidateAttributeRanges(
        CharacterDraft draft,
        IReadOnlyList<AttributeDefinition> attributes,
        List<CharacterIssue> issues)
    {
        // Вычисляемые характеристики пользователь не задаёт, поэтому их границы
        // проверяются не здесь, а при вычислении формулы.
        foreach (var attribute in attributes.Where(item => string.IsNullOrWhiteSpace(item.Formula)))
        {
            var value = draft.AttributeBaseValues.TryGetValue(attribute.Id, out var stored)
                ? stored
                : attribute.DefaultValue;

            if (attribute.MinimumValue is { } minimum && value < minimum)
            {
                issues.Add(new CharacterIssue(
                    CharacterIssueSeverity.Error,
                    CharacterStepIds.Attributes,
                    $"Характеристика «{attribute.Name}»: значение {Format(value)} "
                    + $"меньше допустимого {Format(minimum)}."));
            }

            if (attribute.MaximumValue is { } maximum && value > maximum)
            {
                issues.Add(new CharacterIssue(
                    CharacterIssueSeverity.Error,
                    CharacterStepIds.Attributes,
                    $"Характеристика «{attribute.Name}»: значение {Format(value)} "
                    + $"больше допустимого {Format(maximum)}."));
            }
        }
    }

    /// <summary>
    /// Проверяет сделанный выбор: обязательность шага, разрешённое количество
    /// объектов и требования каждого выбранного объекта.
    /// </summary>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="context">Источник значений переменных.</param>
    /// <param name="issues">Список замечаний.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после проверки.</returns>
    private async Task ValidateSelectionsAsync(
        CharacterDraft draft,
        IFormulaContext context,
        List<CharacterIssue> issues,
        CancellationToken cancellationToken)
    {
        foreach (var step in Steps)
        {
            if (!_sources.TryGetValue(step.Id, out var source))
            {
                continue;
            }

            var selected = GetSelectedIds(step, draft);

            if (step.IsRequired && selected.Count == 0)
            {
                issues.Add(new CharacterIssue(
                    CharacterIssueSeverity.Error,
                    step.Id,
                    $"Не сделан выбор на шаге «{step.Title}»."));
            }

            if (EvaluateSelectionLimit(step, context) is { } limit && selected.Count > limit)
            {
                issues.Add(new CharacterIssue(
                    CharacterIssueSeverity.Error,
                    step.Id,
                    $"На шаге «{step.Title}» выбрано {selected.Count.ToString(CultureInfo.CurrentCulture)} "
                    + $"из {limit.ToString(CultureInfo.CurrentCulture)} допустимых."));
            }

            if (selected.Count == 0)
            {
                continue;
            }

            var entities = await source.LoadByIdsAsync(selected, cancellationToken).ConfigureAwait(false);
            var requiredNames = await LoadRequiredNamesAsync(step, source, entities, cancellationToken)
                .ConfigureAwait(false);

            foreach (var entity in entities)
            {
                var reason = GetUnavailableReason(step, entity, context, requiredNames, selected);

                if (reason is not null)
                {
                    issues.Add(new CharacterIssue(
                        CharacterIssueSeverity.Error,
                        step.Id,
                        $"«{entity.Name}» больше не подходит персонажу. {reason}"));
                }
            }
        }
    }

    /// <summary>
    /// Вычисляет ограничение количества выборов шага.
    /// </summary>
    /// <param name="step">Описание шага.</param>
    /// <param name="context">Источник значений переменных.</param>
    /// <returns>Ограничение либо <see langword="null"/>, если оно не задано.</returns>
    private int? EvaluateSelectionLimit(CharacterStepDefinition step, IFormulaContext context)
    {
        if (string.IsNullOrWhiteSpace(step.SelectionLimitFormula))
        {
            return null;
        }

        var result = _formulas.Evaluate(step.SelectionLimitFormula, context);

        return result.IsSuccess ? (int)Math.Floor(result.Value.AsNumber()) : null;
    }

    /// <summary>
    /// Переносит множественный выбор мастера в персонажа.
    /// </summary>
    /// <param name="draft">Создаваемый персонаж.</param>
    private void ApplyMultipleSelections(CharacterDraft draft)
    {
        foreach (var step in Steps.Where(step => step.Kind == CharacterStepKind.MultipleChoice))
        {
            step.WriteSelections?.Invoke(draft.Character, draft.GetSelections(step.Id));
        }
    }

    /// <summary>
    /// Возвращает объекты, выбранные на шаге, независимо от вида выбора.
    /// </summary>
    /// <param name="step">Описание шага.</param>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <returns>Идентификаторы выбранных объектов.</returns>
    private static List<Guid> GetSelectedIds(CharacterStepDefinition step, CharacterDraft draft)
    {
        if (step.Kind == CharacterStepKind.MultipleChoice)
        {
            return [.. draft.GetSelections(step.Id)];
        }

        return draft.GetSelection(step.Id) is { } single ? [single] : [];
    }

    /// <summary>
    /// Создаёт условия отбора контента по выбранной игровой системе и источникам.
    /// </summary>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="limit">Наибольшее количество загружаемых объектов.</param>
    /// <returns>Условия отбора.</returns>
    private static ContentOptionQuery CreateQuery(CharacterDraft draft, int limit) => new(
        draft.GameSystemId,
        draft.UseAllSources,
        draft.EnabledSourceIds,
        null,
        null,
        null,
        limit);

    private static string Format(double value) =>
        value.ToString("0.####", CultureInfo.CurrentCulture);
}

/// <summary>
/// Сообщения журнала подсистемы персонажей.
/// </summary>
internal static partial class CharacterLog
{
    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Information,
        Message = "Создан персонаж «{Name}», уровень {Level}.")]
    public static partial void CharacterCreated(ILogger logger, string name, int level);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Error,
        Message = "Не удалось создать персонажа «{Name}».")]
    public static partial void CharacterCreateFailed(ILogger logger, Exception exception, string name);

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Information,
        Message = "Персонаж «{Name}»: уровень {PreviousLevel} → {CurrentLevel}.")]
    public static partial void CharacterLevelChanged(
        ILogger logger,
        string name,
        int previousLevel,
        int currentLevel);

    [LoggerMessage(
        EventId = 6004,
        Level = LogLevel.Information,
        Message = "Пересчитан персонаж «{Name}».")]
    public static partial void CharacterRecalculated(ILogger logger, string name);

    [LoggerMessage(
        EventId = 6005,
        Level = LogLevel.Error,
        Message = "Не удалось обновить персонажа {CharacterId}.")]
    public static partial void CharacterUpdateFailed(ILogger logger, Exception exception, Guid characterId);

    [LoggerMessage(
        EventId = 6006,
        Level = LogLevel.Information,
        Message = "Удалён персонаж «{Name}».")]
    public static partial void CharacterDeleted(ILogger logger, string name);

    [LoggerMessage(
        EventId = 6007,
        Level = LogLevel.Information,
        Message = "Сохранён лист персонажа «{Name}».")]
    public static partial void CharacterSheetSaved(ILogger logger, string name);

    [LoggerMessage(
        EventId = 6008,
        Level = LogLevel.Information,
        Message = "Персонаж «{CharacterName}» выучил заклинание «{SpellName}».")]
    public static partial void SpellLearned(ILogger logger, string characterName, string spellName);

    [LoggerMessage(
        EventId = 6009,
        Level = LogLevel.Information,
        Message = "Персонаж «{CharacterName}» применил «{SpellName}» на уровне {CastLevel}.")]
    public static partial void SpellCast(
        ILogger logger,
        string characterName,
        string spellName,
        int castLevel);

    [LoggerMessage(
        EventId = 6010,
        Level = LogLevel.Error,
        Message = "Не удалось выполнить действие с книгой заклинаний персонажа {CharacterId}.")]
    public static partial void SpellbookOperationFailed(
        ILogger logger,
        Exception exception,
        Guid characterId);

    [LoggerMessage(
        EventId = 6011,
        Level = LogLevel.Information,
        Message = "На персонажа «{CharacterName}» наложен эффект «{EffectName}».")]
    public static partial void EffectApplied(ILogger logger, string characterName, string effectName);

    [LoggerMessage(
        EventId = 6012,
        Level = LogLevel.Information,
        Message = "У персонажа «{CharacterName}» закончилось эффектов: {Count}.")]
    public static partial void EffectsExpired(ILogger logger, string characterName, int count);

    [LoggerMessage(
        EventId = 6013,
        Level = LogLevel.Error,
        Message = "Не удалось выполнить действие с эффектами персонажа {CharacterId}.")]
    public static partial void EffectOperationFailed(
        ILogger logger,
        Exception exception,
        Guid characterId);

    [LoggerMessage(
        EventId = 6014,
        Level = LogLevel.Information,
        Message = "Персонаж «{CharacterName}» отдохнул: «{RestName}».")]
    public static partial void CharacterRested(ILogger logger, string characterName, string restName);

    [LoggerMessage(
        EventId = 6015,
        Level = LogLevel.Error,
        Message = "Не удалось выполнить отдых персонажа {CharacterId}.")]
    public static partial void RestOperationFailed(
        ILogger logger,
        Exception exception,
        Guid characterId);
}
