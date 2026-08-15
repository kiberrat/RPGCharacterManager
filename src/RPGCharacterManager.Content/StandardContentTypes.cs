using Microsoft.EntityFrameworkCore;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Content;

/// <summary>
/// Описания встроенных видов игрового контента.
///
/// Здесь перечислено, из каких полей состоит каждый вид объектов. Сами объекты
/// создаёт пользователь: приложение не содержит контента какой-либо игровой системы.
/// Новый вид контента добавляется регистрацией собственного описания и не требует
/// изменения ни редактора, ни хранилища.
/// </summary>
public static class StandardContentTypes
{
    /// <summary>Значение по умолчанию для количества граней кости хитов.</summary>
    private const string DefaultHitDiceFormula = "1d8";

    /// <summary>Количество граней нового пользовательского кубика.</summary>
    private const int DefaultDieSides = 6;

    /// <summary>
    /// Возвращает описания всех встроенных видов контента.
    /// </summary>
    /// <returns>Последовательность описаний.</returns>
    public static IEnumerable<IContentTypeDescriptor> Create()
    {
        yield return CreateGameSystems();
        yield return CreateContentPacks();
        yield return CreateAttributes();
        yield return CreateSkills();
        yield return CreateRaces();
        yield return CreateBackgrounds();
        yield return CreateClasses();
        yield return CreateSubclasses();
        yield return CreateTraits();
        yield return CreateAbilities();
        yield return CreateSpells();
        yield return CreateResources();
        yield return CreateEffects();
        yield return CreateItemCategories();
        yield return CreateItems();
        yield return CreateWeapons();
        yield return CreateEquipmentSlots();
        yield return CreateRestTypes();
        yield return CreateDieTypes();
        yield return CreateMonsters();
        yield return CreateLocations();
        yield return CreateNpcs();
        yield return CreateQuests();
    }

    /// <summary>
    /// Добавляет поля, общие для всех игровых объектов: название, внутреннее имя,
    /// описание, источник, принадлежность игровой системе и оформление.
    /// </summary>
    /// <typeparam name="TEntity">Тип игрового объекта.</typeparam>
    /// <param name="builder">Построитель описания вида.</param>
    /// <returns>Тот же построитель.</returns>
    private static ContentTypeBuilder<TEntity> WithCommonFields<TEntity>(
        this ContentTypeBuilder<TEntity> builder)
        where TEntity : ContentEntity, new() =>
        builder
            .NamedBy(entity => entity.Name, (entity, name) => entity.Name = name)
            .Text(
                "name",
                "Название",
                entity => entity.Name,
                (entity, value) => entity.Name = value ?? string.Empty,
                isRequired: true)
            .Text(
                "systemName",
                "Внутреннее имя",
                entity => entity.SystemName,
                (entity, value) => entity.SystemName = value ?? string.Empty,
                hint: "Имя для формул и правил. Заполняется автоматически по названию.")
            .LongText("description", "Описание", entity => entity.Description, (entity, value) => entity.Description = value)
            .Text("source", "Источник", entity => entity.Source, (entity, value) => entity.Source = value,
                hint: "Книга, контент-пак или автор")
            .Reference(
                "gameSystem",
                "Игровая система",
                ContentTypeIds.GameSystems,
                entity => entity.GameSystemId,
                (entity, value) => entity.GameSystemId = value,
                ContentFieldGroups.General)
            .Image("image", "Изображение", entity => entity.Image, (entity, value) => entity.Image = value)
            .Text("icon", "Значок", entity => entity.Icon, (entity, value) => entity.Icon = value,
                ContentFieldGroups.Appearance);

    private static IContentTypeDescriptor CreateGameSystems() =>
        new ContentTypeBuilder<GameSystem>(ContentTypeIds.GameSystems, "Игровые системы", "Игровая система")
            .Describe("Набор правил и контента: D&D, Pathfinder, Cyberpunk или собственная система.", 0)
            .NamedBy(entity => entity.Name, (entity, name) => entity.Name = name)
            .Text("name", "Название", entity => entity.Name, (entity, value) => entity.Name = value ?? string.Empty, isRequired: true)
            .Text("systemName", "Внутреннее имя", entity => entity.SystemName, (entity, value) => entity.SystemName = value ?? string.Empty)
            .Text("version", "Версия", entity => entity.Version, (entity, value) => entity.Version = value ?? "1.0")
            .Text("author", "Автор", entity => entity.Author, (entity, value) => entity.Author = value)
            .LongText("description", "Описание", entity => entity.Description, (entity, value) => entity.Description = value)
            .Boolean("enabled", "Система включена", entity => entity.Enabled, (entity, value) => entity.Enabled = value)
            .Text("weightUnit", "Единица веса", entity => entity.WeightUnit,
                (entity, value) => entity.WeightUnit = value, ContentFieldGroups.Rules,
                hint: "Например: кг, фунтов, ячеек, литров")
            .Formula("carryCapacity", "Формула переносимого веса",
                entity => entity.CarryCapacityFormula, (entity, value) => entity.CarryCapacityFormula = value,
                "Например: Сила * 10. Пусто — ноша не ограничена")
            .Formula("knownSpells", "Формула предела известных заклинаний",
                entity => entity.KnownSpellsFormula, (entity, value) => entity.KnownSpellsFormula = value,
                "Например: Интеллект + Уровень. Пусто — изучение не ограничено")
            .Formula("preparedSpells", "Формула предела подготовленных заклинаний",
                entity => entity.PreparedSpellsFormula, (entity, value) => entity.PreparedSpellsFormula = value,
                "Задана — применяются только подготовленные. Пусто — подготовка не обязательна")
            .Text("icon", "Значок", entity => entity.Icon, (entity, value) => entity.Icon = value, ContentFieldGroups.Appearance)
            .Build();

    private static IContentTypeDescriptor CreateContentPacks() =>
        new ContentTypeBuilder<ContentPack>(ContentTypeIds.ContentPacks, "Контент-паки", "Контент-пак")
            .Describe("Набор игровых объектов, подключаемый и отключаемый целиком.", 10)
            .NamedBy(entity => entity.Name, (entity, name) => entity.Name = name)
            .Text("name", "Название", entity => entity.Name, (entity, value) => entity.Name = value ?? string.Empty, isRequired: true)
            .Text("version", "Версия", entity => entity.Version, (entity, value) => entity.Version = value ?? "1.0")
            .Text("author", "Автор", entity => entity.Author, (entity, value) => entity.Author = value)
            .LongText("description", "Описание", entity => entity.Description, (entity, value) => entity.Description = value)
            .Reference("gameSystem", "Игровая система", ContentTypeIds.GameSystems,
                entity => entity.GameSystemId, (entity, value) => entity.GameSystemId = value, ContentFieldGroups.General)
            .Boolean("enabled", "Пак включён", entity => entity.Enabled, (entity, value) => entity.Enabled = value)
            .Build();

    private static IContentTypeDescriptor CreateAttributes() =>
        new ContentTypeBuilder<AttributeDefinition>(ContentTypeIds.Attributes, "Характеристики", "Характеристика")
            .Describe("Любой числовой или вычисляемый параметр: Сила, Удача, Радиация, Репутация.", 20)
            .WithCommonFields()
            .Text("category", "Категория", entity => entity.Category, (entity, value) => entity.Category = value, ContentFieldGroups.Rules)
            .Number("defaultValue", "Значение по умолчанию", entity => entity.DefaultValue, (entity, value) => entity.DefaultValue = value)
            .OptionalNumber("minimum", "Минимум", entity => entity.MinimumValue,
                (entity, value) => entity.MinimumValue = value, hint: "Пусто — ограничения нет")
            .OptionalNumber("maximum", "Максимум", entity => entity.MaximumValue,
                (entity, value) => entity.MaximumValue = value, hint: "Пусто — ограничения нет")
            .Boolean("hidden", "Скрытая характеристика", entity => entity.IsHidden, (entity, value) => entity.IsHidden = value)
            .Integer("sortOrder", "Порядок отображения", entity => entity.SortOrder, (entity, value) => entity.SortOrder = value)
            .Formula("formula", "Формула значения", entity => entity.Formula, (entity, value) => entity.Formula = value,
                "Если задана, характеристика вычисляется и не редактируется вручную")
            .Formula("modifierFormula", "Формула модификатора", entity => entity.ModifierFormula,
                (entity, value) => entity.ModifierFormula = value,
                "Например: ОкруглитьВниз((Значение - 10) / 2)")
            .Color("color", "Цвет", entity => entity.Color, (entity, value) => entity.Color = value)
            .Build();

    private static IContentTypeDescriptor CreateSkills() =>
        new ContentTypeBuilder<Skill>(ContentTypeIds.Skills, "Навыки", "Навык")
            .Describe("Умение, используемое в игровых проверках.", 30)
            .WithCommonFields()
            .Text("category", "Категория", entity => entity.Category, (entity, value) => entity.Category = value, ContentFieldGroups.Rules)
            .Reference("attribute", "Связанная характеристика", ContentTypeIds.Attributes,
                entity => entity.LinkedAttributeId, (entity, value) => entity.LinkedAttributeId = value)
            .OptionalInteger("maximumLevel", "Максимальный уровень владения", entity => entity.MaximumLevel,
                (entity, value) => entity.MaximumLevel = value, hint: "Пусто — ограничения нет")
            .Integer("sortOrder", "Порядок отображения", entity => entity.SortOrder, (entity, value) => entity.SortOrder = value)
            .Formula("formula", "Формула значения", entity => entity.Formula, (entity, value) => entity.Formula = value,
                "Например: Ловкость + Владение")
            .Requirement("requirements", "Требования", entity => entity.Requirements, (entity, value) => entity.Requirements = value)
            .Color("color", "Цвет", entity => entity.Color, (entity, value) => entity.Color = value)
            .Build();

    private static IContentTypeDescriptor CreateRaces() =>
        new ContentTypeBuilder<Race>(ContentTypeIds.Races, "Расы", "Раса")
            .Describe("Раса или происхождение вида, задающее базовые особенности персонажа.", 40)
            .WithCommonFields()
            .Number("speed", "Скорость", entity => entity.Speed, (entity, value) => entity.Speed = value)
            .Text("size", "Размер", entity => entity.Size, (entity, value) => entity.Size = value, ContentFieldGroups.Rules)
            .Text("languages", "Языки", entity => entity.Languages, (entity, value) => entity.Languages = value, ContentFieldGroups.Rules)
            .Requirement("requirements", "Требования", entity => entity.Requirements, (entity, value) => entity.Requirements = value)
            .Build();

    private static IContentTypeDescriptor CreateBackgrounds() =>
        new ContentTypeBuilder<Background>(ContentTypeIds.Backgrounds, "Происхождения", "Происхождение")
            .Describe("Предыстория, культура, профессия или фракция персонажа.", 50)
            .WithCommonFields()
            .Requirement("requirements", "Требования", entity => entity.Requirements, (entity, value) => entity.Requirements = value)
            .Build();

    private static IContentTypeDescriptor CreateClasses() =>
        new ContentTypeBuilder<CharacterClass>(ContentTypeIds.Classes, "Классы", "Класс")
            .Describe("Основной путь развития персонажа: класс, профессия, архетип или роль.", 60)
            .CreatedBy(() => new CharacterClass { HitDiceFormula = DefaultHitDiceFormula })
            .WithCommonFields()
            .Text("role", "Роль", entity => entity.Role, (entity, value) => entity.Role = value, ContentFieldGroups.Rules)
            .Reference("primaryAttribute", "Основная характеристика", ContentTypeIds.Attributes,
                entity => entity.PrimaryAttributeId, (entity, value) => entity.PrimaryAttributeId = value)
            .Integer("startingLevel", "Начальный уровень", entity => entity.StartingLevel, (entity, value) => entity.StartingLevel = value)
            .Integer("maximumLevel", "Максимальный уровень", entity => entity.MaximumLevel, (entity, value) => entity.MaximumLevel = value)
            .Formula("hitDice", "Формула здоровья за уровень", entity => entity.HitDiceFormula,
                (entity, value) => entity.HitDiceFormula = value, "Например: 1d10 + Телосложение")
            .Requirement("requirements", "Требования", entity => entity.Requirements, (entity, value) => entity.Requirements = value)
            .Color("color", "Цвет", entity => entity.Color, (entity, value) => entity.Color = value)
            .Build();

    private static IContentTypeDescriptor CreateSubclasses() =>
        new ContentTypeBuilder<Subclass>(ContentTypeIds.Subclasses, "Подклассы", "Подкласс")
            .Describe("Специализация внутри класса.", 70)
            .WithCommonFields()
            .Reference("class", "Класс", ContentTypeIds.Classes,
                entity => entity.ClassId, (entity, value) => entity.ClassId = value ?? Guid.Empty)
            .Integer("availableAtLevel", "Доступен с уровня", entity => entity.AvailableAtLevel, (entity, value) => entity.AvailableAtLevel = value)
            .Requirement("requirements", "Требования", entity => entity.Requirements, (entity, value) => entity.Requirements = value)
            .Build();

    private static IContentTypeDescriptor CreateTraits() =>
        new ContentTypeBuilder<Trait>(ContentTypeIds.Traits, "Черты", "Черта")
            .Describe("Черта, талант, перк или преимущество, изменяющее возможности персонажа.", 80)
            .WithCommonFields()
            .Text("category", "Категория", entity => entity.Category, (entity, value) => entity.Category = value, ContentFieldGroups.Rules)
            .Integer("level", "Уровень или ранг черты", entity => entity.Level, (entity, value) => entity.Level = value)
            .Reference("requiredTrait", "Требуемая черта", ContentTypeIds.Traits,
                entity => entity.RequiredTraitId, (entity, value) => entity.RequiredTraitId = value)
            .Text("recharge", "Восстановление использований", entity => entity.RechargeRule,
                (entity, value) => entity.RechargeRule = value, ContentFieldGroups.Rules,
                hint: "Например: после длительного отдыха")
            .Formula("formula", "Формула эффекта", entity => entity.Formula, (entity, value) => entity.Formula = value)
            .Formula("uses", "Формула количества использований", entity => entity.UsesFormula, (entity, value) => entity.UsesFormula = value)
            .Formula("activation", "Условие действия", entity => entity.ActivationCondition,
                (entity, value) => entity.ActivationCondition = value, "Черта работает только при выполнении условия")
            .Requirement("requirements", "Требования", entity => entity.Requirements, (entity, value) => entity.Requirements = value)
            .Build();

    private static IContentTypeDescriptor CreateAbilities() =>
        new ContentTypeBuilder<Ability>(ContentTypeIds.Abilities, "Способности", "Способность")
            .Describe("Активное или пассивное действие, доступное персонажу.", 90)
            .WithCommonFields()
            .Text("category", "Категория", entity => entity.Category, (entity, value) => entity.Category = value, ContentFieldGroups.Rules)
            .Reference("resource", "Расходуемый ресурс", ContentTypeIds.Resources,
                entity => entity.ResourceId, (entity, value) => entity.ResourceId = value)
            .Text("recharge", "Восстановление", entity => entity.RechargeRule, (entity, value) => entity.RechargeRule = value, ContentFieldGroups.Rules)
            .Formula("formula", "Формула результата", entity => entity.Formula, (entity, value) => entity.Formula = value)
            .Formula("cost", "Формула стоимости", entity => entity.ResourceCostFormula, (entity, value) => entity.ResourceCostFormula = value)
            .Requirement("requirements", "Требования", entity => entity.Requirements, (entity, value) => entity.Requirements = value)
            .Build();

    private static IContentTypeDescriptor CreateSpells() =>
        new ContentTypeBuilder<Spell>(ContentTypeIds.Spells, "Заклинания", "Заклинание")
            .Describe("Заклинание, техника, ритуал или особое действие.", 100)
            .WithCommonFields()
            .Integer("level", "Уровень", entity => entity.Level, (entity, value) => entity.Level = value)
            .Text("school", "Школа", entity => entity.School, (entity, value) => entity.School = value, ContentFieldGroups.Rules)
            .Text("category", "Категория", entity => entity.Category, (entity, value) => entity.Category = value, ContentFieldGroups.Rules)
            .Text("castingTime", "Время применения", entity => entity.CastingTime, (entity, value) => entity.CastingTime = value, ContentFieldGroups.Rules)
            .Text("range", "Дальность", entity => entity.Range, (entity, value) => entity.Range = value, ContentFieldGroups.Rules)
            .Text("area", "Область действия", entity => entity.AreaOfEffect, (entity, value) => entity.AreaOfEffect = value, ContentFieldGroups.Rules)
            .Text("target", "Цель", entity => entity.Target, (entity, value) => entity.Target = value, ContentFieldGroups.Rules)
            .Text("components", "Компоненты", entity => entity.Components, (entity, value) => entity.Components = value, ContentFieldGroups.Rules)
            .Text("duration", "Длительность", entity => entity.Duration, (entity, value) => entity.Duration = value, ContentFieldGroups.Rules)
            .Boolean("concentration", "Требует концентрации", entity => entity.RequiresConcentration, (entity, value) => entity.RequiresConcentration = value)
            .Boolean("ritual", "Можно применить как ритуал", entity => entity.IsRitual, (entity, value) => entity.IsRitual = value)
            .Reference("resource", "Расходуемый ресурс", ContentTypeIds.Resources,
                entity => entity.ResourceId, (entity, value) => entity.ResourceId = value)
            .Formula("formula", "Формула результата", entity => entity.Formula, (entity, value) => entity.Formula = value,
                "Например: 8d6 или 2d8 + Интеллект")
            .Formula("scaling", "Формула усиления", entity => entity.ScalingFormula, (entity, value) => entity.ScalingFormula = value)
            .Formula("cost", "Формула стоимости", entity => entity.ResourceCostFormula, (entity, value) => entity.ResourceCostFormula = value)
            .Requirement("requirements", "Требования", entity => entity.Requirements, (entity, value) => entity.Requirements = value)
            .Build();

    private static IContentTypeDescriptor CreateResources() =>
        new ContentTypeBuilder<GameResource>(ContentTypeIds.Resources, "Ресурсы", "Ресурс")
            .Describe("Здоровье, мана, ярость, выносливость, патроны или любой собственный счётчик.", 110)
            .WithCommonFields()
            .Text("category", "Категория", entity => entity.Category, (entity, value) => entity.Category = value, ContentFieldGroups.Rules)
            .Text("restoreRule", "Правило восстановления", entity => entity.RestoreRule,
                (entity, value) => entity.RestoreRule = value, ContentFieldGroups.Rules,
                hint: "Например: после длительного отдыха")
            .Integer("sortOrder", "Порядок отображения", entity => entity.SortOrder, (entity, value) => entity.SortOrder = value)
            .Formula("maximum", "Формула максимума", entity => entity.MaximumFormula, (entity, value) => entity.MaximumFormula = value,
                "Например: Интеллект * 8")
            .Formula("starting", "Формула начального значения", entity => entity.StartingFormula, (entity, value) => entity.StartingFormula = value)
            .Color("color", "Цвет полосы", entity => entity.Color, (entity, value) => entity.Color = value)
            .Build();

    private static IContentTypeDescriptor CreateEffects() =>
        new ContentTypeBuilder<Effect>(ContentTypeIds.Effects, "Эффекты", "Эффект")
            .Describe("Бафф, дебафф, аура, болезнь, проклятие или благословение.", 120)
            .Including(query => query.Include(effect => effect.Bonuses))
            .WithCommonFields()
            .Text("category", "Категория", entity => entity.Category, (entity, value) => entity.Category = value, ContentFieldGroups.Rules,
                hint: "Например: болезнь, проклятие, благословение, аура — перечень задаёте вы")
            .Enumeration(
                "tone",
                "Окраска",
                EffectTones.All,
                entity => EffectTones.ToText(entity.Tone),
                (entity, value) => entity.Tone = EffectTones.Parse(value),
                ContentFieldGroups.Rules,
                "Определяет цвет отметки в панели эффектов")
            .Text("durationUnit", "Единица длительности", entity => entity.DurationUnit,
                (entity, value) => entity.DurationUnit = value, ContentFieldGroups.Rules,
                hint: "Например: раунд, минута, час. Пусто — эффект без срока")
            .Text("area", "Область действия", entity => entity.Area,
                (entity, value) => entity.Area = value, ContentFieldGroups.Rules,
                hint: "Например: сфера 10 м, вся группа")
            .Enumeration(
                "stacking",
                "Повторное наложение",
                EffectStackings.All,
                entity => EffectStackings.ToText(entity.Stacking),
                (entity, value) => entity.Stacking = EffectStackings.Parse(value),
                ContentFieldGroups.Rules)
            .OptionalInteger("maximumStacks", "Предел наложений", entity => entity.MaximumStacks,
                (entity, value) => entity.MaximumStacks = value, hint: "Пусто — ограничения нет")
            .Integer("priority", "Приоритет", entity => entity.Priority, (entity, value) => entity.Priority = value)
            .Formula("duration", "Формула длительности", entity => entity.DurationFormula,
                (entity, value) => entity.DurationFormula = value,
                "Сколько единиц действует эффект. Например: 10 или Уровень. Пусто — без срока")
            .Formula("formula", "Формула величины", entity => entity.Formula, (entity, value) => entity.Formula = value)
            .Formula("endCondition", "Условие прекращения", entity => entity.EndCondition, (entity, value) => entity.EndCondition = value)
            .Color("color", "Цвет значка", entity => entity.Color, (entity, value) => entity.Color = value)
            .WithEffectBonuses()
            .Build();

    /// <summary>
    /// Добавляет эффекту список бонусов, действующих, пока эффект наложен.
    ///
    /// Устройство совпадает с бонусами предметов: усиление от кольца и от
    /// благословения описывается одинаково и попадает в расчёт одним путём.
    /// </summary>
    /// <param name="builder">Построитель описания вида.</param>
    /// <returns>Тот же построитель.</returns>
    private static ContentTypeBuilder<Effect> WithEffectBonuses(this ContentTypeBuilder<Effect> builder) =>
        builder.Collection<EffectBonus>(
            "bonuses",
            "Что изменяет эффект",
            "Изменение",
            entity => entity.Bonuses,
            bonuses => bonuses
                .Describe(
                    "Действуют, пока эффект наложен. Формула вычисляется по параметрам персонажа "
                    + "без учёта бонусов, поэтому эффекты не зависят от порядка наложения.")
                .AttachedBy((bonus, effect) => bonus.EffectId = effect.Id, bonus => bonus.SortOrder)
                .Enumeration(
                    "target",
                    "Что изменяет",
                    BonusTargets.All,
                    bonus => BonusTargets.ToText(bonus.Target),
                    (bonus, value) => bonus.Target = BonusTargets.Parse(value))
                .Reference(
                    "attribute",
                    "Характеристика",
                    ContentTypeIds.Attributes,
                    bonus => bonus.AttributeId,
                    (bonus, value) => bonus.AttributeId = value)
                .Reference(
                    "resource",
                    "Ресурс",
                    ContentTypeIds.Resources,
                    bonus => bonus.ResourceId,
                    (bonus, value) => bonus.ResourceId = value)
                .Text(
                    "name",
                    "Имя величины или признака",
                    bonus => bonus.Name,
                    (bonus, value) => bonus.Name = value,
                    "Заполняется, когда изменяется величина или признак")
                .Formula(
                    "formula",
                    "Формула",
                    bonus => bonus.Formula,
                    (bonus, value) => bonus.Formula = value,
                    "Например: 2, -1, ловкость / 2. Признаку формула не нужна")
                .Formula(
                    "condition",
                    "Условие",
                    bonus => bonus.Condition,
                    (bonus, value) => bonus.Condition = value,
                    "Пусто — изменение действует всегда")
                .Integer("sortOrder", "Порядок", bonus => bonus.SortOrder, (bonus, value) => bonus.SortOrder = value));

    /// <summary>
    /// Описание категорий предметов.
    ///
    /// Категории образуют дерево: вышестоящая категория выбирается ссылкой на другую
    /// категорию. Готовых категорий приложение не содержит — их состав определяет
    /// пользователь под свою игровую систему.
    /// </summary>
    /// <returns>Описание вида контента.</returns>
    private static IContentTypeDescriptor CreateItemCategories() =>
        new ContentTypeBuilder<ItemCategory>(ContentTypeIds.ItemCategories, "Категории предметов", "Категория предметов")
            .Describe("Раздел инвентаря: снаряжение, расходники, материалы или собственный.", 125)
            .WithCommonFields()
            .Reference("parent", "Вышестоящая категория", ContentTypeIds.ItemCategories,
                entity => entity.ParentId, (entity, value) => entity.ParentId = value,
                ContentFieldGroups.General)
            .Integer("sortOrder", "Порядок отображения", entity => entity.SortOrder, (entity, value) => entity.SortOrder = value)
            .Build();

    private static IContentTypeDescriptor CreateItems() =>
        new ContentTypeBuilder<Item>(ContentTypeIds.Items, "Предметы", "Предмет")
            .Describe("Любой объект инвентаря: снаряжение, расходники, ценности.", 130)
            // Два списка сразу: без разделения запроса строки бонусов и действий
            // перемножились бы между собой, и предмет читался бы тем медленнее,
            // чем подробнее он описан.
            .Including(query => query
                .Include(item => item.Bonuses)
                .Include(item => item.UseEffects))
            .WithCommonFields()
            .Reference("category", "Категория", ContentTypeIds.ItemCategories,
                entity => entity.CategoryId, (entity, value) => entity.CategoryId = value,
                ContentFieldGroups.General)
            .Text("itemType", "Тип предмета", entity => entity.ItemType, (entity, value) => entity.ItemType = value, ContentFieldGroups.Rules)
            .Text("rarity", "Редкость", entity => entity.Rarity, (entity, value) => entity.Rarity = value, ContentFieldGroups.Rules)
            .Number("weight", "Вес", entity => entity.Weight, (entity, value) => entity.Weight = value)
            .Number("price", "Стоимость", entity => entity.Price, (entity, value) => entity.Price = value)
            .Text("currency", "Валюта", entity => entity.Currency, (entity, value) => entity.Currency = value, ContentFieldGroups.Rules)
            .Boolean("stackable", "Складывается в стопку", entity => entity.Stackable, (entity, value) => entity.Stackable = value)
            .OptionalInteger("maximumStack", "Размер стопки", entity => entity.MaximumStackSize,
                (entity, value) => entity.MaximumStackSize = value, hint: "Пусто — ограничения нет")
            .WithEquipmentSlot()
            .Formula("charges", "Формула зарядов", entity => entity.ChargesFormula, (entity, value) => entity.ChargesFormula = value)
            .WithContainer()
            .WithUse()
            .Requirement("requirements", "Требования", entity => entity.Requirements, (entity, value) => entity.Requirements = value)
            .WithBonuses()
            .Build();

    /// <summary>
    /// Добавляет предмету свойства вместилища.
    ///
    /// Рюкзак, сундук, сейф и магическая сумка описываются одними и теми же полями:
    /// вместимостью и долей веса содержимого, которую вместилище передаёт носителю.
    /// </summary>
    /// <param name="builder">Построитель описания вида.</param>
    /// <returns>Тот же построитель.</returns>
    private static ContentTypeBuilder<Item> WithContainer(this ContentTypeBuilder<Item> builder) =>
        builder
            .Boolean("isContainer", "Вмещает другие предметы",
                entity => entity.IsContainer, (entity, value) => entity.IsContainer = value)
            .OptionalNumber("capacity", "Вместимость",
                entity => entity.Capacity, (entity, value) => entity.Capacity = value,
                hint: "В единицах веса. Пусто — вместимость не ограничена")
            .Number("contentWeightFactor", "Доля веса содержимого",
                entity => entity.ContentWeightFactor, (entity, value) => entity.ContentWeightFactor = value,
                hint: "1 — обычная сумка, 0 — безразмерная, 0,5 — облегчает ношу вдвое");

    /// <summary>
    /// Добавляет предмету описание использования: что тратится и что происходит.
    /// </summary>
    /// <param name="builder">Построитель описания вида.</param>
    /// <returns>Тот же построитель.</returns>
    private static ContentTypeBuilder<Item> WithUse(this ContentTypeBuilder<Item> builder) =>
        builder
            .Enumeration(
                "useCost",
                "Использование расходует",
                ItemUseCosts.All,
                entity => ItemUseCosts.ToText(entity.UseCost),
                (entity, value) => entity.UseCost = ItemUseCosts.Parse(value),
                ContentFieldGroups.Rules)
            .Collection<ItemUseEffect>(
                "useEffects",
                "Действия при использовании",
                "Действие",
                entity => entity.UseEffects,
                effects => effects
                    .Describe(
                        "Происходят по нажатию «Использовать». Формула вычисляется по параметрам "
                        + "персонажа: положительная величина восстанавливает ресурс, отрицательная тратит.")
                    .AttachedBy((effect, item) => effect.ItemId = item.Id, effect => effect.SortOrder)
                    .Reference(
                        "resource",
                        "Ресурс",
                        ContentTypeIds.Resources,
                        effect => effect.ResourceId,
                        (effect, value) => effect.ResourceId = value)
                    .Text(
                        "name",
                        "Пояснение",
                        effect => effect.Name,
                        (effect, value) => effect.Name = value,
                        "Заполняется, когда ресурс не выбран")
                    .Formula(
                        "formula",
                        "Формула изменения",
                        effect => effect.Formula,
                        (effect, value) => effect.Formula = value,
                        "Например: 2к4 + 2, -1, уровень")
                    .Integer("sortOrder", "Порядок", effect => effect.SortOrder, (effect, value) => effect.SortOrder = value));

    /// <summary>
    /// Добавляет предмету слот экипировки.
    /// </summary>
    /// <param name="builder">Построитель описания вида.</param>
    /// <returns>Тот же построитель.</returns>
    private static ContentTypeBuilder<Item> WithEquipmentSlot(this ContentTypeBuilder<Item> builder) =>
        builder.Reference(
            "equipmentSlot",
            "Слот экипировки",
            ContentTypeIds.EquipmentSlots,
            entity => entity.EquipmentSlotId,
            (entity, value) => entity.EquipmentSlotId = value);

    /// <summary>
    /// Добавляет предмету список бонусов, действующих, пока он надет.
    ///
    /// Броня, кольцо, плащ, имплант и артефакт описываются одним и тем же списком:
    /// приложение не содержит перечня возможных усилений, его составляет пользователь.
    /// </summary>
    /// <param name="builder">Построитель описания вида.</param>
    /// <returns>Тот же построитель.</returns>
    private static ContentTypeBuilder<Item> WithBonuses(this ContentTypeBuilder<Item> builder) =>
        builder.Collection<ItemBonus>(
            "bonuses",
            "Бонусы экипировки",
            "Бонус",
            entity => entity.Bonuses,
            bonuses => bonuses
                .Describe(
                    "Действуют, пока предмет надет. Формула вычисляется по параметрам персонажа "
                    + "без учёта надетых предметов, поэтому бонусы не зависят от порядка надевания.")
                .AttachedBy((bonus, item) => bonus.ItemId = item.Id, bonus => bonus.SortOrder)
                .Enumeration(
                    "target",
                    "Что изменяет",
                    BonusTargets.All,
                    bonus => BonusTargets.ToText(bonus.Target),
                    (bonus, value) => bonus.Target = BonusTargets.Parse(value))
                .Reference(
                    "attribute",
                    "Характеристика",
                    ContentTypeIds.Attributes,
                    bonus => bonus.AttributeId,
                    (bonus, value) => bonus.AttributeId = value)
                .Reference(
                    "resource",
                    "Ресурс",
                    ContentTypeIds.Resources,
                    bonus => bonus.ResourceId,
                    (bonus, value) => bonus.ResourceId = value)
                .Text(
                    "name",
                    "Имя величины или признака",
                    bonus => bonus.Name,
                    (bonus, value) => bonus.Name = value,
                    "Заполняется, когда изменяется величина или признак")
                .Formula(
                    "formula",
                    "Формула",
                    bonus => bonus.Formula,
                    (bonus, value) => bonus.Formula = value,
                    "Например: 2, ловкость / 2, уровень * 5. Признаку формула не нужна")
                .Formula(
                    "condition",
                    "Условие",
                    bonus => bonus.Condition,
                    (bonus, value) => bonus.Condition = value,
                    "Пусто — бонус действует всегда")
                .Integer("sortOrder", "Порядок", bonus => bonus.SortOrder, (bonus, value) => bonus.SortOrder = value));

    /// <summary>
    /// Описание оружия.
    ///
    /// Оружие является предметом с дополнительными боевыми свойствами, поэтому вид
    /// работает с теми же записями, что и «Предметы», но отбирает только те из них,
    /// у которых заданы оружейные свойства, и показывает соответствующие поля.
    /// </summary>
    /// <returns>Описание вида контента.</returns>
    private static IContentTypeDescriptor CreateWeapons() =>
        new ContentTypeBuilder<Item>(ContentTypeIds.Weapons, "Оружие", "Оружие")
            .Describe("Предмет с боевыми свойствами: формулами попадания, урона и критического удара.", 140)
            .CreatedBy(() => new Item { ItemType = "Оружие", Weapon = new Weapon() })
            .FilteredBy(item => item.Weapon != null)
            .Including(query => query.Include(item => item.Weapon).Include(item => item.Bonuses))
            .WithCommonFields()
            .Text("itemType", "Тип оружия", entity => entity.ItemType,
                (entity, value) => entity.ItemType = value, ContentFieldGroups.Rules,
                hint: "Например: одноручное, двуручное, метательное, огнестрельное")
            .Text("category", "Категория", entity => entity.Weapon?.Category,
                (entity, value) => EnsureWeapon(entity).Category = value, ContentFieldGroups.Rules,
                hint: "Например: ближнее, дальнобойное, магическое")
            .Number("weight", "Вес", entity => entity.Weight, (entity, value) => entity.Weight = value)
            .Number("price", "Стоимость", entity => entity.Price, (entity, value) => entity.Price = value)
            .Text("rarity", "Редкость", entity => entity.Rarity, (entity, value) => entity.Rarity = value, ContentFieldGroups.Rules)
            .Text("damageType", "Тип урона", entity => entity.Weapon?.DamageType,
                (entity, value) => EnsureWeapon(entity).DamageType = value, ContentFieldGroups.Rules,
                hint: "Например: рубящий, огонь, психический — перечень задаёте вы")
            .Text("range", "Дальность", entity => entity.Weapon?.Range,
                (entity, value) => EnsureWeapon(entity).Range = value, ContentFieldGroups.Rules)
            .Text("properties", "Свойства", entity => entity.Weapon?.Properties,
                (entity, value) => EnsureWeapon(entity).Properties = value, ContentFieldGroups.Rules,
                hint: "Через запятую: острое, тяжёлое, пробивающее. Правила боя могут проверять их как признаки")
            .OptionalInteger("criticalThreshold", "Порог критического удара",
                entity => entity.Weapon?.CriticalThreshold,
                (entity, value) => EnsureWeapon(entity).CriticalThreshold = value,
                hint: "Критическим считается бросок кости не меньше этого значения. Пусто — критических ударов нет")
            .Reference("scalingAttribute", "Характеристика масштабирования", ContentTypeIds.Attributes,
                entity => entity.Weapon?.ScalingAttributeId,
                (entity, value) => EnsureWeapon(entity).ScalingAttributeId = value)
            .Reference("proficiencySkill", "Навык владения", ContentTypeIds.Skills,
                entity => entity.Weapon?.ProficiencySkillId,
                (entity, value) => EnsureWeapon(entity).ProficiencySkillId = value)
            .Reference("ammunition", "Боеприпас", ContentTypeIds.Items,
                entity => entity.Weapon?.AmmunitionItemId,
                (entity, value) => EnsureWeapon(entity).AmmunitionItemId = value)
            .Integer("ammunitionPerShot", "Расход боеприпасов за атаку",
                entity => entity.Weapon?.AmmunitionPerShot ?? 1,
                (entity, value) => EnsureWeapon(entity).AmmunitionPerShot = value)
            .OptionalInteger("magazineSize", "Вместимость магазина",
                entity => entity.Weapon?.MagazineSize,
                (entity, value) => EnsureWeapon(entity).MagazineSize = value,
                hint: "Пусто — боеприпасы расходуются из запаса и перезарядка не требуется")
            .Text("reloadTime", "Время перезарядки", entity => entity.Weapon?.ReloadTime,
                (entity, value) => EnsureWeapon(entity).ReloadTime = value, ContentFieldGroups.Rules,
                hint: "Например: действие, ход, две единицы времени")
            .Formula("attackDice", "Кость попадания", entity => entity.Weapon?.AttackDiceFormula,
                (entity, value) => EnsureWeapon(entity).AttackDiceFormula = value,
                "Например: 1d20. По этому броску определяется критическое попадание")
            .Formula("attack", "Бонус попадания", entity => entity.Weapon?.AttackFormula,
                (entity, value) => EnsureWeapon(entity).AttackFormula = value,
                "Прибавляется к кости. Например: характеристика + владение")
            .Formula("damage", "Формула урона", entity => entity.Weapon?.DamageFormula,
                (entity, value) => EnsureWeapon(entity).DamageFormula = value,
                "Например: 2d6 + характеристика")
            .Formula("critical", "Формула критического урона", entity => entity.Weapon?.CriticalFormula,
                (entity, value) => EnsureWeapon(entity).CriticalFormula = value,
                "Обычный урон доступен в переменной «урон». Например: урон * 2")
            .WithEquipmentSlot()
            .Requirement("requirements", "Требования", entity => entity.Requirements, (entity, value) => entity.Requirements = value)
            .WithBonuses()
            .Build();

    private static IContentTypeDescriptor CreateEquipmentSlots() =>
        new ContentTypeBuilder<EquipmentSlot>(ContentTypeIds.EquipmentSlots, "Слоты экипировки", "Слот экипировки")
            // Слоты экипировки должны идти раньше предметов и оружия (130 и 140):
            // и те и другие могут ссылаться на слот по идентификатору, а расширения
            // устанавливают виды контента в порядке этого списка (решение Р-103).
            // Слот, установленный после предмета, ссылающегося на него, столкнулся
            // бы с внешним ключом, которого ещё нет в базе.
            .Describe("Место, в которое надевается предмет. Пользователь может создавать собственные слоты.", 127)
            .WithCommonFields()
            .Boolean("allowMultiple", "Допускает несколько предметов", entity => entity.AllowMultiple, (entity, value) => entity.AllowMultiple = value)
            .Integer("maximumItems", "Максимум предметов", entity => entity.MaximumItems, (entity, value) => entity.MaximumItems = value)
            .Integer("sortOrder", "Порядок отображения", entity => entity.SortOrder, (entity, value) => entity.SortOrder = value)
            .Build();

    /// <summary>
    /// Описание видов отдыха.
    ///
    /// Короткий отдых, длительный отдых и любой другой создаются здесь и ничем
    /// не отличаются друг от друга: приложение не знает ни одного вида отдыха
    /// заранее, поэтому система с тремя видами отдыха или без отдыха вовсе
    /// работает без изменения кода.
    /// </summary>
    /// <returns>Описание вида контента.</returns>
    private static IContentTypeDescriptor CreateRestTypes() =>
        new ContentTypeBuilder<RestType>(ContentTypeIds.RestTypes, "Виды отдыха", "Вид отдыха")
            .Describe(
                "Короткий отдых, длительный отдых или собственный: что он восстанавливает "
                + "и сколько времени занимает.",
                152)
            .Including(query => query.Include(rest => rest.Restores))
            .WithCommonFields()
            .OptionalNumber("duration", "Длительность", entity => entity.Duration,
                (entity, value) => entity.Duration = value, ContentFieldGroups.Rules,
                "Например: 1, 8. Пусто — отдых не занимает игрового времени")
            .Text("durationUnit", "Единица длительности", entity => entity.DurationUnit,
                (entity, value) => entity.DurationUnit = value, ContentFieldGroups.Rules,
                hint: "Например: час, минута, раунд. На эту длительность идёт время эффектов")
            .Requirement("requirements", "Требования", entity => entity.Requirements,
                (entity, value) => entity.Requirements = value)
            .Integer("sortOrder", "Порядок отображения", entity => entity.SortOrder,
                (entity, value) => entity.SortOrder = value)
            .WithRestRestores()
            .Build();

    /// <summary>
    /// Добавляет виду отдыха список восстановлений.
    /// </summary>
    /// <param name="builder">Построитель описания вида.</param>
    /// <returns>Тот же построитель.</returns>
    private static ContentTypeBuilder<RestType> WithRestRestores(this ContentTypeBuilder<RestType> builder) =>
        builder.Collection<RestRestore>(
            "restores",
            "Что восстанавливает отдых",
            "Восстановление",
            entity => entity.Restores,
            restores => restores
                .Describe(
                    "Ресурс не выбран — восстанавливаются все ресурсы персонажа. "
                    + "Формула видит максимум и текущее значение ресурса.")
                .AttachedBy(
                    (restore, rest) => restore.RestTypeId = rest.Id,
                    restore => restore.SortOrder)
                .Reference(
                    "resource",
                    "Ресурс",
                    ContentTypeIds.Resources,
                    restore => restore.ResourceId,
                    (restore, value) => restore.ResourceId = value)
                .Enumeration(
                    "mode",
                    "Насколько восстанавливается",
                    RestRestoreModes.All,
                    restore => RestRestoreModes.ToText(restore.Mode),
                    (restore, value) => restore.Mode = RestRestoreModes.Parse(value))
                .Formula(
                    "formula",
                    "Формула величины",
                    restore => restore.Formula,
                    (restore, value) => restore.Formula = value,
                    "Например: максимум / 2, уровень, 10. Нужна при восстановлении по формуле")
                .Formula(
                    "condition",
                    "Условие",
                    restore => restore.Condition,
                    (restore, value) => restore.Condition = value,
                    "Пусто — восстановление происходит всегда")
                .Integer(
                    "sortOrder",
                    "Порядок",
                    restore => restore.SortOrder,
                    (restore, value) => restore.SortOrder = value));

    private static IContentTypeDescriptor CreateDieTypes() =>
        new ContentTypeBuilder<DieType>(ContentTypeIds.DieTypes, "Кубики", "Кубик")
            .Describe(
                "Кубик, которого нет среди привычных: d3, d50 или «Кристалл судьбы» d777. "
                + "Кубики от d2 до d100 доступны в панели бросков всегда.",
                155)
            .CreatedBy(() => new DieType { Sides = DefaultDieSides })
            .WithCommonFields()
            .Integer("sides", "Количество граней", entity => entity.Sides, (entity, value) => entity.Sides = value,
                ContentFieldGroups.Rules, "Не меньше двух. Например: 3, 50, 777")
            .Integer("sortOrder", "Порядок отображения", entity => entity.SortOrder,
                (entity, value) => entity.SortOrder = value)
            .Color("color", "Цвет кубика", entity => entity.Color, (entity, value) => entity.Color = value)
            .Build();

    private static IContentTypeDescriptor CreateMonsters() =>
        new ContentTypeBuilder<Monster>(ContentTypeIds.Monsters, "Монстры", "Монстр")
            .Describe("Противник или существо, используемое мастером.", 160)
            .WithCommonFields()
            .Text("creatureType", "Тип существа", entity => entity.CreatureType, (entity, value) => entity.CreatureType = value, ContentFieldGroups.Rules)
            .Text("challenge", "Уровень опасности", entity => entity.Challenge, (entity, value) => entity.Challenge = value, ContentFieldGroups.Rules)
            .LongText("statBlock", "Характеристики", entity => entity.StatBlockJson, (entity, value) => entity.StatBlockJson = value, ContentFieldGroups.Rules)
            .Build();

    private static IContentTypeDescriptor CreateLocations() =>
        new ContentTypeBuilder<Location>(ContentTypeIds.Locations, "Локации", "Локация")
            .Describe(
                "Место игрового мира: город, замок, подземелье, планета или станция. "
                + "Локации вкладываются друг в друга: город содержит район, район — здание.",
                165)
            .WithCommonFields()
            .Text("kind", "Вид локации", entity => entity.Kind, (entity, value) => entity.Kind = value,
                ContentFieldGroups.Rules, hint: "Например: город, замок, планета, корабль — перечень задаёте вы")
            .Reference(
                "parentLocation",
                "Входит в локацию",
                ContentTypeIds.Locations,
                entity => entity.ParentLocationId,
                (entity, value) => entity.ParentLocationId = value)
            .Build();

    private static IContentTypeDescriptor CreateNpcs() =>
        new ContentTypeBuilder<Npc>(ContentTypeIds.Npcs, "NPC", "NPC")
            .Describe(
                "Неигровой персонаж: житель мира, которым управляет мастер. "
                + "Один и тот же NPC участвует в нескольких кампаниях.",
                170)
            .WithCommonFields()
            .Text("role", "Роль", entity => entity.Role, (entity, value) => entity.Role = value,
                ContentFieldGroups.Rules, hint: "Например: торговец, наставник, правитель")
            .Text("attitude", "Отношение к игрокам", entity => entity.Attitude,
                (entity, value) => entity.Attitude = value, ContentFieldGroups.Rules,
                hint: "Например: союзник, нейтрален, враг")
            .Reference(
                "location",
                "Находится в локации",
                ContentTypeIds.Locations,
                entity => entity.LocationId,
                (entity, value) => entity.LocationId = value)
            .Build();

    private static IContentTypeDescriptor CreateQuests() =>
        new ContentTypeBuilder<Quest>(ContentTypeIds.Quests, "Квесты", "Квест")
            .Describe("Задание для игроков: кто выдал, где выполняется, из каких этапов состоит.", 175)
            .Including(query => query.Include(quest => quest.Steps))
            .WithCommonFields()
            .Enumeration(
                "status",
                "Состояние",
                QuestStatuses.All,
                entity => QuestStatuses.ToText(entity.Status),
                (entity, value) => entity.Status = QuestStatuses.Parse(value))
            .Text("reward", "Награда", entity => entity.Reward, (entity, value) => entity.Reward = value,
                ContentFieldGroups.Rules, hint: "Например: 500 золотых, репутация +20, редкий предмет")
            .Reference(
                "giver",
                "Кто выдал задание",
                ContentTypeIds.Npcs,
                entity => entity.GiverId,
                (entity, value) => entity.GiverId = value)
            .Reference(
                "location",
                "Локация задания",
                ContentTypeIds.Locations,
                entity => entity.LocationId,
                (entity, value) => entity.LocationId = value)
            .WithQuestSteps()
            .Build();

    /// <summary>
    /// Добавляет квесту список этапов.
    /// </summary>
    /// <param name="builder">Построитель описания вида.</param>
    /// <returns>Тот же построитель.</returns>
    private static ContentTypeBuilder<Quest> WithQuestSteps(this ContentTypeBuilder<Quest> builder) =>
        builder.Collection<QuestStep>(
            "steps",
            "Этапы задания",
            "Этап",
            entity => entity.Steps,
            steps => steps
                .Describe("Этапы выполняются в указанном порядке. Отметка сохраняется вместе с квестом.")
                .AttachedBy(
                    (step, quest) => step.QuestId = quest.Id,
                    step => step.SortOrder)
                .Text("title", "Название", step => step.Title, (step, value) => step.Title = value ?? string.Empty)
                .Text("description", "Описание", step => step.Description, (step, value) => step.Description = value)
                .Boolean("isDone", "Выполнен", step => step.IsDone, (step, value) => step.IsDone = value)
                .Integer("sortOrder", "Порядок", step => step.SortOrder, (step, value) => step.SortOrder = value));

    /// <summary>
    /// Возвращает оружейные свойства предмета, создавая их при первом обращении.
    /// </summary>
    /// <param name="item">Предмет.</param>
    /// <returns>Оружейные свойства.</returns>
    private static Weapon EnsureWeapon(Item item) => item.Weapon ??= new Weapon { ItemId = item.Id };
}

/// <summary>
/// Названия окраски эффекта, отображаемые в редакторе.
/// </summary>
internal static class EffectTones
{
    /// <summary>Положительный эффект.</summary>
    public const string Positive = "Положительная";

    /// <summary>Отрицательный эффект.</summary>
    public const string Negative = "Отрицательная";

    /// <summary>Нейтральный эффект.</summary>
    public const string Neutral = "Нейтральная";

    /// <summary>Все допустимые значения в порядке отображения.</summary>
    public static IReadOnlyList<string> All { get; } = [Positive, Negative, Neutral];

    /// <summary>
    /// Возвращает название окраски эффекта.
    /// </summary>
    /// <param name="tone">Окраска эффекта.</param>
    /// <returns>Название для редактора.</returns>
    public static string ToText(EffectTone tone) => tone switch
    {
        EffectTone.Negative => Negative,
        EffectTone.Neutral => Neutral,
        _ => Positive,
    };

    /// <summary>
    /// Разбирает название окраски эффекта.
    /// </summary>
    /// <param name="text">Название, выбранное пользователем.</param>
    /// <returns>Окраска эффекта.</returns>
    public static EffectTone Parse(string? text) => text switch
    {
        Negative => EffectTone.Negative,
        Neutral => EffectTone.Neutral,
        _ => EffectTone.Positive,
    };
}

/// <summary>
/// Названия правил повторного наложения эффекта, отображаемые в редакторе.
/// </summary>
internal static class EffectStackings
{
    /// <summary>Повторное наложение обновляет длительность.</summary>
    public const string Refresh = "Обновляет длительность";

    /// <summary>Наложения складываются.</summary>
    public const string Sum = "Складывается";

    /// <summary>Повторное наложение запрещено.</summary>
    public const string Forbidden = "Запрещено";

    /// <summary>Все допустимые значения в порядке отображения.</summary>
    public static IReadOnlyList<string> All { get; } = [Refresh, Sum, Forbidden];

    /// <summary>
    /// Возвращает название правила наложения.
    /// </summary>
    /// <param name="stacking">Правило наложения.</param>
    /// <returns>Название для редактора.</returns>
    public static string ToText(EffectStacking stacking) => stacking switch
    {
        EffectStacking.Sum => Sum,
        EffectStacking.Forbidden => Forbidden,
        _ => Refresh,
    };

    /// <summary>
    /// Разбирает название правила наложения.
    /// </summary>
    /// <param name="text">Название, выбранное пользователем.</param>
    /// <returns>Правило наложения.</returns>
    public static EffectStacking Parse(string? text) => text switch
    {
        Sum => EffectStacking.Sum,
        Forbidden => EffectStacking.Forbidden,
        _ => EffectStacking.Refresh,
    };
}

/// <summary>
/// Названия расхода при использовании предмета, отображаемые в редакторе.
///
/// Перечисление хранится в базе числом, а пользователь выбирает его словом,
/// поэтому преобразование собрано в одном месте.
/// </summary>
internal static class ItemUseCosts
{
    /// <summary>Использование ничего не расходует.</summary>
    public const string None = "Ничего";

    /// <summary>Использование расходует заряд предмета.</summary>
    public const string Charge = "Заряд";

    /// <summary>Использование расходует единицу предмета.</summary>
    public const string Unit = "Единицу предмета";

    /// <summary>Все допустимые значения в порядке отображения.</summary>
    public static IReadOnlyList<string> All { get; } = [None, Charge, Unit];

    /// <summary>
    /// Возвращает название расхода при использовании.
    /// </summary>
    /// <param name="cost">Расход при использовании.</param>
    /// <returns>Название для редактора.</returns>
    public static string ToText(ItemUseCost cost) => cost switch
    {
        ItemUseCost.Charge => Charge,
        ItemUseCost.Unit => Unit,
        _ => None,
    };

    /// <summary>
    /// Разбирает название расхода при использовании.
    /// </summary>
    /// <param name="text">Название, выбранное пользователем.</param>
    /// <returns>Расход при использовании.</returns>
    public static ItemUseCost Parse(string? text) => text switch
    {
        Charge => ItemUseCost.Charge,
        Unit => ItemUseCost.Unit,
        _ => ItemUseCost.None,
    };
}

/// <summary>
/// Названия целей бонуса экипировки, показываемые в редакторе контента.
/// </summary>
internal static class BonusTargets
{
    /// <summary>Бонус изменяет характеристику.</summary>
    public const string Attribute = "Характеристика";

    /// <summary>Бонус изменяет максимум ресурса.</summary>
    public const string Resource = "Ресурс";

    /// <summary>Бонус задаёт именованную величину.</summary>
    public const string Variable = "Величина";

    /// <summary>Бонус добавляет признак.</summary>
    public const string Tag = "Признак";

    /// <summary>Все допустимые цели бонуса в порядке отображения.</summary>
    public static IReadOnlyList<string> All { get; } = [Attribute, Resource, Variable, Tag];

    /// <summary>
    /// Возвращает название цели бонуса.
    /// </summary>
    /// <param name="kind">Цель бонуса.</param>
    /// <returns>Название для редактора.</returns>
    public static string ToText(BonusTargetKind kind) => kind switch
    {
        BonusTargetKind.Resource => Resource,
        BonusTargetKind.Variable => Variable,
        BonusTargetKind.Tag => Tag,
        _ => Attribute,
    };

    /// <summary>
    /// Разбирает название цели бонуса.
    /// </summary>
    /// <param name="text">Название, выбранное пользователем.</param>
    /// <returns>Цель бонуса.</returns>
    public static BonusTargetKind Parse(string? text) => text switch
    {
        Resource => BonusTargetKind.Resource,
        Variable => BonusTargetKind.Variable,
        Tag => BonusTargetKind.Tag,
        _ => BonusTargetKind.Attribute,
    };
}

/// <summary>
/// Названия способов восстановления при отдыхе, отображаемые в редакторе.
/// </summary>
internal static class RestRestoreModes
{
    /// <summary>Ресурс восстанавливается до максимума.</summary>
    public const string Full = "Полностью";

    /// <summary>Восстанавливается величина, вычисленная формулой.</summary>
    public const string Formula = "По формуле";

    /// <summary>Все допустимые значения в порядке отображения.</summary>
    public static IReadOnlyList<string> All { get; } = [Full, Formula];

    /// <summary>
    /// Возвращает название способа восстановления.
    /// </summary>
    /// <param name="mode">Способ восстановления.</param>
    /// <returns>Название для редактора.</returns>
    public static string ToText(RestRestoreMode mode) => mode switch
    {
        RestRestoreMode.Formula => Formula,
        _ => Full,
    };

    /// <summary>
    /// Разбирает название способа восстановления.
    /// </summary>
    /// <param name="text">Название, выбранное пользователем.</param>
    /// <returns>Способ восстановления.</returns>
    public static RestRestoreMode Parse(string? text) => text switch
    {
        Formula => RestRestoreMode.Formula,
        _ => RestRestoreMode.Full,
    };
}

/// <summary>
/// Названия состояний квеста, отображаемые в редакторе.
/// </summary>
internal static class QuestStatuses
{
    /// <summary>Задание ещё не выдано игрокам.</summary>
    public const string Planned = "Не начат";

    /// <summary>Задание выполняется.</summary>
    public const string Active = "В работе";

    /// <summary>Задание выполнено.</summary>
    public const string Completed = "Выполнен";

    /// <summary>Задание провалено.</summary>
    public const string Failed = "Провален";

    /// <summary>Все допустимые значения в порядке отображения.</summary>
    public static IReadOnlyList<string> All { get; } = [Planned, Active, Completed, Failed];

    /// <summary>
    /// Возвращает название состояния квеста.
    /// </summary>
    /// <param name="status">Состояние квеста.</param>
    /// <returns>Название для редактора.</returns>
    public static string ToText(QuestStatus status) => status switch
    {
        QuestStatus.Active => Active,
        QuestStatus.Completed => Completed,
        QuestStatus.Failed => Failed,
        _ => Planned,
    };

    /// <summary>
    /// Разбирает название состояния квеста.
    /// </summary>
    /// <param name="text">Название, выбранное пользователем.</param>
    /// <returns>Состояние квеста.</returns>
    public static QuestStatus Parse(string? text) => text switch
    {
        Active => QuestStatus.Active,
        Completed => QuestStatus.Completed,
        Failed => QuestStatus.Failed,
        _ => QuestStatus.Planned,
    };
}
