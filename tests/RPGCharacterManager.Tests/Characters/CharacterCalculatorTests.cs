using RPGCharacterManager.Characters;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Tests.Rules;

namespace RPGCharacterManager.Tests.Characters;

/// <summary>
/// Проверка автоматического расчёта параметров персонажа.
///
/// Расчёт не должен содержать ни одной вшитой игровой формулы: все значения
/// вычисляются выражениями, заданными в контенте, и изменяются правилами.
/// </summary>
public sealed class CharacterCalculatorTests
{
    private const string HalfOfValue = "ОкруглитьВниз((значение - 10) / 2)";

    private static CharacterCalculator CreateCalculator() =>
        new(RuleTestFactory.CreateFormulas(), RuleTestFactory.CreateEngine());

    private static CharacterCalculationInput CreateInput(
        IReadOnlyList<AttributeDefinition> attributes,
        int level = 1) => new()
        {
            Attributes = attributes,
            Level = level,
            DisplayName = "Проверка",
        };

    [Fact]
    public void Характеристика_БезФормулы_ПолучаетБазовоеЗначение()
    {
        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 12);
        var input = CreateInput([strength]);

        var calculation = CreateCalculator().Calculate(input);

        var value = Assert.Single(calculation.Attributes);
        Assert.Equal(12, value.Value);
        Assert.False(value.IsDerived);
    }

    [Fact]
    public void Характеристика_ПолучаетЗаданноеПользователемЗначение()
    {
        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);

        var input = new CharacterCalculationInput
        {
            Attributes = [strength],
            BaseValues = new Dictionary<Guid, double> { [strength.Id] = 17 },
        };

        var calculation = CreateCalculator().Calculate(input);

        Assert.Equal(17, Assert.Single(calculation.Attributes).Value);
    }

    [Fact]
    public void Модификатор_ВычисляетсяФормулойХарактеристики()
    {
        var strength = CharacterContent.Attribute(
            "Сила",
            "сила",
            defaultValue: 16,
            modifierFormula: HalfOfValue);

        var calculation = CreateCalculator().Calculate(CreateInput([strength]));

        Assert.Equal(3, Assert.Single(calculation.Attributes).Modifier);
    }

    [Fact]
    public void ВычисляемаяХарактеристика_ИспользуетДругиеХарактеристики()
    {
        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 14);
        var agility = CharacterContent.Attribute("Ловкость", "ловкость", defaultValue: 12);

        // Производная характеристика ссылается на другую производную:
        // порядок вычисления определяется зависимостями, а не порядком в списке.
        var power = CharacterContent.Attribute("Мощь", "мощь", formula: "сила + ловкость");
        var mastery = CharacterContent.Attribute("Мастерство", "мастерство", formula: "мощь * 2");

        var calculation = CreateCalculator().Calculate(CreateInput([mastery, power, strength, agility]));

        Assert.Equal(26, Find(calculation, "мощь").Value);
        Assert.Equal(52, Find(calculation, "мастерство").Value);
        Assert.True(Find(calculation, "мощь").IsDerived);
    }

    [Fact]
    public void ЦиклическаяЗависимость_ДаётПредупреждение()
    {
        var first = CharacterContent.Attribute("Первая", "первая", formula: "вторая + 1");
        var second = CharacterContent.Attribute("Вторая", "вторая", formula: "первая + 1");

        var calculation = CreateCalculator().Calculate(CreateInput([first, second]));

        Assert.Contains(
            calculation.Issues,
            issue => issue.Severity == CharacterIssueSeverity.Warning
                && issue.Message.Contains("ссылаются друг на друга", StringComparison.Ordinal));
    }

    [Fact]
    public void ОшибкаФормулы_ДаётПредупреждениеИНеПрерываетРасчёт()
    {
        var broken = CharacterContent.Attribute("Сломанная", "сломанная", formula: "неизвестная + 1");
        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 15);

        var calculation = CreateCalculator().Calculate(CreateInput([broken, strength]));

        Assert.Contains(calculation.Issues, issue => issue.Severity == CharacterIssueSeverity.Warning);
        Assert.Equal(15, Find(calculation, "сила").Value);
    }

    [Fact]
    public void Ресурс_ВычисляетсяПоФормулеСУчётомУровня()
    {
        var strength = CharacterContent.Attribute(
            "Сила",
            "сила",
            defaultValue: 16,
            modifierFormula: HalfOfValue);

        var health = CharacterContent.Resource("Здоровье", "здоровье", "10 + уровень * 5");

        var input = new CharacterCalculationInput
        {
            Attributes = [strength],
            Resources = [health],
            Level = 3,
        };

        var calculation = CreateCalculator().Calculate(input);

        var resource = Assert.Single(calculation.Resources);
        Assert.Equal(25, resource.Maximum);
        Assert.Equal(25, resource.Current);
    }

    [Fact]
    public void Ресурс_БезФормулыНачальногоЗначения_ЗаполняетсяДоМаксимума()
    {
        var mana = CharacterContent.Resource("Мана", "мана", "20", startingFormula: "5");

        var input = new CharacterCalculationInput { Resources = [mana] };
        var calculation = CreateCalculator().Calculate(input);

        var resource = Assert.Single(calculation.Resources);
        Assert.Equal(20, resource.Maximum);
        Assert.Equal(5, resource.Current);
    }

    [Fact]
    public void Навык_БезФормулы_РавенМодификаторуСвязаннойХарактеристики()
    {
        var agility = CharacterContent.Attribute(
            "Ловкость",
            "ловкость",
            defaultValue: 18,
            modifierFormula: HalfOfValue);

        var stealth = CharacterContent.Skill("Скрытность", "скрытность", agility.Id);

        var input = new CharacterCalculationInput
        {
            Attributes = [agility],
            Skills = [stealth],
            SkillProficiencies = new Dictionary<Guid, int> { [stealth.Id] = 1 },
        };

        var calculation = CreateCalculator().Calculate(input);

        Assert.Equal(4, Assert.Single(calculation.Skills).Value);
    }

    [Fact]
    public void Навык_СФормулой_ПолучаетВладениеИМодификаторХарактеристики()
    {
        var agility = CharacterContent.Attribute(
            "Ловкость",
            "ловкость",
            defaultValue: 18,
            modifierFormula: HalfOfValue);

        // Формула навыка получает модификатор связанной характеристики
        // и уровень владения как переменные.
        var stealth = CharacterContent.Skill(
            "Скрытность",
            "скрытность",
            agility.Id,
            formula: "характеристика + владение * 3");

        var input = new CharacterCalculationInput
        {
            Attributes = [agility],
            Skills = [stealth],
            SkillProficiencies = new Dictionary<Guid, int> { [stealth.Id] = 2 },
        };

        var calculation = CreateCalculator().Calculate(input);

        Assert.Equal(10, Assert.Single(calculation.Skills).Value);
    }

    [Fact]
    public void АвторскоеЗначение_ЗаменяетФормулуИПересчитываетЗависимости()
    {
        var agility = CharacterContent.Attribute(
            "Ловкость",
            "ловкость",
            defaultValue: 14,
            modifierFormula: HalfOfValue);
        var proficiencyBonus = CharacterContent.Attribute(
            "Бонус мастерства",
            "бонус_мастерства",
            formula: "2 + ОкруглитьВниз((уровень - 1) / 4)");
        var stealth = CharacterContent.Skill(
            "Скрытность",
            "скрытность",
            agility.Id,
            formula: "характеристика + владение * бонус_мастерства");

        var input = new CharacterCalculationInput
        {
            Attributes = [agility, proficiencyBonus],
            Skills = [stealth],
            Level = 1,
            AttributeOverrides = new Dictionary<Guid, double> { [proficiencyBonus.Id] = 9 },
            SkillProficiencies = new Dictionary<Guid, int> { [stealth.Id] = 1 },
        };

        var calculation = CreateCalculator().Calculate(input);

        Assert.Equal(9, Find(calculation, "бонус_мастерства").Value);
        Assert.Equal(11, Assert.Single(calculation.Skills).Value);
        Assert.Empty(calculation.Issues);
    }

    [Fact]
    public void Правило_ИзменяетХарактеристикуДоВычисленияМодификатора()
    {
        var strength = CharacterContent.Attribute(
            "Сила",
            "сила",
            defaultValue: 14,
            modifierFormula: HalfOfValue);

        var rule = new RuleDefinition
        {
            Name = "Дар силы",
            Trigger = RuleTriggers.CharacterRecalculated,
            Category = RuleCategories.Character,
        };

        rule.Actions.Add(RuleTestFactory.Action(
            "изменить_значение",
            ("параметр", "сила"),
            ("значение", "4")));

        var input = new CharacterCalculationInput
        {
            Attributes = [strength],
            RuleSets = [new RuleApplication(RuleTriggers.CharacterRecalculated, [rule])],
        };

        var calculation = CreateCalculator().Calculate(input);

        var value = Assert.Single(calculation.Attributes);
        Assert.Equal(18, value.Value);
        Assert.Equal(4, value.Modifier);
        Assert.Contains("Дар силы", calculation.AppliedRules, StringComparer.Ordinal);
    }

    [Fact]
    public void Правило_СНевыполненнымУсловием_НеПрименяется()
    {
        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);

        var rule = new RuleDefinition
        {
            Name = "Только для сильных",
            Trigger = RuleTriggers.CharacterRecalculated,
            Category = RuleCategories.Character,
            Condition = new RuleComparison
            {
                Left = "сила",
                Operator = RuleComparisonOperator.GreaterOrEqual,
                Right = "15",
            },
        };

        rule.Actions.Add(RuleTestFactory.Action(
            "изменить_значение",
            ("параметр", "сила"),
            ("значение", "5")));

        var input = new CharacterCalculationInput
        {
            Attributes = [strength],
            RuleSets = [new RuleApplication(RuleTriggers.CharacterRecalculated, [rule])],
        };

        var calculation = CreateCalculator().Calculate(input);

        Assert.Equal(10, Assert.Single(calculation.Attributes).Value);
        Assert.Empty(calculation.AppliedRules);
    }

    private static CalculatedAttributeValue Find(CharacterCalculation calculation, string systemName) =>
        calculation.Attributes.Single(attribute => attribute.SystemName == systemName);
}
