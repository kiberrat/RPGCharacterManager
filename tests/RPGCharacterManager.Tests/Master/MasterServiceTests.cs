using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.History;

namespace RPGCharacterManager.Tests.Master;

/// <summary>
/// Проверка режима мастера: сводка, массовые изменения и очередь хода.
/// </summary>
public sealed class MasterServiceTests
{
    [Fact]
    public async Task Сводка_ПоказываетПерсонажейСРесурсамиИЭффектами()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var argus = await context.CreateCharacterAsync("Аргус", level: 3);
        var resourceId = await context.CreateResourceAsync("Хиты", 30, 24, argus);
        var effectId = await context.CreateEffectAsync("Благословение");

        Assert.True((await context.Service.ApplyEffectAsync([argus], effectId)).IsSuccess);

        var board = await context.GetBoardAsync();
        var row = Assert.Single(board.Characters);

        Assert.Equal("Аргус", row.Name);
        Assert.Equal(3, row.Level);

        var resource = Assert.Single(row.Resources);
        Assert.Equal(resourceId, resource.ResourceId);
        Assert.Equal(24, resource.Current);
        Assert.Equal(30, resource.Maximum);

        Assert.Equal("Благословение", Assert.Single(row.Effects).Name);
    }

    [Fact]
    public async Task Сводка_НеПоказываетЗаготовкиПерсонажей()
    {
        await using var context = await MasterTestContext.CreateAsync();

        await context.CreateCharacterAsync("Аргус");
        await context.CreateCharacterAsync("Образец", isTemplate: true);

        var board = await context.GetBoardAsync();

        // Заготовка — образец для создания новых персонажей, а не участник игры.
        Assert.Equal("Аргус", Assert.Single(board.Characters).Name);
    }

    [Fact]
    public async Task Сводка_ОтбираетПерсонажейПоКампании()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var argus = await context.CreateCharacterAsync("Аргус");
        await context.CreateCharacterAsync("Посторонний");

        var campaignId = await context.CreateCampaignAsync("Проклятие Страда", argus);

        var board = await context.GetBoardAsync(campaignId);
        var row = Assert.Single(board.Characters);

        Assert.Equal("Аргус", row.Name);

        // Роль участника кампании показывается как имя игрока.
        Assert.Equal("Игрок", row.Player);

        Assert.Equal(2, (await context.GetBoardAsync()).Characters.Count);
    }

    [Fact]
    public async Task МассовоеИзменение_ОтнимаетРесурсУВсехОтмеченных()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var argus = await context.CreateCharacterAsync("Аргус");
        var luca = await context.CreateCharacterAsync("Люциус");
        var resourceId = await context.CreateResourceAsync("Хиты", 30, 24, argus, luca);

        var result = await context.Service.ChangeResourceAsync([argus, luca], resourceId, -7);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Value.Changed);
        Assert.True(result.Value.IsComplete);

        Assert.Equal(17, await context.GetResourceAsync(argus, resourceId));
        Assert.Equal(17, await context.GetResourceAsync(luca, resourceId));
    }

    [Fact]
    public async Task МассовоеИзменение_НеОпускаетРесурсНижеНуля()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var argus = await context.CreateCharacterAsync("Аргус");
        var resourceId = await context.CreateResourceAsync("Хиты", 30, 5, argus);

        Assert.True((await context.Service.ChangeResourceAsync([argus], resourceId, -100)).IsSuccess);

        Assert.Equal(0, await context.GetResourceAsync(argus, resourceId));
    }

    [Fact]
    public async Task МассовоеИзменение_НеПоднимаетРесурсВышеМаксимума()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var argus = await context.CreateCharacterAsync("Аргус");
        var resourceId = await context.CreateResourceAsync("Хиты", 30, 24, argus);

        Assert.True((await context.Service.ChangeResourceAsync([argus], resourceId, 100)).IsSuccess);

        Assert.Equal(30, await context.GetResourceAsync(argus, resourceId));
    }

    [Fact]
    public async Task МассовоеИзменение_ПопадаетВОбщийЖурнал()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var argus = await context.CreateCharacterAsync("Аргус");
        var resourceId = await context.CreateResourceAsync("Хиты", 30, 24, argus);

        Assert.True((await context.Service.ChangeResourceAsync([argus], resourceId, -7)).IsSuccess);

        var entry = Assert.Single(await context.GetHistoryAsync(argus));

        Assert.Equal(HistoryActions.ResourceChanged, entry.Action);
        Assert.Equal("24", entry.OldValue);
        Assert.Equal("17", entry.NewValue);
    }

    [Fact]
    public async Task МассовоеИзменение_СообщаетОПерсонажеБезРесурса()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var argus = await context.CreateCharacterAsync("Аргус");
        var luca = await context.CreateCharacterAsync("Люциус");
        var resourceId = await context.CreateResourceAsync("Хиты", 30, 24, argus);

        var result = await context.Service.ChangeResourceAsync([argus, luca], resourceId, -7);

        Assert.True(result.IsSuccess, result.Error);

        // Отказ по одному персонажу не отменяет изменения у остальных.
        Assert.Equal(1, result.Value.Changed);
        Assert.Contains("Люциус", Assert.Single(result.Value.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public async Task МассовоеИзменение_ТребуетХотяБыОдногоПерсонажа()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var resourceId = await context.CreateResourceAsync("Хиты", 30, 24);

        Assert.True((await context.Service.ChangeResourceAsync([], resourceId, -7)).IsFailure);
    }

    [Fact]
    public async Task МассовоеНаложение_НакладываетЭффектНаВсехОтмеченных()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var argus = await context.CreateCharacterAsync("Аргус");
        var luca = await context.CreateCharacterAsync("Люциус");
        var effectId = await context.CreateEffectAsync("Отравление", EffectTone.Negative);

        var result = await context.Service.ApplyEffectAsync([argus, luca], effectId);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Value.Changed);

        var board = await context.GetBoardAsync();

        Assert.All(board.Characters, row => Assert.Equal("Отравление", Assert.Single(row.Effects).Name));
    }

    [Fact]
    public async Task МассовоеСнятие_УбираетЭффектИСообщаетОНеналоженном()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var argus = await context.CreateCharacterAsync("Аргус");
        var luca = await context.CreateCharacterAsync("Люциус");
        var effectId = await context.CreateEffectAsync("Отравление", EffectTone.Negative);

        Assert.True((await context.Service.ApplyEffectAsync([argus], effectId)).IsSuccess);

        var result = await context.Service.RemoveEffectAsync([argus, luca], effectId);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, result.Value.Changed);
        Assert.Contains("Люциус", Assert.Single(result.Value.Failures), StringComparison.Ordinal);

        var board = await context.GetBoardAsync();

        Assert.All(board.Characters, row => Assert.Empty(row.Effects));
    }

    [Fact]
    public async Task Инициатива_НедоступнаБезФормулыИгровойСистемы()
    {
        await using var context = await MasterTestContext.CreateAsync();

        await context.CreateCharacterAsync("Аргус");

        var board = await context.GetBoardAsync();

        // Порядок хода есть не во всякой игре, поэтому без формулы его нет и здесь.
        Assert.False(board.Initiative.IsEnabled);
        Assert.False(string.IsNullOrWhiteSpace(board.Initiative.DisabledReason));
    }

    [Fact]
    public async Task Инициатива_НеБросаетсяБезФормулыИгровойСистемы()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var argus = await context.CreateCharacterAsync("Аргус");

        var result = await context.Service.RollInitiativeAsync(null, [argus]);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Инициатива_БросаетсяПоФормулеИгровойСистемы()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var systemId = await context.CreateGameSystemAsync("1к20 + Уровень");
        var argus = await context.CreateCharacterAsync("Аргус", level: 3, gameSystemId: systemId);

        var result = await context.Service.RollInitiativeAsync(null, [argus]);

        Assert.True(result.IsSuccess, result.Error);

        var board = await context.GetBoardAsync();
        var row = Assert.Single(board.Characters);

        // Кубик проверок всегда даёт 10, поэтому 10 + 3 уровня = 13.
        Assert.Equal(13, row.Initiative);
        Assert.True(row.IsCurrentTurn);
        Assert.True(board.Initiative.IsStarted);
        Assert.Equal(1, board.Initiative.Round);
    }

    [Fact]
    public async Task Инициатива_БольшийРезультатХодитРаньше()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var systemId = await context.CreateGameSystemAsync("1к20 + Уровень");
        var argus = await context.CreateCharacterAsync("Аргус", level: 2, gameSystemId: systemId);
        var luca = await context.CreateCharacterAsync("Люциус", level: 7, gameSystemId: systemId);

        Assert.True((await context.Service.RollInitiativeAsync(null, [argus, luca])).IsSuccess);

        var board = await context.GetBoardAsync();

        Assert.Equal("Люциус", board.Characters[0].Name);
        Assert.Equal("Аргус", board.Characters[1].Name);
        Assert.True(board.Characters[0].IsCurrentTurn);
    }

    [Fact]
    public async Task СледующийХод_ПередаётХодИНачинаетНовыйРаунд()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var systemId = await context.CreateGameSystemAsync("1к20 + Уровень");
        var argus = await context.CreateCharacterAsync("Аргус", level: 2, gameSystemId: systemId);
        var luca = await context.CreateCharacterAsync("Люциус", level: 7, gameSystemId: systemId);

        Assert.True((await context.Service.RollInitiativeAsync(null, [argus, luca])).IsSuccess);
        Assert.True((await context.Service.NextTurnAsync(null)).IsSuccess);

        var second = await context.GetBoardAsync();

        Assert.Equal("Аргус", second.Characters.Single(row => row.IsCurrentTurn).Name);
        Assert.Equal(1, second.Initiative.Round);

        // Круг замкнулся — начинается следующий раунд.
        Assert.True((await context.Service.NextTurnAsync(null)).IsSuccess);

        var third = await context.GetBoardAsync();

        Assert.Equal("Люциус", third.Characters.Single(row => row.IsCurrentTurn).Name);
        Assert.Equal(2, third.Initiative.Round);
    }

    [Fact]
    public async Task Инициатива_ЗадаётсяВручнуюИПереставляетУчастника()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var systemId = await context.CreateGameSystemAsync("1к20 + Уровень");
        var argus = await context.CreateCharacterAsync("Аргус", level: 2, gameSystemId: systemId);
        var luca = await context.CreateCharacterAsync("Люциус", level: 7, gameSystemId: systemId);

        Assert.True((await context.Service.RollInitiativeAsync(null, [argus, luca])).IsSuccess);
        Assert.True((await context.Service.SetInitiativeAsync(null, argus, 25)).IsSuccess);

        var board = await context.GetBoardAsync();

        Assert.Equal("Аргус", board.Characters[0].Name);
        Assert.Equal(25, board.Characters[0].Initiative);
    }

    [Fact]
    public async Task ЗавершениеБоя_ОчищаетОчередьХода()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var systemId = await context.CreateGameSystemAsync("1к20 + Уровень");
        var argus = await context.CreateCharacterAsync("Аргус", level: 2, gameSystemId: systemId);

        Assert.True((await context.Service.RollInitiativeAsync(null, [argus])).IsSuccess);
        Assert.True((await context.Service.NextTurnAsync(null)).IsSuccess);
        Assert.True((await context.Service.EndCombatAsync(null)).IsSuccess);

        var board = await context.GetBoardAsync();

        Assert.False(board.Initiative.IsStarted);
        Assert.Equal(1, board.Initiative.Round);
        Assert.Null(Assert.Single(board.Characters).Initiative);
    }

    [Fact]
    public async Task ОчередьХода_ПринадлежитКампанииИНеМешаетДругой()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var systemId = await context.CreateGameSystemAsync("1к20 + Уровень");
        var argus = await context.CreateCharacterAsync("Аргус", level: 2, gameSystemId: systemId);
        var first = await context.CreateCampaignAsync("Первая игра", argus);
        var second = await context.CreateCampaignAsync("Вторая игра", argus);

        Assert.True((await context.Service.RollInitiativeAsync(first, [argus])).IsSuccess);

        // Один и тот же персонаж играет в двух кампаниях: бой в одной
        // не начинает бой в другой.
        Assert.True((await context.GetBoardAsync(first)).Initiative.IsStarted);
        Assert.False((await context.GetBoardAsync(second)).Initiative.IsStarted);
    }

    [Fact]
    public async Task Ресурсы_СводкиСобираютсяИзПоказанныхПерсонажей()
    {
        await using var context = await MasterTestContext.CreateAsync();

        var argus = await context.CreateCharacterAsync("Аргус");
        await context.CreateResourceAsync("Хиты", 30, 24, argus);
        await context.CreateResourceAsync("Мана", 12, 12, argus);

        var board = await context.GetBoardAsync();

        Assert.Equal(2, board.Resources.Count);
        Assert.Contains(board.Resources, option => option.Name == "Мана");
    }
}
