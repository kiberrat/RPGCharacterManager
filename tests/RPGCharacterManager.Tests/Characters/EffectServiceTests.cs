using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Tests.Characters;

/// <summary>
/// Проверка эффектов: баффов и дебаффов, правил повторного наложения,
/// приоритета, таймеров и истечения длительности.
/// </summary>
public sealed class EffectServiceTests
{
    private static async Task<Guid> CreateCharacterAsync(CharacterTestContext context, int level = 1)
    {
        var draft = new CharacterDraft { Level = level };
        draft.Character.Name = "Странник";

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static async Task<EffectState> LoadAsync(CharacterTestContext context, Guid characterId)
    {
        var result = await context.Effects.GetAsync(characterId);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static async Task ApplyAsync(
        CharacterTestContext context,
        Guid characterId,
        Effect effect,
        string? source = null,
        bool save = true)
    {
        if (save)
        {
            await context.AddAsync(effect);
        }

        var applied = await context.Effects.ApplyAsync(characterId, effect.Id, source);
        Assert.True(applied.IsSuccess, applied.Error);
    }

    private static async Task<double> AttributeValueAsync(CharacterTestContext context, Guid characterId)
    {
        var sheet = await context.Sheets.LoadAsync(characterId);
        Assert.True(sheet.IsSuccess, sheet.Error);

        return Assert.Single(sheet.Value.Attributes).Value;
    }

    // ---------- Баффы и дебаффы ----------

    [Fact]
    public async Task Бафф_ПовышаетХарактеристикуПерсонажа()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        await context.AddAsync(strength);

        var characterId = await CreateCharacterAsync(context);

        Assert.Equal(10, await AttributeValueAsync(context, characterId));

        await ApplyAsync(
            context,
            characterId,
            CharacterContent.Effect(
                "Благословение силы",
                "благословение_силы",
                bonuses: CharacterContent.EffectChange(BonusTargetKind.Attribute, "4", attributeId: strength.Id)));

        Assert.Equal(14, await AttributeValueAsync(context, characterId));
    }

    [Fact]
    public async Task Дебафф_ПонижаетХарактеристикуПерсонажа()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        await context.AddAsync(strength);

        var characterId = await CreateCharacterAsync(context);

        await ApplyAsync(
            context,
            characterId,
            CharacterContent.Effect(
                "Проклятие слабости",
                "проклятие_слабости",
                EffectTone.Negative,
                bonuses: CharacterContent.EffectChange(BonusTargetKind.Attribute, "-3", attributeId: strength.Id)));

        Assert.Equal(7, await AttributeValueAsync(context, characterId));

        var state = await LoadAsync(context, characterId);
        var effect = Assert.Single(state.Effects);

        Assert.Equal(EffectTone.Negative, effect.Tone);
        Assert.Equal(-3, Assert.Single(effect.Changes).Value);
    }

    [Fact]
    public async Task СнятыйЭффект_ВозвращаетПрежнееЗначение()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        await context.AddAsync(strength);

        var characterId = await CreateCharacterAsync(context);

        await ApplyAsync(
            context,
            characterId,
            CharacterContent.Effect(
                "Ярость",
                "ярость",
                bonuses: CharacterContent.EffectChange(BonusTargetKind.Attribute, "5", attributeId: strength.Id)));

        var effect = Assert.Single((await LoadAsync(context, characterId)).Effects);

        var removed = await context.Effects.RemoveAsync(characterId, effect.CharacterEffectId);
        Assert.True(removed.IsSuccess, removed.Error);

        Assert.Equal(10, await AttributeValueAsync(context, characterId));
        Assert.True((await LoadAsync(context, characterId)).IsEmpty);
    }

    [Fact]
    public async Task ЭффектПризнака_ДобавляетПерсонажуПризнак()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await ApplyAsync(
            context,
            characterId,
            CharacterContent.Effect(
                "Невидимость",
                "невидимость",
                EffectTone.Neutral,
                bonuses: CharacterContent.EffectChange(BonusTargetKind.Tag, name: "невидим")));

        var effect = Assert.Single((await LoadAsync(context, characterId)).Effects);
        var change = Assert.Single(effect.Changes);

        Assert.True(change.IsApplied);
        Assert.Contains("невидим", change.Description, StringComparison.CurrentCulture);
    }

    // ---------- Правила повторного наложения ----------

    [Fact]
    public async Task СкладывающийсяЭффект_УмножаетВеличинуНаЧислоНаложений()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        await context.AddAsync(strength);

        var characterId = await CreateCharacterAsync(context);

        var effect = CharacterContent.Effect(
            "Прилив сил",
            "прилив_сил",
            stacking: EffectStacking.Sum,
            bonuses: CharacterContent.EffectChange(BonusTargetKind.Attribute, "2", attributeId: strength.Id));

        await ApplyAsync(context, characterId, effect);
        await ApplyAsync(context, characterId, effect, save: false);
        await ApplyAsync(context, characterId, effect, save: false);

        Assert.Equal(16, await AttributeValueAsync(context, characterId));

        var active = Assert.Single((await LoadAsync(context, characterId)).Effects);

        Assert.Equal(3, active.Stacks);
        Assert.Equal(6, Assert.Single(active.Changes).Value);
    }

    [Fact]
    public async Task ПределНаложений_НеПревышается()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var effect = CharacterContent.Effect(
            "Горение",
            "горение",
            EffectTone.Negative,
            stacking: EffectStacking.Sum,
            maximumStacks: 2);

        await ApplyAsync(context, characterId, effect);
        await ApplyAsync(context, characterId, effect, save: false);

        var blocked = await context.Effects.ApplyAsync(characterId, effect.Id);

        Assert.True(blocked.IsFailure);
        Assert.Contains("предельное", blocked.Error, StringComparison.CurrentCulture);
        Assert.Equal(2, Assert.Single((await LoadAsync(context, characterId)).Effects).Stacks);
    }

    [Fact]
    public async Task ЗапрещённоеНаложение_ОтклоняетПовторноеПрименение()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var effect = CharacterContent.Effect(
            "Древнее проклятие",
            "древнее_проклятие",
            EffectTone.Negative,
            stacking: EffectStacking.Forbidden);

        await ApplyAsync(context, characterId, effect);

        var blocked = await context.Effects.ApplyAsync(characterId, effect.Id);

        Assert.True(blocked.IsFailure);
        Assert.Contains("не складывается", blocked.Error, StringComparison.CurrentCulture);
    }

    [Fact]
    public async Task ОбновляющийсяЭффект_ВозобновляетДлительность()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var effect = CharacterContent.Effect(
            "Ускорение",
            "ускорение",
            durationFormula: "10",
            durationUnit: "раунд");

        await ApplyAsync(context, characterId, effect);

        var advanced = await context.Effects.AdvanceAsync(characterId, "раунд", 6);
        Assert.True(advanced.IsSuccess, advanced.Error);
        Assert.Equal(4, Assert.Single((await LoadAsync(context, characterId)).Effects).Remaining);

        await ApplyAsync(context, characterId, effect, save: false);

        Assert.Equal(10, Assert.Single((await LoadAsync(context, characterId)).Effects).Remaining);
        Assert.Equal(1, Assert.Single((await LoadAsync(context, characterId)).Effects).Stacks);
    }

    [Fact]
    public async Task УбранноеНаложение_УменьшаетСчётчикИСнимаетПоследнее()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var effect = CharacterContent.Effect("Заряд", "заряд", stacking: EffectStacking.Sum);

        await ApplyAsync(context, characterId, effect);
        await ApplyAsync(context, characterId, effect, save: false);

        var active = Assert.Single((await LoadAsync(context, characterId)).Effects);
        Assert.Equal(2, active.Stacks);

        Assert.True((await context.Effects.RemoveStackAsync(characterId, active.CharacterEffectId)).IsSuccess);
        Assert.Equal(1, Assert.Single((await LoadAsync(context, characterId)).Effects).Stacks);

        Assert.True((await context.Effects.RemoveStackAsync(characterId, active.CharacterEffectId)).IsSuccess);
        Assert.True((await LoadAsync(context, characterId)).IsEmpty);
    }

    // ---------- Таймеры ----------

    [Fact]
    public async Task Длительность_ВычисляетсяФормулойПоПараметрамПерсонажа()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var intellect = CharacterContent.Attribute("Интеллект", "интеллект", defaultValue: 6);
        await context.AddAsync(intellect);

        var characterId = await CreateCharacterAsync(context);

        await ApplyAsync(
            context,
            characterId,
            CharacterContent.Effect(
                "Щит разума",
                "щит_разума",
                durationFormula: "интеллект * 2",
                durationUnit: "минута"));

        var effect = Assert.Single((await LoadAsync(context, characterId)).Effects);

        Assert.Equal(12, effect.Remaining);
        Assert.Equal("минута", effect.DurationUnit);
        Assert.True(effect.HasTimer);
    }

    [Fact]
    public async Task ИстёкшийТаймер_СнимаетЭффектИВозвращаетХарактеристику()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        await context.AddAsync(strength);

        var characterId = await CreateCharacterAsync(context);

        await ApplyAsync(
            context,
            characterId,
            CharacterContent.Effect(
                "Зелье силы",
                "зелье_силы",
                durationFormula: "3",
                durationUnit: "раунд",
                bonuses: CharacterContent.EffectChange(BonusTargetKind.Attribute, "4", attributeId: strength.Id)));

        Assert.Equal(14, await AttributeValueAsync(context, characterId));

        var advanced = await context.Effects.AdvanceAsync(characterId, "раунд", 3);

        Assert.True(advanced.IsSuccess, advanced.Error);
        Assert.Equal("Зелье силы", Assert.Single(advanced.Value.Expired));
        Assert.True((await LoadAsync(context, characterId)).IsEmpty);
        Assert.Equal(10, await AttributeValueAsync(context, characterId));
    }

    [Fact]
    public async Task ПродвижениеВремени_ТрогаетТолькоСвоюЕдиницу()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await ApplyAsync(
            context,
            characterId,
            CharacterContent.Effect("Ускорение", "ускорение", durationFormula: "10", durationUnit: "раунд"));

        await ApplyAsync(
            context,
            characterId,
            CharacterContent.Effect("Полёт", "полёт", durationFormula: "10", durationUnit: "минута"));

        var advanced = await context.Effects.AdvanceAsync(characterId, "раунд", 4);
        Assert.True(advanced.IsSuccess, advanced.Error);
        Assert.Empty(advanced.Value.Expired);

        var effects = (await LoadAsync(context, characterId)).Effects;

        Assert.Equal(6, effects.Single(effect => effect.Name == "Ускорение").Remaining);
        Assert.Equal(10, effects.Single(effect => effect.Name == "Полёт").Remaining);
    }

    [Fact]
    public async Task ЭффектБезСрока_НеУбываетСоВременем()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await ApplyAsync(
            context,
            characterId,
            CharacterContent.Effect("Тёмное зрение", "тёмное_зрение", EffectTone.Neutral));

        Assert.True((await context.Effects.AdvanceAsync(characterId, "раунд", 100)).IsSuccess);

        var effect = Assert.Single((await LoadAsync(context, characterId)).Effects);

        Assert.Null(effect.Remaining);
        Assert.False(effect.HasTimer);
    }

    [Fact]
    public async Task ЕдиницыДлительности_СобираютсяИзДействующихЭффектов()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await ApplyAsync(
            context,
            characterId,
            CharacterContent.Effect("Ускорение", "ускорение", durationFormula: "10", durationUnit: "раунд"));

        await ApplyAsync(
            context,
            characterId,
            CharacterContent.Effect("Щит", "щит", durationFormula: "5", durationUnit: "раунд"));

        await ApplyAsync(
            context,
            characterId,
            CharacterContent.Effect("Полёт", "полёт", durationFormula: "10", durationUnit: "минута"));

        var state = await LoadAsync(context, characterId);

        Assert.Equal(2, state.Units.Count);
        Assert.Equal(2, state.Units.Single(unit => unit.Unit == "раунд").Count);
        Assert.Equal(1, state.Units.Single(unit => unit.Unit == "минута").Count);
    }

    // ---------- Приоритет ----------

    [Fact]
    public async Task Приоритет_ОпределяетПорядокПоказаЭффектов()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await ApplyAsync(context, characterId, CharacterContent.Effect("Зелье", "зелье", priority: 20));
        await ApplyAsync(context, characterId, CharacterContent.Effect("Артефакт", "артефакт", priority: 100));
        await ApplyAsync(context, characterId, CharacterContent.Effect("Заклинание", "заклинание", priority: 50));

        var state = await LoadAsync(context, characterId);

        Assert.Equal(
            ["Артефакт", "Заклинание", "Зелье"],
            state.Effects.Select(effect => effect.Name));
    }

    // ---------- Источник ----------

    [Fact]
    public async Task ИсточникНаложения_СохраняетсяИПоказывается()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await ApplyAsync(
            context,
            characterId,
            CharacterContent.Effect("Благословение", "благословение"),
            "Заклинание жреца");

        var effect = Assert.Single((await LoadAsync(context, characterId)).Effects);

        Assert.Equal("Заклинание жреца", effect.Source);
    }
}
