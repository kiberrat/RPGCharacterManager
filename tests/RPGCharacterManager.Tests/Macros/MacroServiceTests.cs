using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Core.Abstractions.Macros;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.GameRules.Actions;
using RPGCharacterManager.Macros;
using RPGCharacterManager.Tests.Characters;

namespace RPGCharacterManager.Tests.Macros;

/// <summary>
/// Проверка макросов: хранение, горячие клавиши и выполнение движком правил.
/// </summary>
public sealed class MacroServiceTests
{
    /// <summary>Значение, выпадающее на кубиках проверок.</summary>
    private const int DiceValue = 4;

    private static MacroService CreateService(CharacterTestContext context) => new(
        context.ContextFactory,
        context.Progression,
        new RPGCharacterManager.Infrastructure.Events.InMemoryEventBus(
            NullLogger<RPGCharacterManager.Infrastructure.Events.InMemoryEventBus>.Instance),
        NullLogger<MacroService>.Instance);

    private static RuleAction Adjust(string target, string value)
    {
        var action = new RuleAction { Kind = "изменить_значение" };

        action.Parameters[RuleActionParameterNames.Target] = target;
        action.Parameters[RuleActionParameterNames.Value] = value;

        return action;
    }

    private static async Task<Guid> CreateCharacterAsync(CharacterTestContext context, string name = "Аргус")
    {
        await context.AddAsync(CharacterContent.Attribute("Сила", "Сила", defaultValue: 10));

        var draft = new CharacterDraft { Level = 3 };
        draft.Character.Name = name;

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    [Fact]
    public async Task Макрос_СохраняетсяИЧитается()
    {
        await using var characters = await CharacterTestContext.CreateWithDiceAsync(DiceValue);
        var service = CreateService(characters);

        var draft = new MacroDefinition { Name = "Ярость", Category = "бой", Hotkey = "Ctrl+1" };
        draft.Actions.Add(Adjust("Сила", "2"));

        var saved = await service.SaveAsync(draft);
        Assert.True(saved.IsSuccess, saved.Error);

        var loaded = await service.GetAsync(saved.Value);
        Assert.True(loaded.IsSuccess, loaded.Error);

        Assert.Equal("Ярость", loaded.Value.Name);
        Assert.Equal("Ctrl+1", loaded.Value.Hotkey);

        // Действия хранятся теми же структурами, что у правил, и читаются обратно.
        var action = Assert.Single(loaded.Value.Actions);
        Assert.Equal("изменить_значение", action.Kind);
        Assert.Equal("Сила", action.GetParameter(RuleActionParameterNames.Target));
    }

    [Fact]
    public async Task Макрос_БезНазвания_НеСохраняется()
    {
        await using var characters = await CharacterTestContext.CreateWithDiceAsync(DiceValue);
        var service = CreateService(characters);

        Assert.True((await service.SaveAsync(new MacroDefinition { Name = "  " })).IsFailure);
    }

    [Fact]
    public async Task ГорячаяКлавиша_ЗанятаОднимМакросом()
    {
        await using var characters = await CharacterTestContext.CreateWithDiceAsync(DiceValue);
        var service = CreateService(characters);

        Assert.True((await service.SaveAsync(new MacroDefinition { Name = "Первый", Hotkey = "Ctrl+1" })).IsSuccess);

        // Одно сочетание на два макроса означало бы, что нажатие выполняет
        // неизвестно который из них.
        var second = await service.SaveAsync(new MacroDefinition { Name = "Второй", Hotkey = "Ctrl+1" });

        Assert.True(second.IsFailure);
        Assert.Contains("Ctrl+1", second.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ГорячиеКлавиши_ВозвращаютТолькоВключённыеМакросыССочетанием()
    {
        await using var characters = await CharacterTestContext.CreateWithDiceAsync(DiceValue);
        var service = CreateService(characters);

        Assert.True((await service.SaveAsync(new MacroDefinition { Name = "С клавишей", Hotkey = "Ctrl+1" })).IsSuccess);
        Assert.True((await service.SaveAsync(new MacroDefinition { Name = "Без клавиши" })).IsSuccess);
        Assert.True((await service.SaveAsync(
            new MacroDefinition { Name = "Выключенный", Hotkey = "Ctrl+2", Enabled = false })).IsSuccess);

        var hotkeys = await service.GetHotkeysAsync();

        Assert.Equal("С клавишей", Assert.Single(hotkeys).Name);
    }

    [Fact]
    public async Task Макрос_ВыполняетДействияНадПерсонажем()
    {
        await using var characters = await CharacterTestContext.CreateWithDiceAsync(DiceValue);
        var service = CreateService(characters);

        var characterId = await CreateCharacterAsync(characters);

        var draft = new MacroDefinition { Name = "Прилив сил" };
        draft.Actions.Add(Adjust("Сила", "2"));

        var saved = await service.SaveAsync(draft);
        Assert.True(saved.IsSuccess, saved.Error);

        var run = await service.RunAsync(saved.Value, characterId);

        Assert.True(run.IsSuccess, run.Error);
        Assert.True(run.Value.WasConditionMet);

        var character = await characters.LoadCharacterAsync(characterId);

        Assert.Equal(12, character.Attributes.Single().CurrentValue);
    }

    [Fact]
    public async Task Макрос_СНевыполненнымУсловием_НичегоНеМеняет()
    {
        await using var characters = await CharacterTestContext.CreateWithDiceAsync(DiceValue);
        var service = CreateService(characters);

        var characterId = await CreateCharacterAsync(characters);

        var draft = new MacroDefinition
        {
            Name = "Только для сильных",

            // Условия — те же структуры, что у правил, и проверяет их тот же движок.
            Condition = new RuleComparison
            {
                Left = "Сила",
                Operator = RuleComparisonOperator.Greater,
                Right = "100",
            },
        };

        draft.Actions.Add(Adjust("Сила", "5"));

        var saved = await service.SaveAsync(draft);
        Assert.True(saved.IsSuccess, saved.Error);

        var run = await service.RunAsync(saved.Value, characterId);

        Assert.True(run.IsSuccess, run.Error);
        Assert.False(run.Value.WasConditionMet);

        var character = await characters.LoadCharacterAsync(characterId);

        Assert.Equal(10, character.Attributes.Single().CurrentValue);
    }

    [Fact]
    public async Task Макрос_УсловиеИспользуетФормулу()
    {
        await using var characters = await CharacterTestContext.CreateWithDiceAsync(DiceValue);
        var service = CreateService(characters);

        var characterId = await CreateCharacterAsync(characters);

        var draft = new MacroDefinition
        {
            Name = "По формуле",

            // Сила 10 плюс уровень 3 — больше двенадцати, условие выполняется.
            Condition = new RuleComparison
            {
                Left = "Сила + Уровень",
                Operator = RuleComparisonOperator.Greater,
                Right = "12",
            },
        };

        draft.Actions.Add(Adjust("Сила", "1"));

        var saved = await service.SaveAsync(draft);
        Assert.True(saved.IsSuccess, saved.Error);

        var run = await service.RunAsync(saved.Value, characterId);

        Assert.True(run.IsSuccess, run.Error);
        Assert.True(run.Value.WasConditionMet);
    }

    [Fact]
    public async Task Макрос_ВыполняетДействияПоПорядку()
    {
        await using var characters = await CharacterTestContext.CreateWithDiceAsync(DiceValue);
        var service = CreateService(characters);

        var characterId = await CreateCharacterAsync(characters);

        var draft = new MacroDefinition { Name = "Последовательность" };
        draft.Actions.Add(Adjust("Сила", "2"));
        draft.Actions.Add(Adjust("Сила", "3"));

        var saved = await service.SaveAsync(draft);
        Assert.True(saved.IsSuccess, saved.Error);

        Assert.True((await service.RunAsync(saved.Value, characterId)).IsSuccess);

        var character = await characters.LoadCharacterAsync(characterId);

        // Оба действия применились: 10 + 2 + 3.
        Assert.Equal(15, character.Attributes.Single().CurrentValue);
    }

    [Fact]
    public async Task Макрос_БезДействий_НеВыполняется()
    {
        await using var characters = await CharacterTestContext.CreateWithDiceAsync(DiceValue);
        var service = CreateService(characters);

        var characterId = await CreateCharacterAsync(characters);
        var saved = await service.SaveAsync(new MacroDefinition { Name = "Пустой" });

        Assert.True(saved.IsSuccess, saved.Error);
        Assert.True((await service.RunAsync(saved.Value, characterId)).IsFailure);
    }

    [Fact]
    public async Task Выключенный_Макрос_НеВыполняется()
    {
        await using var characters = await CharacterTestContext.CreateWithDiceAsync(DiceValue);
        var service = CreateService(characters);

        var characterId = await CreateCharacterAsync(characters);

        var draft = new MacroDefinition { Name = "Выключенный", Enabled = false };
        draft.Actions.Add(Adjust("Сила", "2"));

        var saved = await service.SaveAsync(draft);
        Assert.True(saved.IsSuccess, saved.Error);

        Assert.True((await service.RunAsync(saved.Value, characterId)).IsFailure);
    }

    [Fact]
    public async Task Макрос_ЧужогоПерсонажа_НеВыполняется()
    {
        await using var characters = await CharacterTestContext.CreateWithDiceAsync(DiceValue);
        var service = CreateService(characters);

        var first = await CreateCharacterAsync(characters, "Аргус");
        var second = await CreateCharacterAsync(characters, "Люциус");

        var draft = new MacroDefinition { Name = "Личный", CharacterId = first };
        draft.Actions.Add(Adjust("Сила", "2"));

        var saved = await service.SaveAsync(draft);
        Assert.True(saved.IsSuccess, saved.Error);

        Assert.True((await service.RunAsync(saved.Value, second)).IsFailure);
        Assert.True((await service.RunAsync(saved.Value, first)).IsSuccess);
    }

    [Fact]
    public async Task Макрос_Удаляется()
    {
        await using var characters = await CharacterTestContext.CreateWithDiceAsync(DiceValue);
        var service = CreateService(characters);

        var saved = await service.SaveAsync(new MacroDefinition { Name = "Временный" });
        Assert.True(saved.IsSuccess, saved.Error);

        Assert.True((await service.DeleteAsync(saved.Value)).IsSuccess);

        var all = await service.GetAllAsync();

        Assert.True(all.IsSuccess, all.Error);
        Assert.Empty(all.Value);
    }

    [Fact]
    public async Task Список_ПоказываетСоставМакроса()
    {
        await using var characters = await CharacterTestContext.CreateWithDiceAsync(DiceValue);
        var service = CreateService(characters);

        var draft = new MacroDefinition
        {
            Name = "Проверка",
            Condition = new RuleComparison { Left = "Сила", Operator = RuleComparisonOperator.Greater, Right = "1" },
        };

        draft.Actions.Add(Adjust("Сила", "1"));
        draft.Actions.Add(Adjust("Сила", "2"));

        Assert.True((await service.SaveAsync(draft)).IsSuccess);

        var all = await service.GetAllAsync();
        Assert.True(all.IsSuccess, all.Error);

        var item = Assert.Single(all.Value);

        Assert.Equal(2, item.ActionCount);
        Assert.True(item.HasCondition);
        Assert.Equal("действий: 2 · с условием", item.Summary);
    }
}
