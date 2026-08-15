using Microsoft.EntityFrameworkCore;
using RPGCharacterManager.Characters;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Tests.Characters;

/// <summary>
/// Проверка книги заклинаний: уровней и кантрипов, изучения, подготовки,
/// применения с расходом ресурсов и усилением, концентрации и истории.
/// </summary>
public sealed class SpellbookServiceTests
{
    private static async Task<Guid> CreateCharacterAsync(
        CharacterTestContext context,
        Guid? gameSystemId = null,
        int level = 1)
    {
        var draft = new CharacterDraft { Level = level, GameSystemId = gameSystemId };
        draft.Character.Name = "Волшебник";

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static async Task<Core.Abstractions.Characters.SpellbookState> LoadAsync(
        CharacterTestContext context,
        Guid characterId,
        string? search = null)
    {
        var result = await context.Spellbook.GetAsync(characterId, search);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static async Task<Guid> LearnAsync(
        CharacterTestContext context,
        Guid characterId,
        Spell spell)
    {
        await context.AddAsync(spell);

        var learned = await context.Spellbook.LearnAsync(characterId, spell.Id);
        Assert.True(learned.IsSuccess, learned.Error);

        var state = await LoadAsync(context, characterId);

        return state.Levels
            .SelectMany(level => level.Spells)
            .Single(entry => entry.SpellId == spell.Id)
            .CharacterSpellId;
    }

    private static async Task SetResourceAsync(
        CharacterTestContext context,
        Guid characterId,
        Guid resourceId,
        double current)
    {
        await using var database = await context.CreateContextAsync();

        var record = await database.CharacterResources
            .SingleAsync(entry => entry.CharacterId == characterId && entry.ResourceId == resourceId);

        record.Current = current;

        await database.SaveChangesAsync();
    }

    private static async Task<double> ResourceValueAsync(
        CharacterTestContext context,
        Guid characterId,
        Guid resourceId)
    {
        await using var database = await context.CreateContextAsync();

        var record = await database.CharacterResources
            .SingleAsync(entry => entry.CharacterId == characterId && entry.ResourceId == resourceId);

        return record.Current;
    }

    // ---------- Уровни и кантрипы ----------

    [Fact]
    public async Task КнигаЗаклинаний_ГруппируетсяПоУровням()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await LearnAsync(context, characterId, CharacterContent.Spell("Луч холода", "луч_холода", level: 0));
        await LearnAsync(context, characterId, CharacterContent.Spell("Щит", "щит", level: 1));
        await LearnAsync(context, characterId, CharacterContent.Spell("Огненный шар", "огненный_шар", level: 3));

        var state = await LoadAsync(context, characterId);

        Assert.Equal(3, state.Levels.Count);
        Assert.Equal(["Кантрипы", "1 уровень", "3 уровень"], state.Levels.Select(level => level.Title));
        Assert.Equal("Луч холода", Assert.Single(state.Levels[0].Spells).Name);
    }

    // ---------- Изучение ----------

    [Fact]
    public async Task ПределИзвестных_ЗапрещаетИзучениеСверхФормулы()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var system = new GameSystem
        {
            Name = "Система",
            SystemName = "система",
            KnownSpellsFormula = "1",
        };

        await context.AddAsync(system);

        var characterId = await CreateCharacterAsync(context, system.Id);

        await LearnAsync(context, characterId, CharacterContent.Spell("Щит", "щит"));

        var second = CharacterContent.Spell("Полёт", "полёт");
        await context.AddAsync(second);

        var learned = await context.Spellbook.LearnAsync(characterId, second.Id);

        Assert.True(learned.IsFailure);
        Assert.Contains("Предел известных", learned.Error, StringComparison.CurrentCulture);

        var state = await LoadAsync(context, characterId);

        Assert.Equal(1, state.Known.Count);
        Assert.Equal(1, state.Known.Limit);
        Assert.True(state.Known.IsReached);
    }

    [Fact]
    public async Task ЗабытоеЗаклинание_ОсвобождаетМестоВКниге()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);
        var recordId = await LearnAsync(context, characterId, CharacterContent.Spell("Щит", "щит"));

        var forgotten = await context.Spellbook.ForgetAsync(characterId, recordId);
        Assert.True(forgotten.IsSuccess, forgotten.Error);

        var state = await LoadAsync(context, characterId);

        Assert.True(state.IsEmpty);
        Assert.Equal(0, state.Known.Count);
    }

    // ---------- Подготовка ----------

    [Fact]
    public async Task ПределПодготовленных_ОграничиваетПодготовку()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var system = new GameSystem
        {
            Name = "Система",
            SystemName = "система",
            PreparedSpellsFormula = "1",
        };

        await context.AddAsync(system);

        var characterId = await CreateCharacterAsync(context, system.Id);

        var first = await LearnAsync(context, characterId, CharacterContent.Spell("Щит", "щит"));
        var second = await LearnAsync(context, characterId, CharacterContent.Spell("Полёт", "полёт"));

        Assert.True((await context.Spellbook.SetPreparedAsync(characterId, first, true)).IsSuccess);

        var blocked = await context.Spellbook.SetPreparedAsync(characterId, second, true);

        Assert.True(blocked.IsFailure);
        Assert.Contains("Предел подготовленных", blocked.Error, StringComparison.CurrentCulture);
    }

    [Fact]
    public async Task НеподготовленноеЗаклинание_НеПрименяетсяВСистемеСПодготовкой()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var system = new GameSystem
        {
            Name = "Система",
            SystemName = "система",
            PreparedSpellsFormula = "5",
        };

        await context.AddAsync(system);

        var characterId = await CreateCharacterAsync(context, system.Id);
        var recordId = await LearnAsync(context, characterId, CharacterContent.Spell("Щит", "щит"));

        var cast = await context.Spellbook.CastAsync(characterId, recordId);

        Assert.True(cast.IsFailure);
        Assert.Contains("не подготовлено", cast.Error, StringComparison.CurrentCulture);
    }

    [Fact]
    public async Task Кантрип_ПрименяетсяБезПодготовки()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var system = new GameSystem
        {
            Name = "Система",
            SystemName = "система",
            PreparedSpellsFormula = "1",
        };

        await context.AddAsync(system);

        var characterId = await CreateCharacterAsync(context, system.Id);
        var recordId = await LearnAsync(
            context,
            characterId,
            CharacterContent.Spell("Луч холода", "луч_холода", level: 0, formula: "1к8"));

        var cast = await context.Spellbook.CastAsync(characterId, recordId);

        Assert.True(cast.IsSuccess, cast.Error);
        Assert.Equal(3, cast.Value.Result);
    }

    // ---------- Расход ресурсов ----------

    [Fact]
    public async Task Применение_РасходуетРесурсПоФормулеСтоимости()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var mana = CharacterContent.Resource("Мана", "мана", maximumFormula: "10");
        await context.AddAsync(mana);

        var characterId = await CreateCharacterAsync(context);

        await SetResourceAsync(context, characterId, mana.Id, current: 10);

        var recordId = await LearnAsync(
            context,
            characterId,
            CharacterContent.Spell(
                "Огненный шар",
                "огненный_шар",
                level: 3,
                formula: "8к6",
                resourceId: mana.Id,
                resourceCostFormula: "уровень_применения"));

        var cast = await context.Spellbook.CastAsync(characterId, recordId);

        Assert.True(cast.IsSuccess, cast.Error);
        Assert.Equal(3, cast.Value.ResourceSpent);
        Assert.Equal(7, cast.Value.ResourceRemaining);
        Assert.Equal(24, cast.Value.Result);
        Assert.Equal(7, await ResourceValueAsync(context, characterId, mana.Id));
    }

    [Fact]
    public async Task НехваткаРесурса_НеДаётПрименитьЗаклинание()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var mana = CharacterContent.Resource("Мана", "мана", maximumFormula: "10");
        await context.AddAsync(mana);

        var characterId = await CreateCharacterAsync(context);

        await SetResourceAsync(context, characterId, mana.Id, current: 1);

        var recordId = await LearnAsync(
            context,
            characterId,
            CharacterContent.Spell(
                "Щит",
                "щит",
                resourceId: mana.Id,
                resourceCostFormula: "2"));

        var cast = await context.Spellbook.CastAsync(characterId, recordId);

        Assert.True(cast.IsFailure);
        Assert.Contains("Не хватает ресурса", cast.Error, StringComparison.CurrentCulture);
        Assert.Equal(1, await ResourceValueAsync(context, characterId, mana.Id));
    }

    [Fact]
    public async Task ПустаяФормулаСтоимости_ОзначаетЕдиницуРесурса()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var mana = CharacterContent.Resource("Мана", "мана", maximumFormula: "5");
        await context.AddAsync(mana);

        var characterId = await CreateCharacterAsync(context);

        await SetResourceAsync(context, characterId, mana.Id, current: 5);

        var recordId = await LearnAsync(
            context,
            characterId,
            CharacterContent.Spell("Щит", "щит", resourceId: mana.Id));

        var cast = await context.Spellbook.CastAsync(characterId, recordId);

        Assert.True(cast.IsSuccess, cast.Error);
        Assert.Equal(1, cast.Value.ResourceSpent);
        Assert.Equal(4, await ResourceValueAsync(context, characterId, mana.Id));
    }

    // ---------- Усиление ----------

    [Fact]
    public async Task ПрименениеВышеУровня_УсиливаетРезультатФормулой()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var recordId = await LearnAsync(
            context,
            characterId,
            CharacterContent.Spell(
                "Огненный шар",
                "огненный_шар",
                level: 3,
                formula: "8к6",
                scalingFormula: "результат + уровни_сверх * 3"));

        var baseCast = await context.Spellbook.CastAsync(characterId, recordId);
        Assert.True(baseCast.IsSuccess, baseCast.Error);
        Assert.Equal(24, baseCast.Value.Result);

        var upcast = await context.Spellbook.CastAsync(characterId, recordId, castLevel: 5);
        Assert.True(upcast.IsSuccess, upcast.Error);
        Assert.Equal(30, upcast.Value.Result);
    }

    [Fact]
    public async Task ПрименениеНижеУровня_Запрещено()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var recordId = await LearnAsync(
            context,
            characterId,
            CharacterContent.Spell("Огненный шар", "огненный_шар", level: 3));

        var cast = await context.Spellbook.CastAsync(characterId, recordId, castLevel: 1);

        Assert.True(cast.IsFailure);
    }

    // ---------- Концентрация ----------

    [Fact]
    public async Task НоваяКонцентрация_ПрерываетПредыдущую()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var first = await LearnAsync(
            context,
            characterId,
            CharacterContent.Spell("Полёт", "полёт", requiresConcentration: true));

        var second = await LearnAsync(
            context,
            characterId,
            CharacterContent.Spell("Невидимость", "невидимость", requiresConcentration: true));

        var firstCast = await context.Spellbook.CastAsync(characterId, first);
        Assert.True(firstCast.IsSuccess, firstCast.Error);
        Assert.True(firstCast.Value.IsConcentrating);
        Assert.Null(firstCast.Value.BrokeConcentration);

        var secondCast = await context.Spellbook.CastAsync(characterId, second);
        Assert.True(secondCast.IsSuccess, secondCast.Error);
        Assert.Equal("Полёт", secondCast.Value.BrokeConcentration);

        var state = await LoadAsync(context, characterId);

        Assert.Equal("Невидимость", state.ConcentratingOn);
    }

    [Fact]
    public async Task ПрерваннаяКонцентрация_СнимаетсяСоВсехЗаклинаний()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var recordId = await LearnAsync(
            context,
            characterId,
            CharacterContent.Spell("Полёт", "полёт", requiresConcentration: true));

        Assert.True((await context.Spellbook.CastAsync(characterId, recordId)).IsSuccess);

        var stopped = await context.Spellbook.StopConcentrationAsync(characterId);
        Assert.True(stopped.IsSuccess, stopped.Error);

        var state = await LoadAsync(context, characterId);

        Assert.Null(state.ConcentratingOn);
        Assert.False(state.IsConcentrating);
    }

    // ---------- История ----------

    [Fact]
    public async Task Применение_ЗаписываетсяВИсториюИСчитаетПрименения()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var recordId = await LearnAsync(
            context,
            characterId,
            CharacterContent.Spell("Луч холода", "луч_холода", level: 0, formula: "1к8"));

        Assert.True((await context.Spellbook.CastAsync(characterId, recordId)).IsSuccess);
        Assert.True((await context.Spellbook.CastAsync(characterId, recordId)).IsSuccess);

        var state = await LoadAsync(context, characterId);

        Assert.Equal(2, state.History.Count);
        Assert.All(state.History, record => Assert.Contains("Луч холода", record.Description, StringComparison.CurrentCulture));

        var entry = Assert.Single(state.Levels.SelectMany(level => level.Spells));
        Assert.Equal(2, entry.TimesUsed);

        var journal = await context.LoadHistoryAsync(characterId);
        Assert.Equal(2, journal.Count(record => record.Action == SpellbookService.CastHistoryAction));
    }
}
