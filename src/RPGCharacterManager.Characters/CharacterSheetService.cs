using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Characters;

/// <summary>
/// Лист персонажа: чтение вычисленных параметров и сохранение изменений.
///
/// Пользователь изменяет только исходные значения — базовые значения характеристик,
/// уровни владения навыками, состояние черт и описание персонажа. Все производные
/// величины вычисляются формулами и правилами при каждом сохранении, поэтому лист
/// не может разойтись с игровой системой.
/// </summary>
public sealed class CharacterSheetService : ICharacterSheetService
{
    /// <summary>Вид объектов, к которому относятся пользовательские поля персонажей.</summary>
    public const string CustomFieldTargetType = "characters";

    /// <summary>Количество черт, загружаемых в список выбора за один раз.</summary>
    public const int TraitPageSize = 200;

    /// <summary>Причина изменения ресурса, вписанного пользователем на листе.</summary>
    public const string ManualChangeReason = "изменено на листе";

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly ICharacterBuilderService _builder;
    private readonly ICustomPropertyService _customProperties;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CharacterSheetService> _logger;

    /// <summary>
    /// Создаёт службу листа персонажа.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="builder">Мастер создания персонажа, выполняющий расчёт и проверку требований.</param>
    /// <param name="customProperties">Служба пользовательских свойств.</param>
    /// <param name="eventBus">Шина событий приложения.</param>
    /// <param name="logger">Журналировщик.</param>
    public CharacterSheetService(
        IDbContextFactory<RpgDbContext> contextFactory,
        ICharacterBuilderService builder,
        ICustomPropertyService customProperties,
        IEventBus eventBus,
        ILogger<CharacterSheetService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _builder = Guard.NotNull(builder);
        _customProperties = Guard.NotNull(customProperties);
        _eventBus = Guard.NotNull(eventBus);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<CharacterSheet>> LoadAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var character = await CharacterService
            .LoadWithRelatedData(context.Characters)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == characterId, cancellationToken)
            .ConfigureAwait(false);

        if (character is null)
        {
            return Result.Failure<CharacterSheet>("Персонаж не найден: возможно, он был удалён.");
        }

        return Result.Success(
            await BuildSheetAsync(character, calculation: null, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<Result<CharacterSheet>> SaveAsync(
        Character character,
        IReadOnlyDictionary<Guid, string?> customFieldValues,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(character);
        Guard.NotNull(customFieldValues);

        if (string.IsNullOrWhiteSpace(character.Name))
        {
            return Result.Failure<CharacterSheet>("Не задано имя персонажа.");
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var stored = await CharacterService
                .LoadWithRelatedData(context.Characters)
                .FirstOrDefaultAsync(item => item.Id == character.Id, cancellationToken)
                .ConfigureAwait(false);

            if (stored is null)
            {
                return Result.Failure<CharacterSheet>("Персонаж не найден: возможно, он был удалён.");
            }

            // Значения переносятся на отслеживаемую запись, а не подключается
            // отсоединённый граф: так добавленные на листе черты сохраняются
            // как новые записи, а не как изменённые (см. решение Р-18).
            var added = new List<object>();

            // Значения ресурсов запоминаются до правки и сравниваются после
            // пересчёта: пересчёт ограничивает текущее значение новым максимумом,
            // и в журнал должно попасть то значение, которое сохранено.
            var resourcesBefore = HistoryEntries.SnapshotResources(stored);

            CharacterSheetWriter.Apply(character, stored, added);

            var draft = _builder.CreateDraft(stored);
            var calculation = await _builder.CalculateAsync(draft, cancellationToken).ConfigureAwait(false);

            CharacterWriter.ApplyCalculation(stored, calculation, added);

            // Названия ресурсов берутся из расчёта: запрос персонажа их описания
            // не загружает, а в журнале должно стоять «Здоровье», а не «Ресурс».
            added.AddRange(HistoryEntries.ResourceChanges(
                stored,
                resourcesBefore,
                ManualChangeReason,
                calculation.Resources.ToDictionary(resource => resource.Id, resource => resource.Name)));

            context.AddRange(added);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _customProperties
                .SaveValuesAsync(stored.Id, customFieldValues, cancellationToken)
                .ConfigureAwait(false);

            CharacterLog.CharacterSheetSaved(_logger, stored.Name);

            await _eventBus
                .PublishAsync(
                    new CharacterChangedEvent(stored.Id, CharacterChangeKind.Recalculated),
                    cancellationToken)
                .ConfigureAwait(false);

            return Result.Success(
                await BuildSheetAsync(stored, calculation, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CharacterLog.CharacterUpdateFailed(_logger, exception, character.Id);

            return Result.Failure<CharacterSheet>($"Не удалось сохранить лист: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public Task<CharacterOptionPage> GetAvailableTraitsAsync(
        Character character,
        string? search,
        bool includeUnavailable,
        CancellationToken cancellationToken = default) =>
        GetAvailableAsync<Trait>(character, search, includeUnavailable, cancellationToken);

    /// <inheritdoc />
    public Task<CharacterOptionPage> GetAvailableSkillsAsync(
        Character character,
        string? search,
        bool includeUnavailable,
        CancellationToken cancellationToken = default) =>
        GetAvailableAsync<Skill>(character, search, includeUnavailable, cancellationToken);

    /// <summary>
    /// Возвращает объекты указанного вида, которые персонаж может получить.
    ///
    /// Список берётся у того же шага мастера, что и при создании персонажа,
    /// поэтому отбор по игровой системе и проверка требований выполняются
    /// ровно так же и описаны в одном месте.
    /// </summary>
    /// <typeparam name="TEntity">Тип игровых объектов.</typeparam>
    /// <param name="character">Персонаж.</param>
    /// <param name="search">Строка поиска по названию.</param>
    /// <param name="includeUnavailable">Показывать объекты с невыполненными требованиями.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Доступные объекты.</returns>
    private Task<CharacterOptionPage> GetAvailableAsync<TEntity>(
        Character character,
        string? search,
        bool includeUnavailable,
        CancellationToken cancellationToken)
        where TEntity : ContentEntity
    {
        Guard.NotNull(character);

        var step = _builder.Steps.FirstOrDefault(item => item.OptionEntityType == typeof(TEntity));

        if (step is null)
        {
            return Task.FromResult(new CharacterOptionPage([], 0));
        }

        return _builder.GetOptionsAsync(
            step,
            _builder.CreateDraft(character),
            search,
            includeUnavailable,
            TraitPageSize,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> SaveCustomFieldAsync(
        PropertyDefinition definition,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(definition);

        definition.TargetType = CustomFieldTargetType;

        return await _customProperties
            .SaveDefinitionAsync(definition, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteCustomFieldAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _customProperties
            .DeleteDefinitionAsync(definitionId, cancellationToken)
            .ConfigureAwait(false);

        return deleted
            ? Result.Success()
            : Result.Failure("Поле не найдено: возможно, оно уже удалено.");
    }

    /// <inheritdoc />
    public async Task<Result> SaveCustomAbilityAsync(Guid characterId, CharacterCustomAbility ability,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(ability);
        if (string.IsNullOrWhiteSpace(ability.Name))
        {
            return Result.Failure("Введите название авторской способности.");
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (!await context.Characters.AnyAsync(item => item.Id == characterId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure("Персонаж не найден: возможно, он был удалён.");
        }

        ability.CharacterId = characterId;
        ability.Name = ability.Name.Trim();
        ability.Description = NullIfWhiteSpace(ability.Description);
        ability.Category = NullIfWhiteSpace(ability.Category) ?? "Авторские способности";
        ability.Formula = NullIfWhiteSpace(ability.Formula);
        ability.Requirements = NullIfWhiteSpace(ability.Requirements);
        ability.DependencyDescription = NullIfWhiteSpace(ability.DependencyDescription);
        context.CharacterCustomAbilities.Add(ability);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _eventBus.PublishAsync(
            new CharacterChangedEvent(characterId, CharacterChangeKind.Recalculated), cancellationToken)
            .ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteCustomAbilityAsync(Guid characterId, Guid abilityId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var ability = await context.CharacterCustomAbilities
            .FirstOrDefaultAsync(item => item.Id == abilityId && item.CharacterId == characterId,
                cancellationToken)
            .ConfigureAwait(false);
        if (ability is null)
        {
            return Result.Failure("Авторская способность не найдена: возможно, она уже удалена.");
        }

        context.CharacterCustomAbilities.Remove(ability);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _eventBus.PublishAsync(
            new CharacterChangedEvent(characterId, CharacterChangeKind.Recalculated), cancellationToken)
            .ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> SaveCurrencyAsync(Guid characterId, CharacterCurrency currency,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(currency);
        if (string.IsNullOrWhiteSpace(currency.Name))
        {
            return Result.Failure("Введите название валюты.");
        }

        if (currency.Amount < 0)
        {
            return Result.Failure("Количество денег не может быть отрицательным.");
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (!await context.Characters.AnyAsync(item => item.Id == characterId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure("Персонаж не найден: возможно, он был удалён.");
        }

        var stored = await context.CharacterCurrencies
            .FirstOrDefaultAsync(item => item.Id == currency.Id, cancellationToken)
            .ConfigureAwait(false);
        if (stored is null)
        {
            currency.CharacterId = characterId;
            currency.Name = currency.Name.Trim();
            context.CharacterCurrencies.Add(currency);
        }
        else
        {
            if (stored.CharacterId != characterId)
            {
                return Result.Failure("Эта валюта принадлежит другому персонажу.");
            }

            stored.Name = currency.Name.Trim();
            stored.Amount = currency.Amount;
            stored.ModifiedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteCurrencyAsync(Guid characterId, Guid currencyId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var currency = await context.CharacterCurrencies
            .FirstOrDefaultAsync(item => item.Id == currencyId && item.CharacterId == characterId,
                cancellationToken).ConfigureAwait(false);
        if (currency is null)
        {
            return Result.Failure("Валюта не найдена: возможно, она уже удалена.");
        }

        context.CharacterCurrencies.Remove(currency);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> SaveManaAsync(Guid characterId, decimal current, decimal? maximum,
        CancellationToken cancellationToken = default)
    {
        if (current < 0 || maximum < 0)
        {
            return Result.Failure("Значение маны не может быть отрицательным.");
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var character = await context.Characters
            .FirstOrDefaultAsync(item => item.Id == characterId, cancellationToken)
            .ConfigureAwait(false);
        if (character is null)
        {
            return Result.Failure("Персонаж не найден: возможно, он был удалён.");
        }

        character.Mana = current;
        character.ManaMaximum = maximum;
        character.ModifiedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>
    /// Собирает лист персонажа: вычисляет параметры и дополняет их сведениями
    /// об объектах, на которые персонаж ссылается.
    /// </summary>
    /// <param name="character">Персонаж со связанными данными.</param>
    /// <param name="calculation">Готовый результат расчёта либо <see langword="null"/>.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Лист персонажа.</returns>
    private async Task<CharacterSheet> BuildSheetAsync(
        Character character,
        CharacterCalculation? calculation,
        CancellationToken cancellationToken)
    {
        var draft = _builder.CreateDraft(character);

        calculation ??= await _builder.CalculateAsync(draft, cancellationToken).ConfigureAwait(false);

        // Персонаж дополняется записями для характеристик и ресурсов, появившихся
        // в игровой системе после его создания: лист обязан показать их сразу,
        // не дожидаясь первого сохранения.
        CharacterWriter.ApplyCalculation(character, calculation);

        var context = await _builder.CreateContextAsync(draft, cancellationToken).ConfigureAwait(false);

        var definitions = await LoadDefinitionsAsync(character, cancellationToken).ConfigureAwait(false);

        var attributes = BuildAttributes(calculation, definitions.Attributes);
        var skills = BuildSkills(character, calculation, definitions);
        var resources = BuildResources(character, calculation, definitions.Resources);
        var traits = BuildTraits(character, definitions.Traits, context);
        var abilities = BuildAbilities(character, definitions.Abilities, definitions.Resources, context);

        var customFields = await LoadCustomFieldsAsync(character.Id, cancellationToken).ConfigureAwait(false);

        return new CharacterSheet(
            character,
            attributes,
            skills,
            resources,
            traits,
            abilities,
            customFields,
            calculation.Issues);
    }

    /// <summary>
    /// Загружает описания игровых объектов, на которые ссылается персонаж,
    /// а также способности, доступные его игровой системе.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Описания игровых объектов.</returns>
    private async Task<SheetDefinitions> LoadDefinitionsAsync(
        Character character,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var systemId = character.GameSystemId;

        var attributes = await context.Attributes
            .AsNoTracking()
            .Where(item => item.GameSystemId == null || item.GameSystemId == systemId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var skillIds = character.Skills.Select(skill => skill.SkillId).ToList();

        var skills = await context.Skills
            .AsNoTracking()
            .Include(skill => skill.LinkedAttribute)
            .Where(skill => skillIds.Contains(skill.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var resources = await context.Resources
            .AsNoTracking()
            .Where(item => item.GameSystemId == null || item.GameSystemId == systemId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var traitIds = character.Traits.Select(trait => trait.TraitId).ToList();

        var traits = await context.Traits
            .AsNoTracking()
            .Where(trait => traitIds.Contains(trait.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var abilities = await context.Abilities
            .AsNoTracking()
            .Where(item => item.GameSystemId == null || item.GameSystemId == systemId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new SheetDefinitions(attributes, skills, resources, traits, abilities);
    }

    private static List<SheetAttributeValue> BuildAttributes(
        CharacterCalculation calculation,
        IReadOnlyList<AttributeDefinition> definitions)
    {
        var byId = definitions.ToDictionary(definition => definition.Id);

        return calculation.Attributes
            .Select(attribute =>
            {
                byId.TryGetValue(attribute.Id, out var definition);

                return new SheetAttributeValue(
                    attribute.Id,
                    attribute.Name,
                    attribute.SystemName,
                    Category(definition?.Category),
                    attribute.BaseValue,
                    attribute.Value,
                    attribute.Modifier,
                    attribute.IsDerived,
                    definition?.IsHidden ?? false,
                    definition?.Formula,
                    definition?.MinimumValue,
                    definition?.MaximumValue);
            })
            .ToList();
    }

    private static List<SheetSkill> BuildSkills(
        Character character,
        CharacterCalculation calculation,
        SheetDefinitions definitions)
    {
        var byId = definitions.Skills.ToDictionary(skill => skill.Id);
        var stored = character.Skills.ToDictionary(skill => skill.SkillId);

        return calculation.Skills
            .Select(skill =>
            {
                byId.TryGetValue(skill.Id, out var definition);
                stored.TryGetValue(skill.Id, out var value);

                return new SheetSkill(
                    skill.Id,
                    skill.Name,
                    Category(definition?.Category),
                    skill.ProficiencyLevel,
                    value?.Bonus ?? 0,
                    skill.Value,
                    definition?.LinkedAttribute?.Name,
                    definition?.Formula,
                    definition?.MaximumLevel);
            })
            .ToList();
    }

    private static List<SheetResource> BuildResources(
        Character character,
        CharacterCalculation calculation,
        IReadOnlyList<GameResource> definitions)
    {
        var byId = definitions.ToDictionary(definition => definition.Id);
        var stored = character.Resources.ToDictionary(resource => resource.ResourceId);

        return calculation.Resources
            .Select(resource =>
            {
                byId.TryGetValue(resource.Id, out var definition);

                // Текущее значение принадлежит персонажу и не пересчитывается:
                // пересчёт задаёт лишь максимум, до которого оно ограничивается.
                var current = stored.TryGetValue(resource.Id, out var value)
                    ? value.Current
                    : resource.Current;

                return new SheetResource(
                    resource.Id,
                    resource.Name,
                    Category(definition?.Category),
                    Math.Min(current, resource.Maximum),
                    resource.Maximum,
                    definition?.RestoreRule);
            })
            .ToList();
    }

    private List<SheetTrait> BuildTraits(
        Character character,
        IReadOnlyList<Trait> definitions,
        IFormulaContext context)
    {
        var byId = definitions.ToDictionary(trait => trait.Id);
        var result = new List<SheetTrait>(character.Traits.Count);

        foreach (var characterTrait in character.Traits)
        {
            if (!byId.TryGetValue(characterTrait.TraitId, out var definition))
            {
                continue;
            }

            var reason = _builder.CheckRequirement(definition.Requirements, context);

            result.Add(new SheetTrait(
                characterTrait.Id,
                definition.Id,
                definition.Name,
                definition.Description,
                Category(definition.Category),
                characterTrait.Source,
                definition.Formula,
                characterTrait.RemainingUses,
                characterTrait.IsActive,
                reason is null,
                reason));
        }

        return result;
    }

    /// <summary>
    /// Отбирает способности, требования которых выполнены персонажем.
    ///
    /// Классовые, расовые и любые иные способности не связаны с классом или расой
    /// отдельной таблицей: принадлежность выражается требованием, например
    /// <c>класс = "воин"</c>. Поэтому набор способностей всегда соответствует
    /// текущему состоянию персонажа и обновляется вместе с ним.
    /// </summary>
    /// <param name="abilities">Способности игровой системы.</param>
    /// <param name="resources">Ресурсы игровой системы.</param>
    /// <param name="context">Источник значений переменных персонажа.</param>
    /// <returns>Доступные способности.</returns>
    private List<SheetAbility> BuildAbilities(
        Character character,
        IReadOnlyList<Ability> abilities,
        IReadOnlyList<GameResource> resources,
        IFormulaContext context)
    {
        var resourceNames = resources.ToDictionary(resource => resource.Id, resource => resource.Name);
        var result = new List<SheetAbility>();
        var knownAbilityKeys = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

        foreach (var ability in abilities)
        {
            if (_builder.CheckRequirement(ability.Requirements, context) is not null)
            {
                continue;
            }

            AddAbility(result, knownAbilityKeys, ability, resourceNames);
        }

        foreach (var ability in Dnd5eClassAbilityCatalog.GetAbilities(character))
        {
            AddAbility(result, knownAbilityKeys, ability, resourceNames);
        }

        foreach (var ability in Dnd5eSubclassAbilityCatalog.GetAbilities(character))
        {
            AddAbility(result, knownAbilityKeys, ability, resourceNames);
        }

        foreach (var ability in character.CustomAbilities.OrderBy(item => item.Name))
        {
            var reason = _builder.CheckRequirement(ability.Requirements, context);
            result.Add(new SheetAbility(
                ability.Id,
                ability.Name,
                ability.Description,
                Category(ability.Category),
                ability.Formula,
                null,
                null,
                null,
                ability.Requirements,
                IsCustom: true,
                IsAvailable: reason is null,
                UnavailableReason: reason,
                DependencyDescription: ability.DependencyDescription));
        }

        return result.OrderBy(item => item.Category, StringComparer.CurrentCulture)
            .ThenBy(item => item.Name, StringComparer.CurrentCulture).ToList();
    }

    private static void AddAbility(ICollection<SheetAbility> result, ISet<string> knownAbilityKeys,
        Ability ability, IReadOnlyDictionary<Guid, string> resourceNames)
    {
        var key = $"{Category(ability.Category)}\u001f{ability.Name}";
        if (!knownAbilityKeys.Add(key))
        {
            return;
        }

        result.Add(new SheetAbility(
            ability.Id, ability.Name, ability.Description, Category(ability.Category), ability.Formula,
            ability.ResourceId is { } resourceId && resourceNames.TryGetValue(resourceId, out var name)
                ? name : null,
            ability.ResourceCostFormula, ability.RechargeRule, ability.Requirements));
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<List<SheetCustomField>> LoadCustomFieldsAsync(
        Guid characterId,
        CancellationToken cancellationToken)
    {
        var definitions = await _customProperties
            .GetDefinitionsAsync(CustomFieldTargetType, cancellationToken)
            .ConfigureAwait(false);

        if (definitions.Count == 0)
        {
            return [];
        }

        var values = await _customProperties
            .GetValuesAsync(characterId, cancellationToken)
            .ConfigureAwait(false);

        return definitions
            .Select(definition => new SheetCustomField(
                definition.Id,
                definition.DisplayName,
                definition.Description,
                Category(definition.Category),
                definition.DataType,
                values.TryGetValue(definition.Id, out var value) ? value : definition.DefaultValue))
            .ToList();
    }

    private static string Category(string? category) =>
        string.IsNullOrWhiteSpace(category) ? SheetCategories.Other : category;
}

/// <summary>
/// Описания игровых объектов, требуемые для построения листа персонажа.
/// </summary>
/// <param name="Attributes">Характеристики игровой системы.</param>
/// <param name="Skills">Навыки, которыми владеет персонаж.</param>
/// <param name="Resources">Ресурсы игровой системы.</param>
/// <param name="Traits">Черты, полученные персонажем.</param>
/// <param name="Abilities">Способности игровой системы.</param>
internal sealed record SheetDefinitions(
    IReadOnlyList<AttributeDefinition> Attributes,
    IReadOnlyList<Skill> Skills,
    IReadOnlyList<GameResource> Resources,
    IReadOnlyList<Trait> Traits,
    IReadOnlyList<Ability> Abilities);
