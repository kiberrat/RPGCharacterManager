using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Models.Rules;
using RPGCharacterManager.Engine;
using RPGCharacterManager.Engine.Functions;
using RPGCharacterManager.GameRules;
using RPGCharacterManager.GameRules.Actions;
using RPGCharacterManager.GameRules.Serialization;
using RPGCharacterManager.GameRules.Triggers;
using RPGCharacterManager.GameRules.Validation;
using RPGCharacterManager.Tests.Engine;

namespace RPGCharacterManager.Tests.Rules;

/// <summary>
/// Общая подготовка подсистемы правил для тестов.
/// </summary>
internal static class RuleTestFactory
{
    /// <summary>Значение, выпадающее на кубиках, если тест не задал другое.</summary>
    public const int DefaultDiceValue = 3;

    public static IFormulaEngine CreateFormulas(int diceValue = DefaultDiceValue)
    {
        var random = new ConstantRandomSource(diceValue);

        IFormulaFunction[] functions =
        [
            new MinimumFunction(),
            new MaximumFunction(),
            new SumFunction(),
            new RoundFunction(),
            new FloorFunction(),
            new CeilingFunction(),
            new AbsoluteFunction(),
            new ClampFunction(),
            new IfFunction(),
            new DiceFunction(random),
        ];

        return new FormulaEngine(functions, random);
    }

    public static IRuleActionHandler[] CreateHandlers() =>
    [
        new SetValueActionHandler(),
        new AdjustValueActionHandler(),
        new AddTagActionHandler(),
        new RemoveTagActionHandler(),
        new SpendResourceActionHandler(),
        new RestoreResourceActionHandler(),
        new RollActionHandler(),
    ];

    public static RuleEngine CreateEngine(int diceValue = 3) =>
        new(CreateFormulas(diceValue), CreateHandlers());

    public static RuleValidator CreateValidator(RuleEngine? engine = null)
    {
        var formulas = CreateFormulas();
        var ruleEngine = engine ?? new RuleEngine(formulas, CreateHandlers());
        var catalog = new RuleTriggerCatalog([new StandardTriggerProvider()]);

        return new RuleValidator(formulas, ruleEngine, catalog);
    }

    public static RuleAction Action(string kind, params (string Name, string Value)[] parameters)
    {
        var action = new RuleAction { Kind = kind };

        foreach (var parameter in parameters)
        {
            action.Parameters[parameter.Name] = parameter.Value;
        }

        return action;
    }
}

public sealed class RuleConditionTests
{
    [Fact]
    public void Правило_БезУсловий_ВыполняетсяВсегда()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой");

        Assert.True(engine.EvaluateCondition(null, target));
    }

    [Theory]
    [InlineData(RuleComparisonOperator.GreaterOrEqual, "15", true)]
    [InlineData(RuleComparisonOperator.Greater, "18", false)]
    [InlineData(RuleComparisonOperator.Equal, "18", true)]
    [InlineData(RuleComparisonOperator.NotEqual, "18", false)]
    [InlineData(RuleComparisonOperator.Less, "20", true)]
    [InlineData(RuleComparisonOperator.LessOrEqual, "18", true)]
    public void Сравнение_ПрименяетВыбранныйОператор(
        RuleComparisonOperator comparison,
        string right,
        bool expected)
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой").WithVariable("Сила", 18);

        var condition = new RuleComparison { Left = "Сила", Operator = comparison, Right = right };

        Assert.Equal(expected, engine.EvaluateCondition(condition, target));
    }

    [Fact]
    public void Сравнение_ДопускаетФормулыВОбеихЧастях()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой")
            .WithVariable("Сила", 16)
            .WithVariable("Уровень", 5);

        var condition = new RuleComparison
        {
            Left = "Сила + Уровень",
            Operator = RuleComparisonOperator.GreaterOrEqual,
            Right = "Максимум(20; 18)",
        };

        Assert.True(engine.EvaluateCondition(condition, target));
    }

    [Fact]
    public void Оператор_Имеет_ПроверяетПризнакОбъекта()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой").WithTag("Ярость");

        var has = new RuleComparison
        {
            Left = "признак",
            Operator = RuleComparisonOperator.Has,
            Right = "Ярость",
        };

        var hasNot = new RuleComparison
        {
            Left = "признак",
            Operator = RuleComparisonOperator.HasNot,
            Right = "Отравление",
        };

        Assert.True(engine.EvaluateCondition(has, target));
        Assert.True(engine.EvaluateCondition(hasNot, target));
    }

    [Fact]
    public void Группа_И_ТребуетВыполненияВсехУсловий()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой").WithVariable("Уровень", 6).WithVariable("Сила", 12);

        var group = new RuleConditionGroup { Operator = RuleLogicalOperator.And };
        group.Children.Add(new RuleComparison
        {
            Left = "Уровень",
            Operator = RuleComparisonOperator.GreaterOrEqual,
            Right = "4",
        });
        group.Children.Add(new RuleComparison
        {
            Left = "Сила",
            Operator = RuleComparisonOperator.GreaterOrEqual,
            Right = "15",
        });

        Assert.False(engine.EvaluateCondition(group, target));
    }

    [Fact]
    public void Группа_ИЛИ_ТребуетХотяБыОдногоУсловия()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой").WithVariable("Уровень", 6).WithVariable("Сила", 12);

        var group = new RuleConditionGroup { Operator = RuleLogicalOperator.Or };
        group.Children.Add(new RuleComparison
        {
            Left = "Уровень",
            Operator = RuleComparisonOperator.GreaterOrEqual,
            Right = "4",
        });
        group.Children.Add(new RuleComparison
        {
            Left = "Сила",
            Operator = RuleComparisonOperator.GreaterOrEqual,
            Right = "15",
        });

        Assert.True(engine.EvaluateCondition(group, target));
    }

    [Fact]
    public void Группа_НЕ_ИнвертируетРезультат()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой").WithVariable("Уровень", 6);

        var group = new RuleConditionGroup { Operator = RuleLogicalOperator.And, IsNegated = true };
        group.Children.Add(new RuleComparison
        {
            Left = "Уровень",
            Operator = RuleComparisonOperator.GreaterOrEqual,
            Right = "4",
        });

        Assert.False(engine.EvaluateCondition(group, target));
    }

    [Fact]
    public void ВложенныеГруппы_ВычисляютсяКорректно()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой")
            .WithVariable("Уровень", 6)
            .WithVariable("Здоровье", 12)
            .WithTag("Ярость");

        // Уровень >= 5 И (Здоровье < 20 ИЛИ имеет «Благословение»)
        var inner = new RuleConditionGroup { Operator = RuleLogicalOperator.Or };
        inner.Children.Add(new RuleComparison
        {
            Left = "Здоровье",
            Operator = RuleComparisonOperator.Less,
            Right = "20",
        });
        inner.Children.Add(new RuleComparison
        {
            Left = "признак",
            Operator = RuleComparisonOperator.Has,
            Right = "Благословение",
        });

        var root = new RuleConditionGroup { Operator = RuleLogicalOperator.And };
        root.Children.Add(new RuleComparison
        {
            Left = "Уровень",
            Operator = RuleComparisonOperator.GreaterOrEqual,
            Right = "5",
        });
        root.Children.Add(inner);

        Assert.True(engine.EvaluateCondition(root, target));
    }

    [Fact]
    public void Сравнение_СОшибкойВФормуле_СчитаетсяНевыполненным()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой");

        var condition = new RuleComparison
        {
            Left = "НеизвестныйПараметр",
            Operator = RuleComparisonOperator.Greater,
            Right = "1",
        };

        Assert.False(engine.EvaluateCondition(condition, target));
    }
}

public sealed class RuleActionTests
{
    private static RuleDefinition CreateRule(string trigger, params RuleAction[] actions)
    {
        var rule = new RuleDefinition { Name = "Проверка", Trigger = trigger, Enabled = true };

        foreach (var action in actions)
        {
            rule.Actions.Add(action);
        }

        return rule;
    }

    [Fact]
    public void УстановитьЗначение_ПрисваиваетРезультатВыражения()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой").WithVariable("Телосложение", 14).WithVariable("Уровень", 3);

        var rule = CreateRule(
            "персонаж.пересчёт",
            RuleTestFactory.Action("установить_значение", ("параметр", "МаксимумЗдоровья"), ("значение", "Телосложение * 4 + Уровень * 6")));

        var report = engine.Execute("персонаж.пересчёт", target, [rule]);

        Assert.Single(report.ExecutedRules);
        Assert.True(target.TryGetVariable("МаксимумЗдоровья", out var value));
        Assert.Equal(74, value.AsNumber());
    }

    [Fact]
    public void ИзменитьЗначение_ПрибавляетКТекущему()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой").WithVariable("Сила", 10);

        var rule = CreateRule(
            "персонаж.повышение_уровня",
            RuleTestFactory.Action("изменить_значение", ("параметр", "Сила"), ("значение", "2")));

        engine.Execute("персонаж.повышение_уровня", target, [rule]);

        target.TryGetVariable("Сила", out var value);
        Assert.Equal(12, value.AsNumber());
    }

    [Fact]
    public void ДобавитьИУдалитьЭффект_ИзменяютПризнакиОбъекта()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой").WithTag("Отравление");

        var rule = CreateRule(
            "бой.начало",
            RuleTestFactory.Action("добавить_эффект", ("признак", "Ярость")),
            RuleTestFactory.Action("удалить_эффект", ("признак", "Отравление")));

        engine.Execute("бой.начало", target, [rule]);

        Assert.True(target.HasTag("Ярость"));
        Assert.False(target.HasTag("Отравление"));
    }

    [Fact]
    public void РасходРесурса_НеОпускаетсяНижеНуля()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой").WithVariable("Мана", 3);

        var rule = CreateRule(
            "магия.применение_заклинания",
            RuleTestFactory.Action("расход_ресурса", ("ресурс", "Мана"), ("значение", "10")));

        engine.Execute("магия.применение_заклинания", target, [rule]);

        target.TryGetVariable("Мана", out var value);
        Assert.Equal(0, value.AsNumber());
    }

    [Fact]
    public void ВосстановлениеРесурса_НеПревышаетМаксимум()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой")
            .WithVariable("Здоровье", 5)
            .WithVariable("Здоровье.Максимум", 20);

        var rule = CreateRule(
            "отдых.длительный",
            RuleTestFactory.Action("восстановить_ресурс", ("ресурс", "Здоровье"), ("значение", "100")));

        engine.Execute("отдых.длительный", target, [rule]);

        target.TryGetVariable("Здоровье", out var value);
        Assert.Equal(20, value.AsNumber());
    }

    [Fact]
    public void Бросок_ЗаписываетРезультатВПараметр()
    {
        var engine = RuleTestFactory.CreateEngine(diceValue: 4);
        var target = new RuleTarget("Герой").WithVariable("Ловкость", 3);

        var rule = CreateRule(
            "бой.начало",
            RuleTestFactory.Action(
                "бросок",
                ("формула", "2d6 + Ловкость"),
                ("параметр", "Инициатива"),
                ("описание", "Инициатива")));

        engine.Execute("бой.начало", target, [rule]);

        target.TryGetVariable("Инициатива", out var value);
        Assert.Equal(11, value.AsNumber());
    }

    [Fact]
    public void НеизвестноеДействие_ОтражаетсяВОтчётеИНеПрерываетВыполнение()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой").WithVariable("Сила", 10);

        var rule = CreateRule(
            "бой.начало",
            RuleTestFactory.Action("несуществующее_действие"),
            RuleTestFactory.Action("изменить_значение", ("параметр", "Сила"), ("значение", "1")));

        var report = engine.Execute("бой.начало", target, [rule]);

        Assert.Contains(report.Outcomes, outcome => !outcome.Succeeded);

        // Отказ одного действия не должен отменять остальные.
        target.TryGetVariable("Сила", out var value);
        Assert.Equal(11, value.AsNumber());
    }
}

public sealed class RuleExecutionTests
{
    private static RuleDefinition CreateRule(string name, int priority, string value)
    {
        var rule = new RuleDefinition
        {
            Name = name,
            Trigger = "персонаж.пересчёт",
            Priority = priority,
            Enabled = true,
        };

        rule.Actions.Add(RuleTestFactory.Action(
            "установить_значение",
            ("параметр", "МаксимумЗдоровья"),
            ("значение", value)));

        return rule;
    }

    [Fact]
    public void Приоритет_ПравилоСБольшимПриоритетомПрименяетсяПоследним()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой");

        // Пример конфликта из документа 019: два правила задают максимум здоровья.
        var baseRule = CreateRule("Базовое правило", priority: 100, value: "100");
        var bossRule = CreateRule("Правило босса", priority: 300, value: "200");

        engine.Execute("персонаж.пересчёт", target, [bossRule, baseRule]);

        target.TryGetVariable("МаксимумЗдоровья", out var value);
        Assert.Equal(200, value.AsNumber());
    }

    [Fact]
    public void ОтключённоеПравило_НеВыполняется()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой");

        var rule = CreateRule("Отключённое", priority: 0, value: "50");
        rule.Enabled = false;

        var report = engine.Execute("персонаж.пересчёт", target, [rule]);

        Assert.Empty(report.ExecutedRules);
        Assert.False(target.TryGetVariable("МаксимумЗдоровья", out _));
    }

    [Fact]
    public void Правило_ДругогоСобытия_НеВыполняется()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой");

        var rule = CreateRule("Правило пересчёта", priority: 0, value: "50");

        var report = engine.Execute("бой.начало", target, [rule]);

        Assert.Empty(report.ExecutedRules);
        Assert.Empty(report.SkippedRules);
    }

    [Fact]
    public void Отчёт_РазделяетВыполненныеИПропущенныеПравила()
    {
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой").WithVariable("Уровень", 2);

        var matching = CreateRule("Подходящее", priority: 0, value: "10");
        var skipped = CreateRule("Пропущенное", priority: 0, value: "20");
        skipped.Condition = new RuleComparison
        {
            Left = "Уровень",
            Operator = RuleComparisonOperator.GreaterOrEqual,
            Right = "10",
        };

        var report = engine.Execute("персонаж.пересчёт", target, [matching, skipped]);

        Assert.Contains("Подходящее", report.ExecutedRules);
        Assert.Contains("Пропущенное", report.SkippedRules);
    }

    [Fact]
    public void ПользовательскаяМеханика_БроняДобавляетЗдоровье()
    {
        // Пример из документа 019: «Сделай броню, которая даёт HP вместо защиты».
        var engine = RuleTestFactory.CreateEngine();
        var target = new RuleTarget("Герой")
            .WithVariable("МаксимумЗдоровья", 30)
            .WithVariable("ЗначениеБрони", 4)
            .WithTag("Броня экипирована");

        var rule = new RuleDefinition
        {
            Name = "Броня даёт здоровье",
            Trigger = "предметы.экипировка",
            Enabled = true,
            Condition = new RuleComparison
            {
                Left = "признак",
                Operator = RuleComparisonOperator.Has,
                Right = "Броня экипирована",
            },
        };

        rule.Actions.Add(RuleTestFactory.Action(
            "изменить_значение",
            ("параметр", "МаксимумЗдоровья"),
            ("значение", "ЗначениеБрони * 10")));

        engine.Execute("предметы.экипировка", target, [rule]);

        target.TryGetVariable("МаксимумЗдоровья", out var value);
        Assert.Equal(70, value.AsNumber());
    }
}

public sealed class RuleSerializationTests
{
    [Fact]
    public void Условия_СохраняютсяИВосстанавливаются()
    {
        var root = new RuleConditionGroup { Operator = RuleLogicalOperator.Or, IsNegated = true };
        root.Children.Add(new RuleComparison
        {
            Left = "Сила",
            Operator = RuleComparisonOperator.GreaterOrEqual,
            Right = "15",
        });

        var nested = new RuleConditionGroup { Operator = RuleLogicalOperator.And };
        nested.Children.Add(new RuleComparison
        {
            Left = "признак",
            Operator = RuleComparisonOperator.Has,
            Right = "Ярость",
        });
        root.Children.Add(nested);

        var text = RuleSerializer.SerializeCondition(root);
        var restored = RuleSerializer.DeserializeCondition(text) as RuleConditionGroup;

        Assert.NotNull(restored);
        Assert.Equal(RuleLogicalOperator.Or, restored!.Operator);
        Assert.True(restored.IsNegated);
        Assert.Equal(2, restored.Children.Count);

        var comparison = Assert.IsType<RuleComparison>(restored.Children[0]);
        Assert.Equal("Сила", comparison.Left);
        Assert.Equal(RuleComparisonOperator.GreaterOrEqual, comparison.Operator);

        var restoredNested = Assert.IsType<RuleConditionGroup>(restored.Children[1]);
        Assert.Single(restoredNested.Children);
    }

    [Fact]
    public void Действия_СохраняютсяИВосстанавливаются()
    {
        var actions = new List<RuleAction>
        {
            RuleTestFactory.Action("установить_значение", ("параметр", "Здоровье"), ("значение", "Сила * 2")),
            RuleTestFactory.Action("добавить_эффект", ("признак", "Ярость")),
        };

        var restored = RuleSerializer.DeserializeActions(RuleSerializer.SerializeActions(actions));

        Assert.Equal(2, restored.Count);
        Assert.Equal("установить_значение", restored[0].Kind);
        Assert.Equal("Сила * 2", restored[0].GetParameter("значение"));
        Assert.Equal("Ярость", restored[1].GetParameter("признак"));
    }

    [Fact]
    public void ПовреждённыеДанные_НеПриводятКИсключению()
    {
        Assert.Null(RuleSerializer.DeserializeCondition("{ это не JSON"));
        Assert.Empty(RuleSerializer.DeserializeActions("[[["));
    }

    [Fact]
    public void РусскийТекст_СохраняетсяБезЭкранирования()
    {
        var condition = new RuleComparison
        {
            Left = "Сила",
            Operator = RuleComparisonOperator.Equal,
            Right = "Ярость",
        };

        var text = RuleSerializer.SerializeCondition(condition);

        Assert.Contains("Сила", text, StringComparison.Ordinal);
    }
}

public sealed class RuleValidatorTests
{
    private static RuleDefinition CreateValidRule(string name = "Правило")
    {
        var rule = new RuleDefinition
        {
            Name = name,
            Trigger = "персонаж.пересчёт",
            Category = RuleCategories.Character,
            Enabled = true,
        };

        rule.Actions.Add(RuleTestFactory.Action(
            "установить_значение",
            ("параметр", "Здоровье"),
            ("значение", "10")));

        return rule;
    }

    [Fact]
    public void КорректноеПравило_НеИмеетЗамечаний()
    {
        Assert.Empty(RuleTestFactory.CreateValidator().Validate(CreateValidRule()));
    }

    [Fact]
    public void ОшибкаВФормуле_Обнаруживается()
    {
        var rule = CreateValidRule();
        rule.Actions[0].Parameters["значение"] = "2 +";

        var issues = RuleTestFactory.CreateValidator().Validate(rule);

        Assert.Contains(issues, issue => issue.Severity == RuleIssueSeverity.Error);
    }

    [Fact]
    public void НезаполненныйОбязательныйПараметр_Обнаруживается()
    {
        var rule = CreateValidRule();
        rule.Actions[0].Parameters["параметр"] = string.Empty;

        var issues = RuleTestFactory.CreateValidator().Validate(rule);

        Assert.Contains(issues, issue => issue.Severity == RuleIssueSeverity.Error);
    }

    [Fact]
    public void НеизвестноеСобытие_ДаётПредупреждение()
    {
        var rule = CreateValidRule();
        rule.Trigger = "несуществующее.событие";

        var issues = RuleTestFactory.CreateValidator().Validate(rule);

        Assert.Contains(issues, issue => issue.Severity == RuleIssueSeverity.Warning);
    }

    [Fact]
    public void ПравилоБезДействий_ДаётПредупреждение()
    {
        var rule = CreateValidRule();
        rule.Actions.Clear();

        var issues = RuleTestFactory.CreateValidator().Validate(rule);

        Assert.Contains(issues, issue => issue.Severity == RuleIssueSeverity.Warning);
    }

    [Fact]
    public void НевыполнимоеУсловие_Обнаруживается()
    {
        var rule = CreateValidRule();

        var group = new RuleConditionGroup { Operator = RuleLogicalOperator.And };
        group.Children.Add(new RuleComparison
        {
            Left = "Уровень",
            Operator = RuleComparisonOperator.Greater,
            Right = "10",
        });
        group.Children.Add(new RuleComparison
        {
            Left = "Уровень",
            Operator = RuleComparisonOperator.Less,
            Right = "5",
        });

        rule.Condition = group;

        var issues = RuleTestFactory.CreateValidator().Validate(rule);

        Assert.Contains(issues, issue => issue.Message.Contains("невыполнимо", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void КонфликтПриоритетов_Обнаруживается()
    {
        var first = CreateValidRule("Первое");
        var second = CreateValidRule("Второе");

        // Оба правила изменяют один параметр при одном событии с одинаковым приоритетом.
        var issues = RuleTestFactory.CreateValidator().ValidateSet([first, second]);

        Assert.Contains(issues, issue => issue.Message.Contains("приоритетом", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void РазныеПриоритеты_КонфликтаНеСоздают()
    {
        var first = CreateValidRule("Первое");
        var second = CreateValidRule("Второе");
        second.Priority = 100;

        var issues = RuleTestFactory.CreateValidator().ValidateSet([first, second]);

        Assert.DoesNotContain(issues, issue => issue.Message.Contains("приоритетом", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void СовпадениеНазваний_ДаётПредупреждение()
    {
        var first = CreateValidRule("Одинаковое");
        var second = CreateValidRule("Одинаковое");
        second.Priority = 50;

        var issues = RuleTestFactory.CreateValidator().ValidateSet([first, second]);

        Assert.Contains(issues, issue => issue.Message.Contains("используется", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class RuleTriggerCatalogTests
{
    [Fact]
    public void Каталог_СодержитСобытияВсехКатегорий()
    {
        var catalog = new RuleTriggerCatalog([new StandardTriggerProvider()]);

        Assert.NotEmpty(catalog.Triggers);
        Assert.Contains(catalog.Triggers, trigger => trigger.Category == RuleCategories.Character);
        Assert.Contains(catalog.Triggers, trigger => trigger.Category == RuleCategories.Combat);
        Assert.Contains(catalog.Triggers, trigger => trigger.Category == RuleCategories.Magic);
        Assert.Contains(catalog.Triggers, trigger => trigger.Category == RuleCategories.Items);
        Assert.Contains(catalog.Triggers, trigger => trigger.Category == RuleCategories.Rest);
    }

    [Fact]
    public void Каталог_НаходитСобытиеПоКлючу()
    {
        var catalog = new RuleTriggerCatalog([new StandardTriggerProvider()]);

        Assert.NotNull(catalog.Find("бой.начало"));
        Assert.Null(catalog.Find("несуществующее"));
    }

    [Fact]
    public void Каталог_ПринимаетСобытияОтНесколькихПоставщиков()
    {
        var catalog = new RuleTriggerCatalog([new StandardTriggerProvider(), new CustomTriggerProvider()]);

        Assert.NotNull(catalog.Find("моя_система.начало_дня"));
    }

    /// <summary>
    /// Пример поставщика событий, добавляемого игровой системой или плагином.
    /// </summary>
    private sealed class CustomTriggerProvider : IRuleTriggerProvider
    {
        public IEnumerable<RuleTrigger> GetTriggers()
        {
            yield return new RuleTrigger(
                "моя_система.начало_дня",
                "Начало дня",
                RuleCategories.Custom,
                "Событие пользовательской игровой системы.");
        }
    }
}
