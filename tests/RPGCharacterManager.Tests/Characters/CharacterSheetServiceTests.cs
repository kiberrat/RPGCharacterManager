using RPGCharacterManager.Characters;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Tests.Characters;

/// <summary>
/// Проверка листа персонажа: отображения вычисленных значений, сохранения
/// изменений с полным пересчётом и пользовательских полей.
/// </summary>
public sealed class CharacterSheetServiceTests
{
    private const string HalfOfValue = "ОкруглитьВниз((значение - 10) / 2)";

    private static async Task<Guid> CreateCharacterAsync(
        CharacterTestContext context,
        string name = "Герой",
        int level = 1)
    {
        var draft = new CharacterDraft { Level = level };
        draft.Character.Name = name;

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static async Task<CharacterSheet> LoadAsync(CharacterTestContext context, Guid characterId)
    {
        var sheet = await context.Sheets.LoadAsync(characterId);
        Assert.True(sheet.IsSuccess, sheet.Error);

        return sheet.Value;
    }

    [Fact]
    public async Task Лист_ПоказываетВычисленныеЗначенияХарактеристик()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute(
            "Сила",
            "сила",
            defaultValue: 16,
            modifierFormula: HalfOfValue);

        strength.Category = "Основные";
        await context.AddAsync(strength);

        var characterId = await CreateCharacterAsync(context);
        var sheet = await LoadAsync(context, characterId);

        var attribute = Assert.Single(sheet.Attributes);

        Assert.Equal("Сила", attribute.Name);
        Assert.Equal("Основные", attribute.Category);
        Assert.Equal(16, attribute.Value);
        Assert.Equal(3, attribute.Modifier);
        Assert.False(attribute.IsDerived);
    }

    [Fact]
    public async Task Лист_ПоказываетХарактеристикуСозданнуюПослеПерсонажа()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        // Характеристика добавлена в игровую систему уже после создания персонажа.
        await context.AddAsync(CharacterContent.Attribute("Удача", "удача", defaultValue: 7));

        var sheet = await LoadAsync(context, characterId);

        Assert.Equal(7, Assert.Single(sheet.Attributes).Value);
    }

    [Fact]
    public async Task Сохранение_ПересчитываетЗависимыеЗначения()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute(
            "Сила",
            "сила",
            defaultValue: 10,
            modifierFormula: HalfOfValue);

        await context.AddAsync(strength);
        await context.AddAsync(CharacterContent.Resource("Здоровье", "здоровье", "10 + сила"));

        var characterId = await CreateCharacterAsync(context);
        var sheet = await LoadAsync(context, characterId);

        // Игрок изменил базовое значение характеристики прямо на листе.
        sheet.Character.Attributes.Single().BaseValue = 18;

        var saved = await context.Sheets.SaveAsync(sheet.Character, new Dictionary<Guid, string?>());
        Assert.True(saved.IsSuccess, saved.Error);

        var attribute = Assert.Single(saved.Value.Attributes);
        Assert.Equal(18, attribute.Value);
        Assert.Equal(4, attribute.Modifier);

        Assert.Equal(28, Assert.Single(saved.Value.Resources).Maximum);
    }

    [Fact]
    public async Task АвторскийБонусМастерства_СохраняетсяПересчитываетНавыкИСбрасывается()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var agility = CharacterContent.Attribute(
            "Ловкость",
            "ловкость",
            defaultValue: 14,
            modifierFormula: HalfOfValue);
        var proficiencyBonus = CharacterContent.Attribute(
            "Бонус мастерства",
            "бонус_мастерства",
            formula: "2 + ОкруглитьВниз((уровень - 1) / 4)");
        proficiencyBonus.IsHidden = true;
        var stealth = CharacterContent.Skill(
            "Скрытность",
            "скрытность",
            agility.Id,
            formula: "характеристика + владение * бонус_мастерства");

        await context.AddAsync(agility, proficiencyBonus);
        await context.AddAsync(stealth);

        var characterId = await CreateCharacterAsync(context);
        var sheet = await LoadAsync(context, characterId);

        var bonusValue = sheet.Character.Attributes.Single(value => value.AttributeId == proficiencyBonus.Id);
        bonusValue.OverrideValue = 9;
        sheet.Character.Skills.Add(new CharacterSkill
        {
            CharacterId = characterId,
            SkillId = stealth.Id,
            ProficiencyLevel = 1,
        });

        var saved = await context.Sheets.SaveAsync(sheet.Character, new Dictionary<Guid, string?>());
        Assert.True(saved.IsSuccess, saved.Error);
        Assert.True(saved.Value.Attributes.Single(attribute => attribute.Id == proficiencyBonus.Id).IsHidden);
        Assert.Equal(9, saved.Value.Attributes.Single(attribute => attribute.Id == proficiencyBonus.Id).Value);
        Assert.Equal(11, Assert.Single(saved.Value.Skills).Value);

        var reloaded = await LoadAsync(context, characterId);
        var persisted = reloaded.Character.Attributes.Single(value => value.AttributeId == proficiencyBonus.Id);
        Assert.Equal(9, persisted.OverrideValue);

        persisted.OverrideValue = null;
        var reset = await context.Sheets.SaveAsync(reloaded.Character, new Dictionary<Guid, string?>());
        Assert.True(reset.IsSuccess, reset.Error);
        Assert.Equal(2, reset.Value.Attributes.Single(attribute => attribute.Id == proficiencyBonus.Id).Value);
        Assert.Equal(4, Assert.Single(reset.Value.Skills).Value);
    }

    [Fact]
    public async Task Сохранение_СохраняетОписаниеПерсонажа()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);
        var sheet = await LoadAsync(context, characterId);

        sheet.Character.Biography = "Родился в горах.";
        sheet.Character.Notes = "Ищет брата.";
        sheet.Character.Portrait = "portrait.png";

        var saved = await context.Sheets.SaveAsync(sheet.Character, new Dictionary<Guid, string?>());
        Assert.True(saved.IsSuccess, saved.Error);

        var reloaded = await LoadAsync(context, characterId);

        Assert.Equal("Родился в горах.", reloaded.Character.Biography);
        Assert.Equal("Ищет брата.", reloaded.Character.Notes);
        Assert.Equal("portrait.png", reloaded.Character.Portrait);
    }

    [Fact]
    public async Task Сохранение_БезИмени_ЗавершаетсяОшибкой()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);
        var sheet = await LoadAsync(context, characterId);

        sheet.Character.Name = "   ";

        var saved = await context.Sheets.SaveAsync(sheet.Character, new Dictionary<Guid, string?>());

        Assert.True(saved.IsFailure);
    }

    [Fact]
    public async Task Навык_ДобавляетсяИУдаляетсяНаЛисте()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var agility = CharacterContent.Attribute(
            "Ловкость",
            "ловкость",
            defaultValue: 18,
            modifierFormula: HalfOfValue);

        await context.AddAsync(agility);

        var stealth = CharacterContent.Skill("Скрытность", "скрытность", agility.Id);
        stealth.Category = "Физические";
        await context.AddAsync(stealth);

        var characterId = await CreateCharacterAsync(context);
        var sheet = await LoadAsync(context, characterId);

        Assert.Empty(sheet.Skills);

        sheet.Character.Skills.Add(new CharacterSkill
        {
            CharacterId = characterId,
            SkillId = stealth.Id,
            ProficiencyLevel = 1,
        });

        var saved = await context.Sheets.SaveAsync(sheet.Character, new Dictionary<Guid, string?>());
        Assert.True(saved.IsSuccess, saved.Error);

        var skill = Assert.Single(saved.Value.Skills);
        Assert.Equal("Скрытность", skill.Name);
        Assert.Equal("Физические", skill.Category);
        Assert.Equal(4, skill.Value);

        saved.Value.Character.Skills.Clear();

        var removed = await context.Sheets.SaveAsync(saved.Value.Character, new Dictionary<Guid, string?>());
        Assert.True(removed.IsSuccess, removed.Error);

        Assert.Empty(removed.Value.Skills);
    }

    [Fact]
    public async Task Спасбросок_ЯвляетсяНавыкомСвоейКатегории()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var save = CharacterContent.Skill("Стойкость", "стойкость");
        save.Category = SheetCategories.SavingThrows;
        await context.AddAsync(save);

        var characterId = await CreateCharacterAsync(context);
        var sheet = await LoadAsync(context, characterId);

        sheet.Character.Skills.Add(new CharacterSkill
        {
            CharacterId = characterId,
            SkillId = save.Id,
            ProficiencyLevel = 1,
        });

        var saved = await context.Sheets.SaveAsync(sheet.Character, new Dictionary<Guid, string?>());
        Assert.True(saved.IsSuccess, saved.Error);

        Assert.Equal(SheetCategories.SavingThrows, Assert.Single(saved.Value.Skills).Category);
    }

    [Fact]
    public async Task Черта_ДобавляетсяНаЛистеИСохраняетСостояние()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var trait = CharacterContent.Trait("Ночное зрение", "ночное_зрение");
        trait.Category = "Расовые";
        await context.AddAsync(trait);

        var characterId = await CreateCharacterAsync(context);
        var sheet = await LoadAsync(context, characterId);

        sheet.Character.Traits.Add(new CharacterTrait
        {
            CharacterId = characterId,
            TraitId = trait.Id,
            Source = "Лист персонажа",
            IsActive = false,
            RemainingUses = 3,
        });

        var saved = await context.Sheets.SaveAsync(sheet.Character, new Dictionary<Guid, string?>());
        Assert.True(saved.IsSuccess, saved.Error);

        var row = Assert.Single(saved.Value.Traits);

        Assert.Equal("Ночное зрение", row.Name);
        Assert.Equal("Расовые", row.Category);
        Assert.Equal("Лист персонажа", row.Source);
        Assert.False(row.IsActive);
        Assert.Equal(3, row.RemainingUses);
    }

    [Fact]
    public async Task Черта_СНарушеннымиТребованиями_ПоказываетПричину()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 8);
        await context.AddAsync(strength);

        var trait = CharacterContent.Trait("Силач", "силач", requirements: "сила >= 15");
        await context.AddAsync(trait);

        var characterId = await CreateCharacterAsync(context);
        var sheet = await LoadAsync(context, characterId);

        sheet.Character.Traits.Add(new CharacterTrait
        {
            CharacterId = characterId,
            TraitId = trait.Id,
        });

        var saved = await context.Sheets.SaveAsync(sheet.Character, new Dictionary<Guid, string?>());
        Assert.True(saved.IsSuccess, saved.Error);

        var row = Assert.Single(saved.Value.Traits);

        Assert.False(row.IsAvailable);
        Assert.Contains("сила >= 15", row.UnavailableReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Способности_ОтбираютсяПоВыполненнымТребованиям()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var warrior = CharacterContent.Class("Воин", "воин");
        var mage = CharacterContent.Class("Маг", "маг");
        await context.AddAsync(warrior, mage);

        await context.AddAsync(
            new Ability
            {
                Name = "Второе дыхание",
                SystemName = "второе_дыхание",
                Category = "Классовые",
                Requirements = "класс = \"воин\"",
            },
            new Ability
            {
                Name = "Магический выброс",
                SystemName = "магический_выброс",
                Category = "Классовые",
                Requirements = "класс = \"маг\"",
            },
            new Ability
            {
                Name = "Обычное действие",
                SystemName = "обычное_действие",
                Category = "Общие",
            });

        var draft = new CharacterDraft();
        draft.Character.Name = "Аргус";
        context.Builder.SetSelection(context.Step(CharacterStepIds.Class), draft, warrior.Id);

        var created = await context.Builder.CreateAsync(draft);
        Assert.True(created.IsSuccess, created.Error);

        var sheet = await LoadAsync(context, created.Value);

        var names = sheet.Abilities.Select(ability => ability.Name).Order().ToArray();

        // Классовая способность определяется требованием, а не отдельной связью,
        // поэтому персонаж-воин не получает способности мага.
        Assert.Equal(["Второе дыхание", "Обычное действие"], names);
    }

    [Fact]
    public async Task ПользовательскоеПоле_СоздаётсяИХранитЗначение()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var definition = new PropertyDefinition
        {
            DisplayName = "Радиация",
            DataType = GameValueType.WholeNumber,
            Category = "Домашние правила",
        };

        var created = await context.Sheets.SaveCustomFieldAsync(definition);
        Assert.True(created.IsSuccess, created.Error);

        var sheet = await LoadAsync(context, characterId);
        var field = Assert.Single(sheet.CustomFields);

        Assert.Equal("Радиация", field.DisplayName);
        Assert.Equal("Домашние правила", field.Category);

        var saved = await context.Sheets.SaveAsync(
            sheet.Character,
            new Dictionary<Guid, string?> { [field.DefinitionId] = "42" });

        Assert.True(saved.IsSuccess, saved.Error);

        var reloaded = await LoadAsync(context, characterId);

        Assert.Equal("42", Assert.Single(reloaded.CustomFields).Value);
    }

    [Fact]
    public async Task ПользовательскоеПоле_УдаляетсяВместеСоЗначениями()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var definition = new PropertyDefinition
        {
            DisplayName = "Честь",
            DataType = GameValueType.WholeNumber,
        };

        await context.Sheets.SaveCustomFieldAsync(definition);

        var sheet = await LoadAsync(context, characterId);

        await context.Sheets.SaveAsync(
            sheet.Character,
            new Dictionary<Guid, string?> { [definition.Id] = "5" });

        var deleted = await context.Sheets.DeleteCustomFieldAsync(definition.Id);
        Assert.True(deleted.IsSuccess, deleted.Error);

        Assert.Empty((await LoadAsync(context, characterId)).CustomFields);
    }

    [Fact]
    public async Task ДоступныеЧерты_ИсключаютНеподходящиеТребованиям()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        await context.AddAsync(strength);

        await context.AddAsync(
            CharacterContent.Trait("Доступная", "доступная"),
            CharacterContent.Trait("Недоступная", "недоступная", requirements: "сила >= 18"));

        var characterId = await CreateCharacterAsync(context);
        var sheet = await LoadAsync(context, characterId);

        var available = await context.Sheets
            .GetAvailableTraitsAsync(sheet.Character, search: null, includeUnavailable: false);

        Assert.Equal("Доступная", Assert.Single(available.Options).Name);
    }

    [Fact]
    public async Task Ресурс_СохраняетТекущееЗначениеИОграничиваетсяМаксимумом()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        await context.AddAsync(CharacterContent.Resource("Здоровье", "здоровье", "20"));

        var characterId = await CreateCharacterAsync(context);
        var sheet = await LoadAsync(context, characterId);

        sheet.Character.Resources.Single().Current = 7;

        var saved = await context.Sheets.SaveAsync(sheet.Character, new Dictionary<Guid, string?>());
        Assert.True(saved.IsSuccess, saved.Error);

        Assert.Equal(7, Assert.Single(saved.Value.Resources).Current);

        // Значение выше максимума приводится к нему при пересчёте.
        saved.Value.Character.Resources.Single().Current = 500;

        var clamped = await context.Sheets.SaveAsync(saved.Value.Character, new Dictionary<Guid, string?>());
        Assert.True(clamped.IsSuccess, clamped.Error);

        Assert.Equal(20, Assert.Single(clamped.Value.Resources).Current);
    }

    [Fact]
    public async Task АвторскаяСпособность_ХранитУсловиеИСвязанаСТекущимПерсонажем()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context, level: 3);
        var ability = new CharacterCustomAbility
        {
            Name = "Громовой рывок",
            Description = "Авторская способность героя.",
            Formula = "1к8 + сила",
            Requirements = "уровень >= 5",
            DependencyDescription = "Минимальный уровень: 5",
        };

        var saved = await context.Sheets.SaveCustomAbilityAsync(characterId, ability);
        Assert.True(saved.IsSuccess, saved.Error);

        var sheet = await LoadAsync(context, characterId);
        var row = Assert.Single(sheet.Abilities);
        Assert.True(row.IsCustom);
        Assert.False(row.IsAvailable);
        Assert.Contains("уровень >= 5", row.UnavailableReason, StringComparison.Ordinal);
        Assert.Equal("1к8 + сила", row.Formula);

        sheet.Character.Level = 5;
        var recalculated = await context.Sheets.SaveAsync(
            sheet.Character,
            new Dictionary<Guid, string?>());
        Assert.True(recalculated.IsSuccess, recalculated.Error);
        Assert.True(Assert.Single(recalculated.Value.Abilities).IsAvailable);

        var deleted = await context.Sheets.DeleteCustomAbilityAsync(characterId, ability.Id);
        Assert.True(deleted.IsSuccess, deleted.Error);
        Assert.Empty((await LoadAsync(context, characterId)).Abilities);
    }

    [Fact]
    public async Task Деньги_ДобавляютсяОбновляютсяИСохраняютсяМеждуЗагрузками()
    {
        await using var context = await CharacterTestContext.CreateAsync();
        var characterId = await CreateCharacterAsync(context, level: 1);
        var currency = new CharacterCurrency
        {
            Name = "Золотые монеты",
            Amount = 2500,
        };

        // EntityBase сразу создаёт Guid: это новая запись, хотя Id уже не пустой.
        Assert.NotEqual(Guid.Empty, currency.Id);
        var added = await context.Sheets.SaveCurrencyAsync(characterId, currency);
        Assert.True(added.IsSuccess, added.Error);

        var loaded = await LoadAsync(context, characterId);
        var stored = Assert.Single(loaded.Character.Currencies);
        Assert.Equal(currency.Id, stored.Id);
        Assert.Equal("Золотые монеты", stored.Name);
        Assert.Equal(2500m, stored.Amount);

        stored.Name = "Монеты";
        stored.Amount = 2750;
        var updated = await context.Sheets.SaveCurrencyAsync(characterId, stored);
        Assert.True(updated.IsSuccess, updated.Error);

        var reloaded = await LoadAsync(context, characterId);
        var persisted = Assert.Single(reloaded.Character.Currencies);
        Assert.Equal("Монеты", persisted.Name);
        Assert.Equal(2750m, persisted.Amount);
    }

    [Fact]
    public async Task Мана_СохраняетТекущееЗначениеИНеобязательныйМаксимум()
    {
        await using var context = await CharacterTestContext.CreateAsync();
        var characterId = await CreateCharacterAsync(context, level: 1);

        var saved = await context.Sheets.SaveManaAsync(characterId, 17.5m, 42m);
        Assert.True(saved.IsSuccess, saved.Error);
        var loaded = await LoadAsync(context, characterId);
        Assert.Equal(17.5m, loaded.Character.Mana);
        Assert.Equal(42m, loaded.Character.ManaMaximum);

        var withoutMaximum = await context.Sheets.SaveManaAsync(characterId, 9m, null);
        Assert.True(withoutMaximum.IsSuccess, withoutMaximum.Error);
        var reloaded = await LoadAsync(context, characterId);
        Assert.Equal(9m, reloaded.Character.Mana);
        Assert.Null(reloaded.Character.ManaMaximum);
    }

    [Fact]
    public async Task Dnd5eSubclassAbilities_AreSeparateAndFollowCharacterLevel()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var dnd5e = new Guid("5bb844d5-4e9c-4cd8-97b5-1f2e978d3675");
        await context.AddAsync(new GameSystem { Id = dnd5e, Name = "D&D 5e", SystemName = "dnd5e" });

        var bard = CharacterContent.Class("Бард", "бард");
        bard.GameSystemId = dnd5e;
        await context.AddAsync(bard);

        var eloquence = CharacterContent.Subclass(
            "Коллегия красноречия", "коллегия_красноречия", bard.Id, availableAtLevel: 3);
        eloquence.GameSystemId = dnd5e;
        eloquence.Description =
            "Златоуст 3-й уровень, умение коллегии красноречия Вы мастер говорить нужные вещи. " +
            "Универсальная речь 6-й уровень, умение коллегии красноречия Вы можете сделать речь понятной всем.";
        await context.AddAsync(eloquence);

        var draft = new CharacterDraft { Level = 3 };
        draft.Character.Name = "Люциус";
        draft.Character.GameSystemId = dnd5e;
        context.Builder.SetSelection(context.Step(CharacterStepIds.Class), draft, bard.Id);
        context.Builder.SetSelection(context.Step(CharacterStepIds.Subclass), draft, eloquence.Id);

        var created = await context.Builder.CreateAsync(draft);
        Assert.True(created.IsSuccess, created.Error);

        var levelThree = await LoadAsync(context, created.Value);
        var subclassAbility = Assert.Single(levelThree.Abilities.Where(ability =>
            ability.Category.StartsWith("Способности подкласса", StringComparison.Ordinal)));
        Assert.Equal("Златоуст", subclassAbility.Name);
        Assert.DoesNotContain(levelThree.Abilities, ability => ability.Name == "Универсальная речь");

        levelThree.Character.Level = 6;
        var levelSix = await context.Sheets.SaveAsync(
            levelThree.Character, new Dictionary<Guid, string?>());
        Assert.True(levelSix.IsSuccess, levelSix.Error);
        Assert.Contains(levelSix.Value.Abilities, ability => ability.Name == "Универсальная речь");
    }

}
