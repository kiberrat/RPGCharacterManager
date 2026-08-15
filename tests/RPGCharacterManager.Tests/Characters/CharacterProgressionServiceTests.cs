using Microsoft.EntityFrameworkCore;
using RPGCharacterManager.Characters;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Tests.Rules;

namespace RPGCharacterManager.Tests.Characters;

/// <summary>
/// Проверка повышения уровня и автоматического обновления параметров персонажа.
/// </summary>
public sealed class CharacterProgressionServiceTests
{
    private const string HealthFormula = "10 + уровень * 5";

    private static async Task<Guid> CreateCharacterAsync(CharacterTestContext context, string name = "Герой")
    {
        var draft = new CharacterDraft();
        draft.Character.Name = name;

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static RuleDefinition CreateLevelUpRule(string attributeSystemName)
    {
        var rule = new RuleDefinition
        {
            Name = "Награда за уровень",
            Trigger = RuleTriggers.CharacterLevelUp,
            Category = RuleCategories.Character,
        };

        rule.Actions.Add(RuleTestFactory.Action(
            "изменить_значение",
            ("параметр", attributeSystemName),
            ("значение", "1")));

        return rule;
    }

    [Fact]
    public async Task ПовышениеУровня_УвеличиваетУровеньИПересчитываетРесурс()
    {
        await using var context = await CharacterTestContext.CreateAsync();
        await context.AddAsync(CharacterContent.Resource("Здоровье", "здоровье", HealthFormula));

        var characterId = await CreateCharacterAsync(context);

        var report = await context.Progression.LevelUpAsync(characterId);
        Assert.True(report.IsSuccess, report.Error);

        Assert.Equal(1, report.Value.PreviousLevel);
        Assert.Equal(2, report.Value.CurrentLevel);

        var character = await context.LoadCharacterAsync(characterId);

        Assert.Equal(2, character.Level);
        Assert.Equal(20, Assert.Single(character.Resources).Maximum);
    }

    [Fact]
    public async Task ПовышениеУровня_НаНесколькоУровней_УчитываетсяФормулами()
    {
        await using var context = await CharacterTestContext.CreateAsync();
        await context.AddAsync(CharacterContent.Resource("Здоровье", "здоровье", HealthFormula));

        var characterId = await CreateCharacterAsync(context);

        var report = await context.Progression.LevelUpAsync(characterId, levels: 4);
        Assert.True(report.IsSuccess, report.Error);

        var character = await context.LoadCharacterAsync(characterId);

        Assert.Equal(5, character.Level);
        Assert.Equal(35, Assert.Single(character.Resources).Maximum);
    }

    [Fact]
    public async Task ПовышениеУровня_ПрименяетПравилаСобытия()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        await context.AddAsync(strength);

        await context.Rules.SaveAsync(CreateLevelUpRule("сила"));

        var characterId = await CreateCharacterAsync(context);

        var report = await context.Progression.LevelUpAsync(characterId);
        Assert.True(report.IsSuccess, report.Error);

        Assert.Contains("Награда за уровень", report.Value.AppliedRules, StringComparer.Ordinal);

        var character = await context.LoadCharacterAsync(characterId);

        Assert.Equal(11, Assert.Single(character.Attributes).CurrentValue);
    }

    [Fact]
    public async Task ПовышениеУровня_НаграждаетКаждыйРаз_ИНеТеряетПрежнююНаграду()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        await context.AddAsync(strength);

        await context.Rules.SaveAsync(CreateLevelUpRule("сила"));

        var characterId = await CreateCharacterAsync(context);

        await context.Progression.LevelUpAsync(characterId);
        await context.Progression.LevelUpAsync(characterId);

        var character = await context.LoadCharacterAsync(characterId);
        var value = Assert.Single(character.Attributes);

        // Награда за уровень постоянна: она записывается в базовое значение,
        // поэтому два повышения дают ровно две единицы.
        Assert.Equal(12, value.CurrentValue);
        Assert.Equal(12, value.BaseValue);
    }

    [Fact]
    public async Task Пересчёт_НеПрименяетПравилаПовышенияУровня()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        await context.AddAsync(strength);

        await context.Rules.SaveAsync(CreateLevelUpRule("сила"));

        var characterId = await CreateCharacterAsync(context);

        var report = await context.Progression.RecalculateAsync(characterId);
        Assert.True(report.IsSuccess, report.Error);

        var character = await context.LoadCharacterAsync(characterId);

        Assert.Equal(1, character.Level);
        Assert.Equal(10, Assert.Single(character.Attributes).CurrentValue);
    }

    [Fact]
    public async Task Пересчёт_УчитываетИзменившиесяФормулыКонтента()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var health = CharacterContent.Resource("Здоровье", "здоровье", "10");
        await context.AddAsync(health);

        var characterId = await CreateCharacterAsync(context);

        var before = await context.LoadCharacterAsync(characterId);
        Assert.Equal(10, Assert.Single(before.Resources).Maximum);

        await context.UpdateResourceFormulaAsync(health.Id, "40");

        var report = await context.Progression.RecalculateAsync(characterId);
        Assert.True(report.IsSuccess, report.Error);

        var after = await context.LoadCharacterAsync(characterId);

        Assert.Equal(40, Assert.Single(after.Resources).Maximum);
        Assert.Contains(
            report.Value.Changes,
            change => change.Contains("Здоровье", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Пересчёт_ДобавляетЗаписиДляНовогоКонтента()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        // Характеристика и ресурс созданы уже после персонажа: пересчёт обязан
        // добавить ему недостающие записи, а не завершиться ошибкой сохранения.
        await context.AddAsync(CharacterContent.Attribute("Удача", "удача", defaultValue: 4));
        await context.AddAsync(CharacterContent.Resource("Мана", "мана", "12"));

        var report = await context.Progression.RecalculateAsync(characterId);
        Assert.True(report.IsSuccess, report.Error);

        var character = await context.LoadCharacterAsync(characterId);

        Assert.Equal(4, Assert.Single(character.Attributes).CurrentValue);
        Assert.Equal(12, Assert.Single(character.Resources).Maximum);
    }

    [Fact]
    public async Task ПовышениеУровня_ЗаписываетсяВЖурналИзменений()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);
        await context.Progression.LevelUpAsync(characterId);

        var history = await context.LoadHistoryAsync(characterId);

        Assert.Contains(history, entry => entry.Action == "повышение_уровня");
    }

    [Fact]
    public async Task ПовышениеУровня_НесуществующийПерсонаж_ЗавершаетсяОшибкой()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var report = await context.Progression.LevelUpAsync(Guid.NewGuid());

        Assert.True(report.IsFailure);
    }

    [Fact]
    public async Task ПовышениеУровня_НедопустимоеКоличествоУровней_ЗавершаетсяОшибкой()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);
        var report = await context.Progression.LevelUpAsync(characterId, levels: 0);

        Assert.True(report.IsFailure);
    }

    [Fact]
    public async Task Персонаж_НаходитсяПоискомИУдаляется()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context, "Мелисандра");

        var found = await context.Characters.SearchAsync("Мелис", 0, 50);
        Assert.Equal("Мелисандра", Assert.Single(found.Items).Name);

        var deleted = await context.Characters.DeleteAsync(characterId);
        Assert.True(deleted.IsSuccess, deleted.Error);

        var empty = await context.Characters.SearchAsync(null, 0, 50);
        Assert.Empty(empty.Items);
    }
}
