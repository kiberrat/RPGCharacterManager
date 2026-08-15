using RPGCharacterManager.Characters;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Tests.Characters;

/// <summary>
/// Проверка мастера создания персонажа: отбора вариантов по игровой системе
/// и источникам, проверки требований и создания персонажа.
/// </summary>
public sealed class CharacterBuilderServiceTests
{
    private const int OptionLimit = 100;

    [Fact]
    public async Task Мастер_СобираетШагиИзОписаний()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var identifiers = context.Builder.Steps.Select(step => step.Id).ToList();

        Assert.Equal(CharacterStepIds.GameSystem, identifiers[0]);
        Assert.Equal(CharacterStepIds.Summary, identifiers[^1]);
        Assert.Contains(CharacterStepIds.Attributes, identifiers, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Варианты_ОтбираютсяПоИгровойСистеме()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var first = new GameSystem { Name = "Первая система", SystemName = "первая" };
        var second = new GameSystem { Name = "Вторая система", SystemName = "вторая" };
        await context.AddAsync(first, second);

        await context.AddAsync(
            CharacterContent.Race("Эльф", "эльф", gameSystemId: first.Id),
            CharacterContent.Race("Орк", "орк", gameSystemId: second.Id),
            CharacterContent.Race("Человек", "человек"));

        var draft = new CharacterDraft { GameSystemId = first.Id };

        var page = await context.Builder.GetOptionsAsync(
            context.Step(CharacterStepIds.Race),
            draft,
            search: null,
            includeUnavailable: true,
            OptionLimit);

        // Показываются объекты выбранной системы и объекты, не привязанные к системе.
        Assert.Equal(["Человек", "Эльф"], page.Options.Select(option => option.Name).Order().ToArray());
    }

    [Fact]
    public async Task Варианты_ОтбираютсяПоИсточникам()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var allowed = new ContentPack { Name = "Основная книга" };
        var excluded = new ContentPack { Name = "Дополнение" };
        await context.AddAsync(allowed, excluded);

        await context.AddAsync(
            CharacterContent.Race("Эльф", "эльф", contentPackId: allowed.Id),
            CharacterContent.Race("Дракон", "дракон", contentPackId: excluded.Id));

        var draft = new CharacterDraft { UseAllSources = false };
        draft.EnabledSourceIds.Add(allowed.Id);

        var page = await context.Builder.GetOptionsAsync(
            context.Step(CharacterStepIds.Race),
            draft,
            search: null,
            includeUnavailable: true,
            OptionLimit);

        Assert.Equal("Эльф", Assert.Single(page.Options).Name);
    }

    [Fact]
    public async Task Требование_НеВыполнено_ВариантНедоступенСПричиной()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        await context.AddAsync(strength);
        await context.AddAsync(CharacterContent.Race("Великан", "великан", requirements: "сила >= 15"));

        var draft = new CharacterDraft();

        var page = await context.Builder.GetOptionsAsync(
            context.Step(CharacterStepIds.Race),
            draft,
            search: null,
            includeUnavailable: true,
            OptionLimit);

        var option = Assert.Single(page.Options);
        Assert.False(option.IsAvailable);
        Assert.Contains("Требование не выполнено", option.UnavailableReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Требование_Выполнено_ВариантДоступен()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        await context.AddAsync(strength);
        await context.AddAsync(CharacterContent.Race("Великан", "великан", requirements: "сила >= 15"));

        var draft = new CharacterDraft();
        draft.AttributeBaseValues[strength.Id] = 16;

        var page = await context.Builder.GetOptionsAsync(
            context.Step(CharacterStepIds.Race),
            draft,
            search: null,
            includeUnavailable: true,
            OptionLimit);

        Assert.True(Assert.Single(page.Options).IsAvailable);
    }

    [Fact]
    public async Task Требование_ПоУровню_ПроверяетсяПеременнойУровень()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        await context.AddAsync(CharacterContent.Race("Древний", "древний", requirements: "уровень >= 5"));

        var draft = new CharacterDraft { Level = 5 };

        var page = await context.Builder.GetOptionsAsync(
            context.Step(CharacterStepIds.Race),
            draft,
            search: null,
            includeUnavailable: true,
            OptionLimit);

        Assert.True(Assert.Single(page.Options).IsAvailable);
    }

    [Fact]
    public async Task НедоступныеВарианты_МожноСкрыть()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        await context.AddAsync(CharacterContent.Race("Древний", "древний", requirements: "уровень >= 5"));

        var page = await context.Builder.GetOptionsAsync(
            context.Step(CharacterStepIds.Race),
            new CharacterDraft(),
            search: null,
            includeUnavailable: false,
            OptionLimit);

        Assert.Empty(page.Options);
    }

    [Fact]
    public async Task Подкласс_ПоказываетсяТолькоДляВыбранногоКласса()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var warrior = CharacterContent.Class("Воин", "воин");
        var mage = CharacterContent.Class("Маг", "маг");
        await context.AddAsync(warrior, mage);

        await context.AddAsync(
            CharacterContent.Subclass("Берсерк", "берсерк", warrior.Id),
            CharacterContent.Subclass("Некромант", "некромант", mage.Id));

        var draft = new CharacterDraft();
        var classStep = context.Step(CharacterStepIds.Class);
        var subclassStep = context.Step(CharacterStepIds.Subclass);

        // Пока класс не выбран, подклассы не показываются: они принадлежат классу.
        var empty = await context.Builder.GetOptionsAsync(
            subclassStep, draft, null, includeUnavailable: true, OptionLimit);
        Assert.Empty(empty.Options);

        context.Builder.SetSelection(classStep, draft, warrior.Id);

        var page = await context.Builder.GetOptionsAsync(
            subclassStep, draft, null, includeUnavailable: true, OptionLimit);

        Assert.Equal("Берсерк", Assert.Single(page.Options).Name);
    }

    [Fact]
    public async Task Подкласс_НедоступенДоТребуемогоУровня()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var warrior = CharacterContent.Class("Воин", "воин");
        await context.AddAsync(warrior);
        await context.AddAsync(CharacterContent.Subclass("Мастер", "мастер", warrior.Id, availableAtLevel: 3));

        var draft = new CharacterDraft();
        context.Builder.SetSelection(context.Step(CharacterStepIds.Class), draft, warrior.Id);

        var page = await context.Builder.GetOptionsAsync(
            context.Step(CharacterStepIds.Subclass), draft, null, includeUnavailable: true, OptionLimit);

        Assert.False(Assert.Single(page.Options).IsAvailable);

        draft.Level = 3;

        var afterLevelUp = await context.Builder.GetOptionsAsync(
            context.Step(CharacterStepIds.Subclass), draft, null, includeUnavailable: true, OptionLimit);

        Assert.True(Assert.Single(afterLevelUp.Options).IsAvailable);
    }

    [Fact]
    public async Task СменаКласса_СбрасываетВыборПодкласса()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var warrior = CharacterContent.Class("Воин", "воин");
        var mage = CharacterContent.Class("Маг", "маг");
        await context.AddAsync(warrior, mage);

        var berserk = CharacterContent.Subclass("Берсерк", "берсерк", warrior.Id);
        await context.AddAsync(berserk);

        var draft = new CharacterDraft();
        var classStep = context.Step(CharacterStepIds.Class);
        var subclassStep = context.Step(CharacterStepIds.Subclass);

        context.Builder.SetSelection(classStep, draft, warrior.Id);
        context.Builder.SetSelection(subclassStep, draft, berserk.Id);

        Assert.Equal(berserk.Id, draft.Character.SubclassId);

        context.Builder.SetSelection(classStep, draft, mage.Id);

        Assert.Null(draft.GetSelection(subclassStep.Id));
        Assert.Null(draft.Character.SubclassId);
    }

    [Fact]
    public async Task Черта_НедоступнаБезТребуемойЧерты()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var basic = CharacterContent.Trait("Основы боя", "основы_боя");
        await context.AddAsync(basic);

        var advanced = CharacterContent.Trait("Мастер боя", "мастер_боя", requiredTraitId: basic.Id);
        await context.AddAsync(advanced);

        var draft = new CharacterDraft();
        var step = context.Step(CharacterStepIds.Traits);

        var page = await context.Builder.GetOptionsAsync(
            step, draft, "Мастер", includeUnavailable: true, OptionLimit);

        var option = Assert.Single(page.Options);
        Assert.False(option.IsAvailable);
        Assert.Contains("Основы боя", option.UnavailableReason, StringComparison.Ordinal);

        draft.GetSelections(step.Id).Add(basic.Id);

        var afterSelection = await context.Builder.GetOptionsAsync(
            step, draft, "Мастер", includeUnavailable: true, OptionLimit);

        Assert.True(Assert.Single(afterSelection.Options).IsAvailable);
    }

    [Fact]
    public async Task Создание_СохраняетПерсонажаСоВсемиВычисленнымиЗначениями()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute(
            "Сила",
            "сила",
            defaultValue: 10,
            modifierFormula: "ОкруглитьВниз((значение - 10) / 2)");

        await context.AddAsync(strength);
        await context.AddAsync(CharacterContent.Resource("Здоровье", "здоровье", "10 + уровень * 5"));

        var skill = CharacterContent.Skill("Атлетика", "атлетика", strength.Id);
        await context.AddAsync(skill);

        var race = CharacterContent.Race("Человек", "человек");
        await context.AddAsync(race);

        var draft = new CharacterDraft { Level = 2 };
        draft.Character.Name = "Аргус";
        draft.AttributeBaseValues[strength.Id] = 16;
        draft.GetSelections(CharacterStepIds.Skills).Add(skill.Id);

        context.Builder.SetSelection(context.Step(CharacterStepIds.Race), draft, race.Id);

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        var character = await context.LoadCharacterAsync(result.Value);

        Assert.Equal("Аргус", character.Name);
        Assert.Equal(race.Id, character.RaceId);

        var attribute = Assert.Single(character.Attributes);
        Assert.Equal(16, attribute.CurrentValue);
        Assert.Equal(3, attribute.Modifier);

        var resource = Assert.Single(character.Resources);
        Assert.Equal(20, resource.Maximum);

        var characterSkill = Assert.Single(character.Skills);
        Assert.Equal(skill.Id, characterSkill.SkillId);
        Assert.Equal(3, characterSkill.CurrentValue);
    }

    [Fact]
    public async Task Создание_ЗаписываетсяВЖурналИзменений()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var draft = new CharacterDraft();
        draft.Character.Name = "Лира";

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        var history = await context.LoadHistoryAsync(result.Value);

        Assert.Equal("создание_персонажа", Assert.Single(history).Action);
    }

    [Fact]
    public async Task Создание_ЕстьСистемыНоНеВыбрана_ЗавершаетсяОшибкой()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        // Расширение установило игровую систему — но мастер её не выбрал.
        // Раньше это молча оставляло списки рас, классов и остального контента
        // пустыми, вместо явной ошибки при попытке создать персонажа.
        await context.AddAsync(new GameSystem { Name = "D&D 5e", SystemName = "днд5" });

        var draft = new CharacterDraft();
        draft.Character.Name = "Лира";

        var result = await context.Builder.CreateAsync(draft);

        Assert.True(result.IsFailure);
        Assert.Contains("игровая система", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Создание_НетНиОднойСистемы_НеТребуетВыбора()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        // Весь контент — самодельный, без установленных расширений: выбирать
        // игровую систему не из чего, и создание не должно на этом спотыкаться.
        var draft = new CharacterDraft();
        draft.Character.Name = "Лира";

        var result = await context.Builder.CreateAsync(draft);

        Assert.True(result.IsSuccess, result.Error);
    }

    [Fact]
    public async Task Создание_БезИмени_ЗавершаетсяОшибкой()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var result = await context.Builder.CreateAsync(new CharacterDraft());

        Assert.True(result.IsFailure);
        Assert.Contains("Имя", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Создание_ЗначениеХарактеристикиВнеДиапазона_ЗавершаетсяОшибкой()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", minimum: 3, maximum: 18);
        await context.AddAsync(strength);

        var draft = new CharacterDraft();
        draft.Character.Name = "Голиаф";
        draft.AttributeBaseValues[strength.Id] = 25;

        var result = await context.Builder.CreateAsync(draft);

        Assert.True(result.IsFailure);
        Assert.Contains("больше допустимого", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Создание_ВыбранныйОбъектПересталПодходить_ЗавершаетсяОшибкой()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        await context.AddAsync(strength);

        var race = CharacterContent.Race("Великан", "великан", requirements: "сила >= 15");
        await context.AddAsync(race);

        var draft = new CharacterDraft();
        draft.Character.Name = "Тор";
        draft.AttributeBaseValues[strength.Id] = 16;

        context.Builder.SetSelection(context.Step(CharacterStepIds.Race), draft, race.Id);

        // Значение характеристики снижено после выбора расы: требование больше не выполняется.
        draft.AttributeBaseValues[strength.Id] = 8;

        var result = await context.Builder.CreateAsync(draft);

        Assert.True(result.IsFailure);
        Assert.Contains("больше не подходит", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Черновик_ВосстанавливаетсяПоСохранённомуПерсонажу()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила");
        await context.AddAsync(strength);

        var race = CharacterContent.Race("Человек", "человек");
        await context.AddAsync(race);

        var skill = CharacterContent.Skill("Атлетика", "атлетика", strength.Id);
        await context.AddAsync(skill);

        var draft = new CharacterDraft();
        draft.Character.Name = "Ирис";
        draft.AttributeBaseValues[strength.Id] = 14;
        draft.GetSelections(CharacterStepIds.Skills).Add(skill.Id);

        context.Builder.SetSelection(context.Step(CharacterStepIds.Race), draft, race.Id);

        var created = await context.Builder.CreateAsync(draft);
        Assert.True(created.IsSuccess, created.Error);

        var character = await context.LoadCharacterAsync(created.Value);
        var restored = context.Builder.CreateDraft(character);

        Assert.Equal(race.Id, restored.GetSelection(CharacterStepIds.Race));
        Assert.Contains(skill.Id, restored.GetSelections(CharacterStepIds.Skills));
        Assert.Equal(14, restored.AttributeBaseValues[strength.Id]);
    }

    [Fact]
    public async Task СкрытыйБонусМастерства_РаботаетПоУровнюИПриАвторскомЗначении()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute(
            "Сила",
            "сила",
            defaultValue: 14,
            modifierFormula: "ОкруглитьВниз((значение - 10) / 2)");
        var proficiencyBonus = CharacterContent.Attribute(
            "Бонус мастерства",
            "бонус_мастерства",
            formula: "2 + ОкруглитьВниз((уровень - 1) / 4)");
        proficiencyBonus.IsHidden = true;

        var athletics = CharacterContent.Skill(
            "Атлетика",
            "атлетика",
            strength.Id,
            formula: "характеристика + владение * бонус_мастерства");

        await context.AddAsync(strength, proficiencyBonus);
        await context.AddAsync(athletics);

        var draft = new CharacterDraft();
        draft.AttributeBaseValues[strength.Id] = 14;
        draft.GetSelections(CharacterStepIds.Skills).Add(athletics.Id);

        var visible = await context.Builder.GetAttributesAsync(draft);
        Assert.DoesNotContain(visible, attribute => attribute.Id == proficiencyBonus.Id);

        var expectedByLevel = new[]
        {
            (Level: 1, Bonus: 2d),
            (Level: 5, Bonus: 3d),
            (Level: 9, Bonus: 4d),
            (Level: 13, Bonus: 5d),
            (Level: 17, Bonus: 6d),
        };

        foreach (var expected in expectedByLevel)
        {
            draft.Level = expected.Level;
            var calculation = await context.Builder.CalculateAsync(draft);

            Assert.Equal(
                expected.Bonus,
                calculation.Attributes.Single(attribute => attribute.Id == proficiencyBonus.Id).Value);
            Assert.Equal(2 + expected.Bonus, Assert.Single(calculation.Skills).Value);
            Assert.DoesNotContain(
                calculation.Issues,
                issue => issue.Message.Contains("Неизвестная переменная", StringComparison.Ordinal));
        }

        draft.Level = 1;
        draft.AttributeOverrides[proficiencyBonus.Id] = 9;

        var custom = await context.Builder.CalculateAsync(draft);
        Assert.Equal(9, custom.Attributes.Single(attribute => attribute.Id == proficiencyBonus.Id).Value);
        Assert.Equal(11, Assert.Single(custom.Skills).Value);

        var formulaContext = await context.Builder.CreateContextAsync(draft);
        Assert.True(formulaContext.TryGetVariable("бонус_мастерства", out var value));
        Assert.Equal(9, value.AsNumber());
    }
}
