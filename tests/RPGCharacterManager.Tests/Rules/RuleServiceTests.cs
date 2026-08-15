using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database.Repositories;
using RPGCharacterManager.GameRules;
using RPGCharacterManager.Tests.Support;

namespace RPGCharacterManager.Tests.Rules;

/// <summary>
/// Проверка сохранения правил в базе данных: условия и действия должны
/// переживать запись и чтение без потерь.
/// </summary>
public sealed class RuleServiceTests
{
    private static RuleService CreateService(TestDatabase database) => new(
        new Repository<GameRule>(database.ContextFactory),
        NullLogger<RuleService>.Instance);

    private static RuleDefinition CreateRule()
    {
        var rule = new RuleDefinition
        {
            Name = "Ярость берсерка",
            Description = "При падении здоровья ниже трети персонаж впадает в ярость.",
            Category = RuleCategories.Combat,
            Trigger = "бой.получение_урона",
            Priority = 150,
            Enabled = true,
            Author = "Пользователь",
        };

        var group = new RuleConditionGroup { Operator = RuleLogicalOperator.And };
        group.Children.Add(new RuleComparison
        {
            Left = "Здоровье",
            Operator = RuleComparisonOperator.Less,
            Right = "Здоровье.Максимум / 3",
        });
        group.Children.Add(new RuleComparison
        {
            Left = "признак",
            Operator = RuleComparisonOperator.HasNot,
            Right = "Ярость",
        });

        rule.Condition = group;

        rule.Actions.Add(RuleTestFactory.Action("добавить_эффект", ("признак", "Ярость")));
        rule.Actions.Add(RuleTestFactory.Action(
            "изменить_значение",
            ("параметр", "Сила"),
            ("значение", "4")));

        return rule;
    }

    [Fact]
    public async Task Правило_СохраняетсяИЧитаетсяБезПотерь()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var original = CreateRule();
        var saved = await service.SaveAsync(original);
        Assert.True(saved.IsSuccess, saved.Error);

        var loaded = await service.GetAllAsync();
        var restored = Assert.Single(loaded);

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Description, restored.Description);
        Assert.Equal(original.Category, restored.Category);
        Assert.Equal(original.Trigger, restored.Trigger);
        Assert.Equal(original.Priority, restored.Priority);
        Assert.Equal(original.Author, restored.Author);

        var group = Assert.IsType<RuleConditionGroup>(restored.Condition);
        Assert.Equal(2, group.Children.Count);

        var first = Assert.IsType<RuleComparison>(group.Children[0]);
        Assert.Equal("Здоровье.Максимум / 3", first.Right);

        var second = Assert.IsType<RuleComparison>(group.Children[1]);
        Assert.Equal(RuleComparisonOperator.HasNot, second.Operator);

        Assert.Equal(2, restored.Actions.Count);
        Assert.Equal("Ярость", restored.Actions[0].GetParameter("признак"));
        Assert.Equal("4", restored.Actions[1].GetParameter("значение"));
    }

    [Fact]
    public async Task СохранённоеПравило_ВыполняетсяДвижком()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        await service.SaveAsync(CreateRule());

        var rules = await service.GetByTriggerAsync("бой.получение_урона");
        Assert.Single(rules);

        var engine = RuleTestFactory.CreateEngine();
        var target = new Core.Models.Rules.RuleTarget("Герой")
            .WithVariable("Здоровье", 10)
            .WithVariable("Здоровье.Максимум", 60)
            .WithVariable("Сила", 14);

        var report = engine.Execute("бой.получение_урона", target, rules);

        Assert.Single(report.ExecutedRules);
        Assert.True(target.HasTag("Ярость"));

        target.TryGetVariable("Сила", out var strength);
        Assert.Equal(18, strength.AsNumber());
    }

    [Fact]
    public async Task ПовторноеСохранение_ОбновляетПравилоАНеСоздаётВторое()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var rule = CreateRule();
        await service.SaveAsync(rule);

        rule.Name = "Ярость берсерка (изменено)";
        rule.Priority = 500;
        await service.SaveAsync(rule);

        var loaded = await service.GetAllAsync();
        var restored = Assert.Single(loaded);

        Assert.Equal("Ярость берсерка (изменено)", restored.Name);
        Assert.Equal(500, restored.Priority);
    }

    [Fact]
    public async Task ОтключённоеПравило_НеПопадаетВВыборкуПоСобытию()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var rule = CreateRule();
        rule.Enabled = false;
        await service.SaveAsync(rule);

        Assert.Empty(await service.GetByTriggerAsync("бой.получение_урона"));
        Assert.Single(await service.GetAllAsync());
    }

    [Fact]
    public async Task Удаление_УбираетПравилоИзБазы()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var rule = CreateRule();
        await service.SaveAsync(rule);

        Assert.True(await service.DeleteAsync(rule.Id));
        Assert.Empty(await service.GetAllAsync());
    }

    [Fact]
    public async Task ПравилоБезНазвания_НеСохраняется()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var rule = CreateRule();
        rule.Name = string.Empty;

        var result = await service.SaveAsync(rule);

        Assert.True(result.IsFailure);
        Assert.Empty(await service.GetAllAsync());
    }

    [Fact]
    public async Task СложноеУсловие_ПревышающееДлинуВыражения_Сохраняется()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var rule = CreateRule();
        var group = new RuleConditionGroup { Operator = RuleLogicalOperator.Or };

        // Дерево из множества условий проверяет, что поле базы данных
        // рассчитано на длинное представление, а не на одно выражение.
        for (var index = 0; index < 60; index++)
        {
            group.Children.Add(new RuleComparison
            {
                Left = $"Характеристика{index}",
                Operator = RuleComparisonOperator.GreaterOrEqual,
                Right = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
        }

        rule.Condition = group;

        var saved = await service.SaveAsync(rule);
        Assert.True(saved.IsSuccess, saved.Error);

        var restored = Assert.Single(await service.GetAllAsync());
        var restoredGroup = Assert.IsType<RuleConditionGroup>(restored.Condition);

        Assert.Equal(60, restoredGroup.Children.Count);
    }
}
