using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Characters;
using RPGCharacterManager.Content;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database;
using RPGCharacterManager.Database.Repositories;
using RPGCharacterManager.GameRules;
using RPGCharacterManager.Infrastructure.Events;
using RPGCharacterManager.Items;
using RPGCharacterManager.Tests.Rules;
using RPGCharacterManager.Tests.Support;

namespace RPGCharacterManager.Tests.Characters;

/// <summary>
/// Подсистема персонажей, собранная поверх временной базы данных.
/// Службы соединяются так же, как в приложении: настоящий движок формул,
/// настоящий движок правил и настоящее хранилище.
/// </summary>
internal sealed class CharacterTestContext : IAsyncDisposable
{
    private readonly TestDatabase _database;

    private CharacterTestContext(
        TestDatabase database,
        IFormulaEngine formulas,
        IRuleService rules,
        ICharacterCalculator calculator,
        CharacterBuilderService builder,
        CharacterService characters,
        CharacterProgressionService progression,
        CharacterSheetService sheets,
        WeaponService weapons,
        EquipmentService equipment,
        InventoryService inventory,
        SpellbookService spellbook,
        EffectService effects,
        RestService rests)
    {
        Effects = effects;
        Rests = rests;
        _database = database;
        Formulas = formulas;
        Rules = rules;
        Calculator = calculator;
        Builder = builder;
        Characters = characters;
        Progression = progression;
        Sheets = sheets;
        Weapons = weapons;
        Equipment = equipment;
        Inventory = inventory;
        Spellbook = spellbook;
    }

    /// <summary>Единый движок вычислений.</summary>
    public IFormulaEngine Formulas { get; }

    /// <summary>Хранилище игровых правил.</summary>
    public IRuleService Rules { get; }

    /// <summary>Служба расчёта параметров персонажа.</summary>
    public ICharacterCalculator Calculator { get; }

    /// <summary>Мастер создания персонажа.</summary>
    public CharacterBuilderService Builder { get; }

    /// <summary>Служба персонажей.</summary>
    public CharacterService Characters { get; }

    /// <summary>Служба развития персонажа.</summary>
    public CharacterProgressionService Progression { get; }

    /// <summary>Служба листа персонажа.</summary>
    public CharacterSheetService Sheets { get; }

    /// <summary>Служба оружия персонажа.</summary>
    public WeaponService Weapons { get; }

    /// <summary>Служба экипировки персонажа.</summary>
    public EquipmentService Equipment { get; }

    /// <summary>Служба инвентаря персонажа.</summary>
    public InventoryService Inventory { get; }

    /// <summary>Служба книги заклинаний персонажа.</summary>
    public SpellbookService Spellbook { get; }

    /// <summary>Служба эффектов персонажа.</summary>
    public EffectService Effects { get; }

    /// <summary>Служба отдыха персонажа.</summary>
    public RestService Rests { get; }

    /// <summary>
    /// Фабрика контекстов базы данных теста.
    /// Позволяет собрать поверх той же базы службу другой подсистемы.
    /// </summary>
    public IDbContextFactory<RpgDbContext> ContextFactory => _database.ContextFactory;

    /// <summary>
    /// Полный путь к файлу базы данных теста.
    /// Нужен проверкам, которым требуется собственное соединение.
    /// </summary>
    public string DatabaseFilePath => _database.Paths.DatabaseFilePath;

    /// <summary>
    /// Создаёт подсистему персонажей поверх новой временной базы данных.
    /// </summary>
    /// <param name="stepProviders">Поставщики шагов мастера. По умолчанию — стандартные шаги.</param>
    /// <returns>Готовое окружение теста.</returns>
    public static Task<CharacterTestContext> CreateAsync(
        params ICharacterStepProvider[] stepProviders) =>
        CreateCoreAsync(RuleTestFactory.DefaultDiceValue, stepProviders);

    /// <summary>
    /// Создаёт подсистему персонажей с заданным исходом броска кубиков.
    /// Позволяет проверять попадание, критическое попадание и урон без случайности.
    /// </summary>
    /// <param name="diceValue">Значение, выпадающее на каждом кубике.</param>
    /// <returns>Готовое окружение теста.</returns>
    public static Task<CharacterTestContext> CreateWithDiceAsync(int diceValue) =>
        CreateCoreAsync(diceValue, []);

    private static async Task<CharacterTestContext> CreateCoreAsync(
        int diceValue,
        ICharacterStepProvider[] stepProviders)
    {
        var database = await TestDatabase.CreateAsync();

        var formulas = RuleTestFactory.CreateFormulas(diceValue);
        var ruleEngine = new RuleEngine(formulas, RuleTestFactory.CreateHandlers());
        var ruleService = new RuleService(
            new Repository<GameRule>(database.ContextFactory),
            NullLogger<RuleService>.Instance);

        var calculator = new CharacterCalculator(formulas, ruleEngine);

        var providers = stepProviders.Length > 0
            ? stepProviders
            : [new StandardCharacterStepProvider()];

        var eventBus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);

        var builder = new CharacterBuilderService(
            providers,
            database.ContextFactory,
            formulas,
            ruleService,
            calculator,
            eventBus,
            NullLogger<CharacterBuilderService>.Instance);

        var characters = new CharacterService(
            database.ContextFactory,
            eventBus,
            NullLogger<CharacterService>.Instance);

        var progression = new CharacterProgressionService(
            database.ContextFactory,
            builder,
            eventBus,
            NullLogger<CharacterProgressionService>.Instance);

        var customProperties = new CustomPropertyService(
            database.ContextFactory,
            NullLogger<CustomPropertyService>.Instance);

        var sheets = new CharacterSheetService(
            database.ContextFactory,
            builder,
            customProperties,
            eventBus,
            NullLogger<CharacterSheetService>.Instance);

        var weapons = new WeaponService(
            database.ContextFactory,
            builder,
            formulas,
            ruleService,
            ruleEngine,
            NullLogger<WeaponService>.Instance);

        var equipment = new EquipmentService(
            database.ContextFactory,
            builder,
            eventBus,
            NullLogger<EquipmentService>.Instance);

        var inventory = new InventoryService(
            database.ContextFactory,
            builder,
            formulas,
            eventBus,
            NullLogger<InventoryService>.Instance);

        var spellbook = new SpellbookService(
            database.ContextFactory,
            builder,
            formulas,
            eventBus,
            NullLogger<SpellbookService>.Instance);

        var effects = new EffectService(
            database.ContextFactory,
            builder,
            formulas,
            eventBus,
            NullLogger<EffectService>.Instance);

        var rests = new RestService(
            database.ContextFactory,
            builder,
            effects,
            formulas,
            eventBus,
            NullLogger<RestService>.Instance);

        return new CharacterTestContext(
            database,
            formulas,
            ruleService,
            calculator,
            builder,
            characters,
            progression,
            sheets,
            weapons,
            equipment,
            inventory,
            spellbook,
            effects,
            rests);
    }

    /// <summary>
    /// Находит шаг мастера по идентификатору.
    /// </summary>
    /// <param name="stepId">Идентификатор шага.</param>
    /// <returns>Описание шага.</returns>
    public CharacterStepDefinition Step(string stepId) =>
        Builder.Steps.Single(step => step.Id == stepId);

    /// <summary>
    /// Сохраняет игровые объекты в базе данных.
    /// </summary>
    /// <typeparam name="TEntity">Тип объектов.</typeparam>
    /// <param name="entities">Сохраняемые объекты.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    public async Task AddAsync<TEntity>(params TEntity[] entities)
        where TEntity : EntityBase
    {
        await using var context = await _database.ContextFactory.CreateDbContextAsync();

        context.Set<TEntity>().AddRange(entities);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Создаёт контекст базы данных теста.
    /// Позволяет проверять состояние, для которого у служб нет запроса.
    /// </summary>
    /// <returns>Контекст базы данных.</returns>
    public Task<RpgDbContext> CreateContextAsync() => _database.ContextFactory.CreateDbContextAsync();

    /// <summary>
    /// Загружает персонажа со всеми связанными данными.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <returns>Персонаж.</returns>
    public async Task<Character> LoadCharacterAsync(Guid characterId)
    {
        var character = await Characters.GetAsync(characterId);

        return character ?? throw new InvalidOperationException("Персонаж не найден.");
    }

    /// <summary>
    /// Изменяет формулу максимума ресурса, имитируя правку контента пользователем.
    /// </summary>
    /// <param name="resourceId">Идентификатор ресурса.</param>
    /// <param name="maximumFormula">Новая формула максимума.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    public async Task UpdateResourceFormulaAsync(Guid resourceId, string maximumFormula)
    {
        await using var context = await _database.ContextFactory.CreateDbContextAsync();

        var resource = await context.Resources.SingleAsync(item => item.Id == resourceId);
        resource.MaximumFormula = maximumFormula;

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Возвращает записи журнала изменений персонажа.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <returns>Записи журнала.</returns>
    public async Task<IReadOnlyList<HistoryEntry>> LoadHistoryAsync(Guid characterId)
    {
        await using var context = await _database.ContextFactory.CreateDbContextAsync();

        return context.History
            .Where(entry => entry.CharacterId == characterId)
            .ToList();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _database.DisposeAsync();
}

/// <summary>
/// Создание игровых объектов для тестов подсистемы персонажей.
/// </summary>
internal static class CharacterContent
{
    /// <summary>
    /// Создаёт характеристику.
    /// </summary>
    /// <param name="name">Название характеристики.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <param name="defaultValue">Значение по умолчанию.</param>
    /// <param name="modifierFormula">Формула модификатора.</param>
    /// <param name="formula">Формула значения для вычисляемой характеристики.</param>
    /// <param name="minimum">Наименьшее допустимое значение.</param>
    /// <param name="maximum">Наибольшее допустимое значение.</param>
    /// <param name="gameSystemId">Игровая система.</param>
    /// <returns>Характеристика.</returns>
    public static AttributeDefinition Attribute(
        string name,
        string systemName,
        double defaultValue = 10,
        string? modifierFormula = null,
        string? formula = null,
        double? minimum = null,
        double? maximum = null,
        Guid? gameSystemId = null) => new()
        {
            Name = name,
            SystemName = systemName,
            DefaultValue = defaultValue,
            ModifierFormula = modifierFormula,
            Formula = formula,
            MinimumValue = minimum,
            MaximumValue = maximum,
            GameSystemId = gameSystemId,
        };

    /// <summary>
    /// Создаёт ресурс.
    /// </summary>
    /// <param name="name">Название ресурса.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <param name="maximumFormula">Формула максимума.</param>
    /// <param name="startingFormula">Формула начального значения.</param>
    /// <returns>Ресурс.</returns>
    public static GameResource Resource(
        string name,
        string systemName,
        string maximumFormula,
        string? startingFormula = null) => new()
        {
            Name = name,
            SystemName = systemName,
            MaximumFormula = maximumFormula,
            StartingFormula = startingFormula,
        };

    /// <summary>
    /// Создаёт навык.
    /// </summary>
    /// <param name="name">Название навыка.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <param name="linkedAttributeId">Связанная характеристика.</param>
    /// <param name="formula">Формула значения навыка.</param>
    /// <param name="requirements">Требования к выбору.</param>
    /// <returns>Навык.</returns>
    public static Skill Skill(
        string name,
        string systemName,
        Guid? linkedAttributeId = null,
        string? formula = null,
        string? requirements = null) => new()
        {
            Name = name,
            SystemName = systemName,
            LinkedAttributeId = linkedAttributeId,
            Formula = formula,
            Requirements = requirements,
        };

    /// <summary>
    /// Создаёт расу.
    /// </summary>
    /// <param name="name">Название расы.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <param name="requirements">Требования к выбору.</param>
    /// <param name="gameSystemId">Игровая система.</param>
    /// <param name="contentPackId">Контент-пак.</param>
    /// <returns>Раса.</returns>
    public static Race Race(
        string name,
        string systemName,
        string? requirements = null,
        Guid? gameSystemId = null,
        Guid? contentPackId = null) => new()
        {
            Name = name,
            SystemName = systemName,
            Requirements = requirements,
            GameSystemId = gameSystemId,
            ContentPackId = contentPackId,
        };

    /// <summary>
    /// Создаёт класс персонажа.
    /// </summary>
    /// <param name="name">Название класса.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <param name="requirements">Требования к выбору.</param>
    /// <returns>Класс персонажа.</returns>
    public static CharacterClass Class(
        string name,
        string systemName,
        string? requirements = null) => new()
        {
            Name = name,
            SystemName = systemName,
            Requirements = requirements,
        };

    /// <summary>
    /// Создаёт подкласс.
    /// </summary>
    /// <param name="name">Название подкласса.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <param name="classId">Родительский класс.</param>
    /// <param name="availableAtLevel">Уровень, с которого доступен подкласс.</param>
    /// <returns>Подкласс.</returns>
    public static Subclass Subclass(
        string name,
        string systemName,
        Guid classId,
        int availableAtLevel = 1) => new()
        {
            Name = name,
            SystemName = systemName,
            ClassId = classId,
            AvailableAtLevel = availableAtLevel,
        };

    /// <summary>
    /// Создаёт предмет.
    /// </summary>
    /// <param name="name">Название предмета.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <returns>Предмет.</returns>
    public static Item Item(string name, string systemName) => new()
    {
        Name = name,
        SystemName = systemName,
    };

    /// <summary>
    /// Создаёт слот экипировки.
    /// </summary>
    /// <param name="name">Название слота.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <param name="maximumItems">Сколько предметов помещается в слот.</param>
    /// <returns>Слот экипировки.</returns>
    public static EquipmentSlot Slot(string name, string systemName, int maximumItems = 1) => new()
    {
        Name = name,
        SystemName = systemName,
        MaximumItems = maximumItems,
        AllowMultiple = maximumItems > 1,
    };

    /// <summary>
    /// Создаёт надеваемый предмет с бонусами.
    /// </summary>
    /// <param name="name">Название предмета.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <param name="slotId">Слот экипировки.</param>
    /// <param name="requirements">Требования к ношению.</param>
    /// <param name="bonuses">Бонусы предмета.</param>
    /// <returns>Предмет.</returns>
    public static Item Equipment(
        string name,
        string systemName,
        Guid? slotId = null,
        string? requirements = null,
        params ItemBonus[] bonuses)
    {
        var item = new Item
        {
            Name = name,
            SystemName = systemName,
            EquipmentSlotId = slotId,
            Requirements = requirements,
        };

        foreach (var bonus in bonuses)
        {
            bonus.ItemId = item.Id;
            item.Bonuses.Add(bonus);
        }

        return item;
    }

    /// <summary>
    /// Создаёт эффект вместе с его изменениями.
    /// </summary>
    /// <param name="name">Название эффекта.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <param name="tone">Окраска эффекта.</param>
    /// <param name="category">Категория эффекта.</param>
    /// <param name="durationFormula">Формула длительности.</param>
    /// <param name="durationUnit">Единица длительности.</param>
    /// <param name="stacking">Правило повторного наложения.</param>
    /// <param name="maximumStacks">Предел наложений.</param>
    /// <param name="priority">Приоритет эффекта.</param>
    /// <param name="bonuses">Изменения, которые вносит эффект.</param>
    /// <returns>Эффект.</returns>
    /// <summary>
    /// Создаёт вид отдыха.
    /// </summary>
    /// <param name="name">Название отдыха.</param>
    /// <param name="systemName">Внутреннее имя, задающее событие правил.</param>
    /// <param name="duration">Длительность отдыха.</param>
    /// <param name="durationUnit">Единица длительности.</param>
    /// <param name="requirements">Требования к отдыху.</param>
    /// <param name="sortOrder">Порядок отображения.</param>
    /// <param name="restores">Что отдых восстанавливает.</param>
    /// <returns>Вид отдыха.</returns>
    public static RestType Rest(
        string name,
        string systemName,
        double? duration = null,
        string? durationUnit = null,
        string? requirements = null,
        int sortOrder = 0,
        params RestRestore[] restores)
    {
        var rest = new RestType
        {
            Name = name,
            SystemName = systemName,
            Duration = duration,
            DurationUnit = durationUnit,
            Requirements = requirements,
            SortOrder = sortOrder,
        };

        foreach (var restore in restores)
        {
            restore.RestTypeId = rest.Id;
            rest.Restores.Add(restore);
        }

        return rest;
    }

    /// <summary>
    /// Создаёт восстановление ресурса при отдыхе.
    /// </summary>
    /// <param name="resourceId">Ресурс; <see langword="null"/> — все ресурсы.</param>
    /// <param name="mode">Способ восстановления.</param>
    /// <param name="formula">Формула величины.</param>
    /// <param name="condition">Условие восстановления.</param>
    /// <param name="sortOrder">Порядок применения.</param>
    /// <returns>Восстановление ресурса.</returns>
    public static RestRestore Restore(
        Guid? resourceId = null,
        RestRestoreMode mode = RestRestoreMode.Full,
        string? formula = null,
        string? condition = null,
        int sortOrder = 0) => new()
        {
            ResourceId = resourceId,
            Mode = mode,
            Formula = formula,
            Condition = condition,
            SortOrder = sortOrder,
        };

    public static Effect Effect(
        string name,
        string systemName,
        EffectTone tone = EffectTone.Positive,
        string? category = null,
        string? durationFormula = null,
        string? durationUnit = null,
        EffectStacking stacking = EffectStacking.Refresh,
        int? maximumStacks = null,
        int priority = 0,
        params EffectBonus[] bonuses)
    {
        var effect = new Effect
        {
            Name = name,
            SystemName = systemName,
            Tone = tone,
            Category = category,
            DurationFormula = durationFormula,
            DurationUnit = durationUnit,
            Stacking = stacking,
            MaximumStacks = maximumStacks,
            Priority = priority,
        };

        foreach (var bonus in bonuses)
        {
            bonus.EffectId = effect.Id;
            effect.Bonuses.Add(bonus);
        }

        return effect;
    }

    /// <summary>
    /// Создаёт изменение, вносимое эффектом.
    /// </summary>
    /// <param name="target">Что изменяется.</param>
    /// <param name="formula">Формула величины.</param>
    /// <param name="attributeId">Изменяемая характеристика.</param>
    /// <param name="resourceId">Изменяемый ресурс.</param>
    /// <param name="name">Имя величины или признака.</param>
    /// <param name="condition">Условие действия изменения.</param>
    /// <param name="sortOrder">Порядок отображения.</param>
    /// <returns>Изменение эффекта.</returns>
    public static EffectBonus EffectChange(
        BonusTargetKind target,
        string? formula = null,
        Guid? attributeId = null,
        Guid? resourceId = null,
        string? name = null,
        string? condition = null,
        int sortOrder = 0) => new()
        {
            Target = target,
            Formula = formula,
            AttributeId = attributeId,
            ResourceId = resourceId,
            Name = name,
            Condition = condition,
            SortOrder = sortOrder,
        };

    /// <summary>
    /// Создаёт заклинание.
    /// </summary>
    /// <param name="name">Название заклинания.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <param name="level">Уровень заклинания. Ноль — кантрип.</param>
    /// <param name="school">Школа магии.</param>
    /// <param name="formula">Формула результата.</param>
    /// <param name="scalingFormula">Формула усиления.</param>
    /// <param name="resourceId">Расходуемый ресурс.</param>
    /// <param name="resourceCostFormula">Формула стоимости.</param>
    /// <param name="requiresConcentration">Заклинание требует концентрации.</param>
    /// <param name="requirements">Требования к изучению и применению.</param>
    /// <returns>Заклинание.</returns>
    public static Spell Spell(
        string name,
        string systemName,
        int level = 1,
        string? school = null,
        string? formula = null,
        string? scalingFormula = null,
        Guid? resourceId = null,
        string? resourceCostFormula = null,
        bool requiresConcentration = false,
        string? requirements = null) => new()
        {
            Name = name,
            SystemName = systemName,
            Level = level,
            School = school,
            Formula = formula,
            ScalingFormula = scalingFormula,
            ResourceId = resourceId,
            ResourceCostFormula = resourceCostFormula,
            RequiresConcentration = requiresConcentration,
            Requirements = requirements,
        };

    /// <summary>
    /// Создаёт предмет инвентаря.
    /// </summary>
    /// <param name="name">Название предмета.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <param name="weight">Вес единицы предмета.</param>
    /// <param name="price">Стоимость единицы предмета.</param>
    /// <param name="currency">Валюта стоимости.</param>
    /// <param name="categoryId">Категория предмета.</param>
    /// <param name="rarity">Редкость предмета.</param>
    /// <param name="stackable">Предметы складываются в стопку.</param>
    /// <param name="maximumStackSize">Наибольший размер стопки.</param>
    /// <param name="chargesFormula">Формула количества зарядов.</param>
    /// <param name="useCost">Что расходует использование.</param>
    /// <param name="useEffects">Действия при использовании.</param>
    /// <returns>Предмет.</returns>
    public static Item Item(
        string name,
        string systemName,
        double weight = 0,
        double price = 0,
        string? currency = null,
        Guid? categoryId = null,
        string? rarity = null,
        bool stackable = false,
        int? maximumStackSize = null,
        string? chargesFormula = null,
        ItemUseCost useCost = ItemUseCost.None,
        params ItemUseEffect[] useEffects)
    {
        var item = new Item
        {
            Name = name,
            SystemName = systemName,
            Weight = weight,
            Price = price,
            Currency = currency,
            CategoryId = categoryId,
            Rarity = rarity,
            Stackable = stackable,
            MaximumStackSize = maximumStackSize,
            ChargesFormula = chargesFormula,
            UseCost = useCost,
        };

        foreach (var effect in useEffects)
        {
            effect.ItemId = item.Id;
            item.UseEffects.Add(effect);
        }

        return item;
    }

    /// <summary>
    /// Создаёт вместилище.
    /// </summary>
    /// <param name="name">Название вместилища.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <param name="weight">Собственный вес вместилища.</param>
    /// <param name="capacity">Вместимость в единицах веса.</param>
    /// <param name="contentWeightFactor">Доля веса содержимого, передаваемая носителю.</param>
    /// <returns>Предмет-вместилище.</returns>
    public static Item Container(
        string name,
        string systemName,
        double weight = 0,
        double? capacity = null,
        double contentWeightFactor = 1) => new()
        {
            Name = name,
            SystemName = systemName,
            Weight = weight,
            IsContainer = true,
            Capacity = capacity,
            ContentWeightFactor = contentWeightFactor,
        };

    /// <summary>
    /// Создаёт категорию предметов.
    /// </summary>
    /// <param name="name">Название категории.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <param name="parentId">Вышестоящая категория.</param>
    /// <returns>Категория предметов.</returns>
    public static ItemCategory Category(string name, string systemName, Guid? parentId = null) => new()
    {
        Name = name,
        SystemName = systemName,
        ParentId = parentId,
    };

    /// <summary>
    /// Создаёт действие, происходящее при использовании предмета.
    /// </summary>
    /// <param name="formula">Формула изменения ресурса.</param>
    /// <param name="resourceId">Изменяемый ресурс.</param>
    /// <param name="name">Пояснение к действию.</param>
    /// <param name="sortOrder">Порядок применения.</param>
    /// <returns>Действие предмета.</returns>
    public static ItemUseEffect UseEffect(
        string? formula = null,
        Guid? resourceId = null,
        string? name = null,
        int sortOrder = 0) => new()
        {
            Formula = formula,
            ResourceId = resourceId,
            Name = name,
            SortOrder = sortOrder,
        };

    /// <summary>
    /// Создаёт бонус предмета.
    /// </summary>
    /// <param name="target">Что изменяет бонус.</param>
    /// <param name="formula">Формула величины.</param>
    /// <param name="attributeId">Изменяемая характеристика.</param>
    /// <param name="resourceId">Изменяемый ресурс.</param>
    /// <param name="name">Имя величины или признака.</param>
    /// <param name="condition">Условие действия бонуса.</param>
    /// <param name="sortOrder">Порядок отображения.</param>
    /// <returns>Бонус предмета.</returns>
    public static ItemBonus Bonus(
        BonusTargetKind target,
        string? formula = null,
        Guid? attributeId = null,
        Guid? resourceId = null,
        string? name = null,
        string? condition = null,
        int sortOrder = 0) => new()
        {
            Target = target,
            Formula = formula,
            AttributeId = attributeId,
            ResourceId = resourceId,
            Name = name,
            Condition = condition,
            SortOrder = sortOrder,
        };

    /// <summary>
    /// Создаёт оружие.
    /// </summary>
    /// <param name="name">Название оружия.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <param name="damageFormula">Формула урона.</param>
    /// <param name="attackDiceFormula">Формула кости попадания.</param>
    /// <param name="attackFormula">Формула бонуса попадания.</param>
    /// <param name="criticalThreshold">Порог критического попадания.</param>
    /// <param name="criticalFormula">Формула критического урона.</param>
    /// <param name="scalingAttributeId">Характеристика масштабирования.</param>
    /// <param name="proficiencySkillId">Навык владения оружием.</param>
    /// <param name="ammunitionItemId">Предмет-боеприпас.</param>
    /// <param name="ammunitionPerShot">Расход боеприпасов за атаку.</param>
    /// <param name="magazineSize">Вместимость магазина.</param>
    /// <param name="properties">Свойства оружия.</param>
    /// <param name="requirements">Требования к применению.</param>
    /// <returns>Предмет с оружейными свойствами.</returns>
    public static Item Weapon(
        string name,
        string systemName,
        string? damageFormula = null,
        string? attackDiceFormula = null,
        string? attackFormula = null,
        int? criticalThreshold = null,
        string? criticalFormula = null,
        Guid? scalingAttributeId = null,
        Guid? proficiencySkillId = null,
        Guid? ammunitionItemId = null,
        int ammunitionPerShot = 1,
        int? magazineSize = null,
        string? properties = null,
        string? requirements = null)
    {
        var item = new Item
        {
            Name = name,
            SystemName = systemName,
            ItemType = "Оружие",
            Requirements = requirements,
        };

        item.Weapon = new Weapon
        {
            ItemId = item.Id,
            DamageFormula = damageFormula,
            AttackDiceFormula = attackDiceFormula,
            AttackFormula = attackFormula,
            CriticalThreshold = criticalThreshold,
            CriticalFormula = criticalFormula,
            ScalingAttributeId = scalingAttributeId,
            ProficiencySkillId = proficiencySkillId,
            AmmunitionItemId = ammunitionItemId,
            AmmunitionPerShot = ammunitionPerShot,
            MagazineSize = magazineSize,
            Properties = properties,
        };

        return item;
    }

    /// <summary>
    /// Создаёт черту.
    /// </summary>
    /// <param name="name">Название черты.</param>
    /// <param name="systemName">Внутреннее имя.</param>
    /// <param name="requirements">Требования к получению.</param>
    /// <param name="requiredTraitId">Черта, требуемая для получения этой черты.</param>
    /// <returns>Черта.</returns>
    public static Trait Trait(
        string name,
        string systemName,
        string? requirements = null,
        Guid? requiredTraitId = null) => new()
        {
            Name = name,
            SystemName = systemName,
            Requirements = requirements,
            RequiredTraitId = requiredTraitId,
        };
}
