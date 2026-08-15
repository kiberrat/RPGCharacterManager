using RPGCharacterManager.Core.Abstractions.Dice;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Tests.Characters;

namespace RPGCharacterManager.Tests.Dice;

/// <summary>
/// Проверка бросков: выражения, преимущество и помеха, журнал и любимые броски.
/// </summary>
public sealed class DiceServiceTests
{
    private const int NoHistoryLimit = 0;
    private const int DefaultHistoryLimit = 100;

    private static async Task<Guid> CreateCharacterAsync(CharacterTestContext context)
    {
        var draft = new CharacterDraft { Level = 3 };
        draft.Character.Name = "Странник";

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static async Task<RollOutcome> RollAsync(DiceTestContext context, RollRequest request)
    {
        var result = await context.Service.RollAsync(request);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    // ---------- Кубики ----------

    [Fact]
    public async Task Кубики_БезПользовательских_СодержатСтандартныйНабор()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 1);

        var result = await context.Service.GetDiceAsync();
        Assert.True(result.IsSuccess, result.Error);

        Assert.Equal([2, 3, 4, 6, 8, 10, 12, 20, 100], result.Value.Select(die => die.Sides));
        Assert.All(result.Value, die => Assert.False(die.IsCustom));
    }

    [Fact]
    public async Task Кубики_ПользовательскийКубик_ДобавленКСтандартным()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 1);
        await context.AddDieAsync("Кристалл судьбы", 777);

        var result = await context.Service.GetDiceAsync();
        Assert.True(result.IsSuccess, result.Error);

        var custom = Assert.Single(result.Value, die => die.IsCustom);

        Assert.Equal("Кристалл судьбы", custom.Name);
        Assert.Equal(777, custom.Sides);
    }

    // ---------- Броски ----------

    [Fact]
    public async Task Бросок_НесколькоКубиков_ЗаписываетКаждый()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 4, 5, 6);

        var outcome = await RollAsync(context, new RollRequest("3d6"));

        Assert.Equal(15, outcome.Total);
        Assert.Equal([4, 5, 6], outcome.Dice.Select(cast => cast.Value));
        Assert.All(outcome.Dice, cast => Assert.Equal(6, cast.Sides));
    }

    [Fact]
    public async Task Бросок_МодификаторВФормуле_ДобавленКИтогу()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 7);

        var outcome = await RollAsync(context, new RollRequest("1d20 + 5"));

        Assert.Equal(12, outcome.Total);
        Assert.Equal(7, Assert.Single(outcome.Dice).Value);
    }

    [Fact]
    public async Task Бросок_ФункцияКубик_ТожеЗаписанаКостями()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 3);

        var outcome = await RollAsync(context, new RollRequest("Кубик(2; 8)"));

        Assert.Equal(6, outcome.Total);
        Assert.Equal(2, outcome.Dice.Count);
        Assert.All(outcome.Dice, cast => Assert.Equal(8, cast.Sides));
    }

    [Fact]
    public async Task Бросок_ПустоеВыражение_ВозвращаетОшибку()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 1);

        var result = await context.Service.RollAsync(new RollRequest("   "));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Бросок_НеизвестнаяПеременная_ВозвращаетОшибку()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 1);

        var result = await context.Service.RollAsync(new RollRequest("1d20 + Сила"));

        Assert.True(result.IsFailure);
        Assert.Contains("Сила", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Бросок_ЗначениеПерсонажа_ДоступноФормуле()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 7);

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 14);
        await context.Characters.AddAsync(strength);

        var characterId = await CreateCharacterAsync(context.Characters);

        var outcome = await RollAsync(context, new RollRequest("1d20 + Сила", CharacterId: characterId));

        Assert.Equal(21, outcome.Total);
        Assert.Equal(characterId, outcome.CharacterId);
    }

    // ---------- Преимущество и помеха ----------

    [Fact]
    public async Task Преимущество_ДваБроска_ПринимаетЛучший()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 6, 17);

        var outcome = await RollAsync(context, new RollRequest("1d20", RollMode.Advantage));

        Assert.Equal(17, outcome.Total);
        Assert.Equal(2, outcome.Attempts.Count);
        Assert.Equal([6d, 17d], outcome.Attempts.Select(attempt => attempt.Total));
        Assert.Equal(17, Assert.Single(outcome.Attempts, attempt => attempt.IsChosen).Total);
    }

    [Fact]
    public async Task Помеха_ДваБроска_ПринимаетХудший()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 6, 17);

        var outcome = await RollAsync(context, new RollRequest("1d20", RollMode.Disadvantage));

        Assert.Equal(6, outcome.Total);
        Assert.Equal(6, Assert.Single(outcome.Attempts, attempt => attempt.IsChosen).Total);
    }

    [Fact]
    public async Task Преимущество_КаждаяПопытка_БросаетСвоиКости()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 2, 3, 4, 5);

        var outcome = await RollAsync(context, new RollRequest("2d6", RollMode.Advantage));

        Assert.Equal([2, 3], outcome.Attempts[0].Dice.Select(cast => cast.Value));
        Assert.Equal([4, 5], outcome.Attempts[1].Dice.Select(cast => cast.Value));
        Assert.Equal(9, outcome.Total);
    }

    [Fact]
    public async Task ОбычныйБросок_ВыполняетсяОдинРаз()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 4);

        var outcome = await RollAsync(context, new RollRequest("1d6"));

        Assert.Single(outcome.Attempts);
        Assert.Equal(1, context.Random.Count);
    }

    // ---------- Журнал ----------

    [Fact]
    public async Task Журнал_ПослеБроска_СодержитЗаписьСКостями()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 5, 5);

        await RollAsync(context, new RollRequest("2d6", Title: "Проверка Скрытности"));

        var history = await context.Service.GetHistoryAsync(null, DefaultHistoryLimit);
        Assert.True(history.IsSuccess, history.Error);

        var record = Assert.Single(history.Value);

        Assert.Equal("Проверка Скрытности", record.Title);
        Assert.Equal("2d6", record.Expression);
        Assert.Equal(10, record.Total);
        Assert.Equal([5, 5], record.Dice.Select(cast => cast.Value));
    }

    [Fact]
    public async Task Журнал_ПревышенПредел_УдаляетСтарыеЗаписи()
    {
        const int Limit = 3;

        await using var context = await DiceTestContext.CreateAsync(Limit, 1);

        for (var index = 0; index < Limit + 2; index++)
        {
            await RollAsync(context, new RollRequest("1d6"));
        }

        var history = await context.Service.GetHistoryAsync(null, DefaultHistoryLimit);
        Assert.True(history.IsSuccess, history.Error);

        Assert.Equal(Limit, history.Value.Count);
    }

    [Fact]
    public async Task Журнал_ПределНеЗадан_ХранитВсеЗаписи()
    {
        const int Rolls = 4;

        await using var context = await DiceTestContext.CreateAsync(NoHistoryLimit, 1);

        for (var index = 0; index < Rolls; index++)
        {
            await RollAsync(context, new RollRequest("1d6"));
        }

        var history = await context.Service.GetHistoryAsync(null, DefaultHistoryLimit);
        Assert.True(history.IsSuccess, history.Error);

        Assert.Equal(Rolls, history.Value.Count);
    }

    [Fact]
    public async Task Журнал_Очистка_СохраняетЛюбимыеБроски()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 1);

        var kept = await RollAsync(context, new RollRequest("1d20", Title: "Атака мечом"));
        await RollAsync(context, new RollRequest("1d6"));

        var favorite = await context.Service.SetFavoriteAsync(kept.Id, true);
        Assert.True(favorite.IsSuccess, favorite.Error);

        var removed = await context.Service.ClearHistoryAsync(null);
        Assert.True(removed.IsSuccess, removed.Error);
        Assert.Equal(1, removed.Value);

        var history = await context.Service.GetHistoryAsync(null, DefaultHistoryLimit);
        Assert.True(history.IsSuccess, history.Error);

        var record = Assert.Single(history.Value);
        Assert.Equal("Атака мечом", record.Title);
    }

    [Fact]
    public async Task Журнал_Удаление_УбираетЗапись()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 1);

        var outcome = await RollAsync(context, new RollRequest("1d6"));

        var removed = await context.Service.DeleteAsync(outcome.Id);
        Assert.True(removed.IsSuccess, removed.Error);

        var history = await context.Service.GetHistoryAsync(null, DefaultHistoryLimit);
        Assert.True(history.IsSuccess, history.Error);

        Assert.Empty(history.Value);
    }

    // ---------- Любимые броски ----------

    [Fact]
    public async Task Любимые_ОтмеченныйБросок_СохраняетВыражениеИСпособ()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 9, 12);

        var outcome = await RollAsync(
            context,
            new RollRequest("1d20 + 3", RollMode.Advantage, "Спасбросок мудрости"));

        var favorite = await context.Service.SetFavoriteAsync(outcome.Id, true);
        Assert.True(favorite.IsSuccess, favorite.Error);

        var favorites = await context.Service.GetFavoritesAsync(null);
        Assert.True(favorites.IsSuccess, favorites.Error);

        var saved = Assert.Single(favorites.Value);

        Assert.True(saved.IsFavorite);
        Assert.Equal("Спасбросок мудрости", saved.Title);
        Assert.Equal("1d20 + 3", saved.Expression);
        Assert.Equal(RollMode.Advantage, saved.Mode);
    }

    [Fact]
    public async Task Любимые_СнятиеОтметки_УбираетИзСписка()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 1);

        var outcome = await RollAsync(context, new RollRequest("1d6"));

        await context.Service.SetFavoriteAsync(outcome.Id, true);
        await context.Service.SetFavoriteAsync(outcome.Id, false);

        var favorites = await context.Service.GetFavoritesAsync(null);
        Assert.True(favorites.IsSuccess, favorites.Error);

        Assert.Empty(favorites.Value);
    }

    [Fact]
    public async Task Любимые_НовоеНазвание_ЗаменяетПрежнее()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 1);

        var outcome = await RollAsync(context, new RollRequest("1d6"));

        var favorite = await context.Service.SetFavoriteAsync(outcome.Id, true, "Урон кинжалом");
        Assert.True(favorite.IsSuccess, favorite.Error);

        Assert.Equal("Урон кинжалом", favorite.Value.Title);
    }

    [Fact]
    public async Task Любимые_НесуществующаяЗапись_ВозвращаетОшибку()
    {
        await using var context = await DiceTestContext.CreateAsync(DefaultHistoryLimit, 1);

        var result = await context.Service.SetFavoriteAsync(Guid.NewGuid(), true);

        Assert.True(result.IsFailure);
    }
}
