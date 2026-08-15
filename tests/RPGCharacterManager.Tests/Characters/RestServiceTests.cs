using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Core.Models.Rules;
using RPGCharacterManager.Tests.Rules;

namespace RPGCharacterManager.Tests.Characters;

/// <summary>
/// Проверка отдыха: восстановление ресурсов, требования, течение времени
/// и правила события отдыха.
/// </summary>
public sealed class RestServiceTests
{
    private static async Task<Guid> CreateCharacterAsync(CharacterTestContext context, int level = 3)
    {
        var draft = new CharacterDraft { Level = level };
        draft.Character.Name = "Странник";

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static async Task<GameResource> GiveResourceAsync(
        CharacterTestContext context,
        string name = "Здоровье",
        string systemName = "здоровье",
        string maximumFormula = "20")
    {
        var resource = CharacterContent.Resource(name, systemName, maximumFormula, maximumFormula);
        await context.AddAsync(resource);

        return resource;
    }

    private static async Task<double> SpendAsync(
        CharacterTestContext context,
        Guid characterId,
        Guid resourceId,
        double value)
    {
        var sheet = await context.Sheets.LoadAsync(characterId);
        Assert.True(sheet.IsSuccess, sheet.Error);

        var resource = sheet.Value.Character.Resources.Single(entry => entry.ResourceId == resourceId);
        resource.Current = value;

        var saved = await context.Sheets.SaveAsync(sheet.Value.Character, new Dictionary<Guid, string?>());
        Assert.True(saved.IsSuccess, saved.Error);

        return value;
    }

    private static async Task<double> CurrentAsync(
        CharacterTestContext context,
        Guid characterId,
        Guid resourceId)
    {
        var sheet = await context.Sheets.LoadAsync(characterId);
        Assert.True(sheet.IsSuccess, sheet.Error);

        return sheet.Value.Character.Resources.Single(entry => entry.ResourceId == resourceId).Current;
    }

    private static async Task<RestResult> RestAsync(
        CharacterTestContext context,
        Guid characterId,
        Guid restTypeId)
    {
        var result = await context.Rests.RestAsync(characterId, restTypeId);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    // ---------- Список видов отдыха ----------

    [Fact]
    public async Task Отдых_ВидыОтдыха_ПеречисленыВЗаданномПорядке()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        await context.AddAsync(
            CharacterContent.Rest("Длительный отдых", "длительный", 8, "час", sortOrder: 2),
            CharacterContent.Rest("Короткий отдых", "короткий", 1, "час", sortOrder: 1));

        var characterId = await CreateCharacterAsync(context);

        var state = await context.Rests.GetAsync(characterId);
        Assert.True(state.IsSuccess, state.Error);

        Assert.Equal(
            ["Короткий отдых", "Длительный отдых"],
            state.Value.Options.Select(option => option.Name));

        Assert.Equal("1 час", state.Value.Options[0].Duration);
    }

    [Fact]
    public async Task Отдых_НевыполненноеТребование_ОтдыхНедоступен()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        await context.AddAsync(
            CharacterContent.Rest("Медитация", "медитация", requirements: "уровень >= 10"));

        var characterId = await CreateCharacterAsync(context, level: 3);

        var state = await context.Rests.GetAsync(characterId);
        Assert.True(state.IsSuccess, state.Error);

        var option = Assert.Single(state.Value.Options);

        Assert.False(option.IsAvailable);
        Assert.NotNull(option.UnavailableReason);
    }

    [Fact]
    public async Task Отдых_НевыполненноеТребование_ВыполнитьНельзя()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var rest = CharacterContent.Rest("Медитация", "медитация", requirements: "уровень >= 10");
        await context.AddAsync(rest);

        var characterId = await CreateCharacterAsync(context, level: 3);

        var result = await context.Rests.RestAsync(characterId, rest.Id);

        Assert.True(result.IsFailure);
    }

    // ---------- Восстановление ресурсов ----------

    [Fact]
    public async Task Отдых_ПолноеВосстановление_ПоднимаетРесурсДоМаксимума()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var health = await GiveResourceAsync(context);

        var rest = CharacterContent.Rest(
            "Длительный отдых",
            "длительный",
            restores: CharacterContent.Restore(health.Id));

        await context.AddAsync(rest);

        var characterId = await CreateCharacterAsync(context);

        await SpendAsync(context, characterId, health.Id, 4);

        var result = await RestAsync(context, characterId, rest.Id);

        Assert.Equal(20, await CurrentAsync(context, characterId, health.Id));

        var change = Assert.Single(result.Changes);
        Assert.Equal("Здоровье", change.ResourceName);
        Assert.Equal(4, change.Before);
        Assert.Equal(20, change.After);
    }

    [Fact]
    public async Task Отдых_ВосстановлениеПоФормуле_ВидитМаксимумРесурса()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var health = await GiveResourceAsync(context);

        var rest = CharacterContent.Rest(
            "Короткий отдых",
            "короткий",
            restores: CharacterContent.Restore(
                health.Id,
                RestRestoreMode.Formula,
                $"{RestVariables.Maximum} / 2"));

        await context.AddAsync(rest);

        var characterId = await CreateCharacterAsync(context);

        await SpendAsync(context, characterId, health.Id, 2);
        await RestAsync(context, characterId, rest.Id);

        Assert.Equal(12, await CurrentAsync(context, characterId, health.Id));
    }

    [Fact]
    public async Task Отдых_ВосстановлениеПоФормуле_НеПревышаетМаксимум()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var health = await GiveResourceAsync(context);

        var rest = CharacterContent.Rest(
            "Короткий отдых",
            "короткий",
            restores: CharacterContent.Restore(health.Id, RestRestoreMode.Formula, "100"));

        await context.AddAsync(rest);

        var characterId = await CreateCharacterAsync(context);

        await SpendAsync(context, characterId, health.Id, 5);
        await RestAsync(context, characterId, rest.Id);

        Assert.Equal(20, await CurrentAsync(context, characterId, health.Id));
    }

    [Fact]
    public async Task Отдых_ФормулаОтПеременнойПерсонажа_Вычисляется()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var health = await GiveResourceAsync(context);

        var rest = CharacterContent.Rest(
            "Перевязка",
            "перевязка",
            restores: CharacterContent.Restore(health.Id, RestRestoreMode.Formula, "уровень"));

        await context.AddAsync(rest);

        var characterId = await CreateCharacterAsync(context, level: 3);

        await SpendAsync(context, characterId, health.Id, 1);
        await RestAsync(context, characterId, rest.Id);

        Assert.Equal(4, await CurrentAsync(context, characterId, health.Id));
    }

    [Fact]
    public async Task Отдых_МаксимумВыросПослеНадеванияБрони_ВосстанавливаетДоНовогоМаксимума()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        await context.AddAsync(strength);

        // Максимум здоровья зависит от Силы, а Силу повышает надетая броня.
        var health = await GiveResourceAsync(context, maximumFormula: "сила * 2");

        var slot = CharacterContent.Slot("Тело", "тело");
        await context.AddAsync(slot);

        var armour = CharacterContent.Equipment(
            "Кираса",
            "кираса",
            slot.Id,
            null,
            CharacterContent.Bonus(BonusTargetKind.Attribute, "4", strength.Id));

        await context.AddAsync(armour);

        var rest = CharacterContent.Rest(
            "Длительный отдых",
            "длительный",
            restores: CharacterContent.Restore(health.Id));

        await context.AddAsync(rest);

        var characterId = await CreateCharacterAsync(context);

        await SpendAsync(context, characterId, health.Id, 3);

        // Надевание брони не пересчитывает сохранённый максимум ресурса,
        // поэтому отдых обязан пересчитать персонажа до восстановления.
        var equipped = await context.Equipment.EquipAsync(characterId, armour.Id);
        Assert.True(equipped.IsSuccess, equipped.Error);

        var result = await RestAsync(context, characterId, rest.Id);

        Assert.Equal(28, await CurrentAsync(context, characterId, health.Id));
        Assert.Equal(28, Assert.Single(result.Changes).After);
    }

    [Fact]
    public async Task Отдых_БезВыбранногоРесурса_ВосстанавливаетВсеРесурсы()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var health = await GiveResourceAsync(context);
        var mana = await GiveResourceAsync(context, "Мана", "мана", "10");

        var rest = CharacterContent.Rest(
            "Длительный отдых",
            "длительный",
            restores: CharacterContent.Restore());

        await context.AddAsync(rest);

        var characterId = await CreateCharacterAsync(context);

        await SpendAsync(context, characterId, health.Id, 3);
        await SpendAsync(context, characterId, mana.Id, 1);

        var result = await RestAsync(context, characterId, rest.Id);

        Assert.Equal(20, await CurrentAsync(context, characterId, health.Id));
        Assert.Equal(10, await CurrentAsync(context, characterId, mana.Id));
        Assert.Equal(2, result.Changes.Count);
    }

    [Fact]
    public async Task Отдых_НевыполненноеУсловиеВосстановления_РесурсНеТрогает()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var health = await GiveResourceAsync(context);

        var rest = CharacterContent.Rest(
            "Короткий отдых",
            "короткий",
            restores: CharacterContent.Restore(health.Id, condition: "уровень >= 10"));

        await context.AddAsync(rest);

        var characterId = await CreateCharacterAsync(context, level: 3);

        await SpendAsync(context, characterId, health.Id, 6);

        var result = await RestAsync(context, characterId, rest.Id);

        Assert.Equal(6, await CurrentAsync(context, characterId, health.Id));
        Assert.Empty(result.Changes);
    }

    [Fact]
    public async Task Отдых_БезВосстановлений_НичегоНеМеняет()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var health = await GiveResourceAsync(context);
        var rest = CharacterContent.Rest("Передышка", "передышка");

        await context.AddAsync(rest);

        var characterId = await CreateCharacterAsync(context);

        await SpendAsync(context, characterId, health.Id, 7);

        var result = await RestAsync(context, characterId, rest.Id);

        Assert.Equal(7, await CurrentAsync(context, characterId, health.Id));
        Assert.Empty(result.Changes);
    }

    // ---------- Течение времени ----------

    [Fact]
    public async Task Отдых_СДлительностью_ЗавершаетЭффектыВТойЖеЕдинице()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var effect = CharacterContent.Effect(
            "Благословение",
            "благословение",
            durationFormula: "2",
            durationUnit: "час");

        var rest = CharacterContent.Rest("Длительный отдых", "длительный", 8, "час");

        await context.AddAsync(effect);
        await context.AddAsync(rest);

        var characterId = await CreateCharacterAsync(context);

        var applied = await context.Effects.ApplyAsync(characterId, effect.Id);
        Assert.True(applied.IsSuccess, applied.Error);

        var result = await RestAsync(context, characterId, rest.Id);

        Assert.Contains("Благословение", result.Expired);

        var state = await context.Effects.GetAsync(characterId);
        Assert.True(state.IsSuccess, state.Error);
        Assert.Empty(state.Value.Effects);
    }

    [Fact]
    public async Task Отдых_ДругаяЕдиницаДлительности_ЭффектНеТрогает()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var effect = CharacterContent.Effect(
            "Ускорение",
            "ускорение",
            durationFormula: "3",
            durationUnit: "раунд");

        var rest = CharacterContent.Rest("Длительный отдых", "длительный", 8, "час");

        await context.AddAsync(effect);
        await context.AddAsync(rest);

        var characterId = await CreateCharacterAsync(context);

        var applied = await context.Effects.ApplyAsync(characterId, effect.Id);
        Assert.True(applied.IsSuccess, applied.Error);

        var result = await RestAsync(context, characterId, rest.Id);

        Assert.Empty(result.Expired);

        var state = await context.Effects.GetAsync(characterId);
        Assert.True(state.IsSuccess, state.Error);
        Assert.Single(state.Value.Effects);
    }

    [Fact]
    public async Task Отдых_БезДлительности_ВремяНеИдёт()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var effect = CharacterContent.Effect(
            "Благословение",
            "благословение",
            durationFormula: "1",
            durationUnit: "час");

        var rest = CharacterContent.Rest("Передышка", "передышка");

        await context.AddAsync(effect);
        await context.AddAsync(rest);

        var characterId = await CreateCharacterAsync(context);

        var applied = await context.Effects.ApplyAsync(characterId, effect.Id);
        Assert.True(applied.IsSuccess, applied.Error);

        var result = await RestAsync(context, characterId, rest.Id);

        Assert.Empty(result.Expired);
        Assert.Single((await context.Effects.GetAsync(characterId)).Value.Effects);
    }

    // ---------- Правила события ----------

    [Fact]
    public async Task Отдых_ПравилоСобытия_ПрименяетсяПоВнутреннемуИмениОтдыха()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        await context.AddAsync(strength);

        var rest = CharacterContent.Rest("Длительный отдых", "длительный", 8, "час");
        await context.AddAsync(rest);

        // Внутреннее имя вида отдыха задаёт ключ события, поэтому правило
        // для «отдых.длительный» относится именно к этому отдыху.
        var rule = new RuleDefinition
        {
            Name = "Отдохнувший",
            Trigger = RuleTriggers.Rest("длительный"),
            Category = RuleCategories.Rest,
        };

        rule.Actions.Add(RuleTestFactory.Action(
            "установить_значение",
            ("параметр", "сила"),
            ("значение", "14")));

        await context.Rules.SaveAsync(rule);

        var characterId = await CreateCharacterAsync(context);

        var result = await RestAsync(context, characterId, rest.Id);

        Assert.Contains("Отдохнувший", result.AppliedRules);

        var sheet = await context.Sheets.LoadAsync(characterId);
        Assert.True(sheet.IsSuccess, sheet.Error);
        Assert.Equal(14, Assert.Single(sheet.Value.Attributes).Value);
    }

    // ---------- Журнал ----------

    [Fact]
    public async Task Отдых_ЗаписываетсяВЖурналСИзменениямиРесурсов()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var health = await GiveResourceAsync(context);

        var rest = CharacterContent.Rest(
            "Длительный отдых",
            "длительный",
            8,
            "час",
            restores: CharacterContent.Restore(health.Id));

        await context.AddAsync(rest);

        var characterId = await CreateCharacterAsync(context);

        await SpendAsync(context, characterId, health.Id, 5);
        await RestAsync(context, characterId, rest.Id);

        await using var db = await context.CreateContextAsync();

        var entries = db.History
            .Where(entry => entry.CharacterId == characterId)
            .ToList();

        var restEntry = Assert.Single(entries, entry => entry.Action == HistoryActions.Rest);
        Assert.Contains("Длительный отдых", restEntry.Description, StringComparison.Ordinal);

        Assert.Contains(
            entries,
            entry => entry.Action == HistoryActions.ResourceChanged
                && entry.OldValue == "5"
                && entry.NewValue == "20");
    }
}
