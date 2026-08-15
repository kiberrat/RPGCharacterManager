using Microsoft.EntityFrameworkCore;
using RPGCharacterManager.Campaigns;
using RPGCharacterManager.Core.Abstractions.Campaigns;
using RPGCharacterManager.Core.Abstractions.Content;

namespace RPGCharacterManager.Tests.Campaigns;

/// <summary>
/// Проверка менеджера кампаний на настоящей базе данных SQLite.
/// </summary>
public sealed class CampaignServiceTests
{
    [Fact]
    public async Task Кампания_СоздаётсяИЧитается()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var saved = await context.Service.SaveAsync(new CampaignDraft
        {
            Name = "Проклятие Страда",
            World = "Баровия",
            StartDate = "15 день месяца Пепла, 1250 год",
            Description = "Туман сомкнулся за спиной.",
            Notes = "Страд знает о них с первого дня.",
        });

        Assert.True(saved.IsSuccess, saved.Error);

        var card = await context.GetCardAsync(saved.Value);

        Assert.Equal("Проклятие Страда", card.Draft.Name);
        Assert.Equal("Баровия", card.Draft.World);
        Assert.Equal("15 день месяца Пепла, 1250 год", card.Draft.StartDate);
        Assert.True(card.Draft.IsActive);
        Assert.Empty(card.Groups);
        Assert.Empty(card.Events);
    }

    [Fact]
    public async Task Кампания_БезНазвания_НеСохраняется()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var result = await context.Service.SaveAsync(new CampaignDraft { Name = "   " });

        Assert.True(result.IsFailure);
        Assert.Contains("название", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task СписокКампаний_ПоказываетРазмерСоставаИХронологии()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();
        var monsterId = await context.CreateObjectAsync(ContentTypeIds.Monsters, "Волколак");

        await context.Service.AddMemberAsync(campaignId, ContentTypeIds.Monsters, monsterId);
        await context.Service.SaveEventAsync(new CampaignEventDraft
        {
            CampaignId = campaignId,
            Title = "Первая ночь в Баровии",
        });

        var result = await context.Service.GetAllAsync();

        Assert.True(result.IsSuccess, result.Error);

        var item = Assert.Single(result.Value);

        Assert.Equal(1, item.MemberCount);
        Assert.Equal(1, item.EventCount);
    }

    [Theory]
    [InlineData(ContentTypeIds.Npcs, "Мадам Ева")]
    [InlineData(ContentTypeIds.Monsters, "Волколак")]
    [InlineData(ContentTypeIds.Quests, "Найти Солнечный меч")]
    [InlineData(ContentTypeIds.Locations, "Замок Равенлофт")]
    [InlineData(ContentTypeIds.Items, "Солнечный меч")]
    public async Task Состав_ПринимаетОбъектыЛюбогоВида(string kindId, string name)
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();
        var objectId = await context.CreateObjectAsync(kindId, name);

        var added = await context.Service.AddMemberAsync(campaignId, kindId, objectId, "враг игроков");

        Assert.True(added.IsSuccess, added.Error);

        var card = await context.GetCardAsync(campaignId);
        var group = Assert.Single(card.Groups);
        var member = Assert.Single(group.Members);

        Assert.Equal(kindId, group.Kind.Id);
        Assert.Equal(name, member.ObjectName);
        Assert.Equal("враг игроков", member.Role);
        Assert.False(member.IsMissing);
    }

    [Fact]
    public async Task Состав_ПринимаетПерсонажаИгрока()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();
        var characterId = await context.CreateCharacterAsync("Люциус Морвейн");

        var added = await context.Service
            .AddMemberAsync(campaignId, CampaignCatalog.CharacterKindId, characterId, "Вася");

        Assert.True(added.IsSuccess, added.Error);

        var card = await context.GetCardAsync(campaignId);
        var group = Assert.Single(card.Groups);
        var member = Assert.Single(group.Members);

        Assert.Equal("Игрок", group.Kind.RoleTitle);
        Assert.Equal("Люциус Морвейн", member.ObjectName);
        Assert.Equal("Вася", member.Role);
    }

    [Fact]
    public async Task Состав_НеПринимаетОдинОбъектДважды()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();
        var npcId = await context.CreateObjectAsync(ContentTypeIds.Npcs, "Мадам Ева");

        await context.Service.AddMemberAsync(campaignId, ContentTypeIds.Npcs, npcId);

        var second = await context.Service.AddMemberAsync(campaignId, ContentTypeIds.Npcs, npcId);

        Assert.True(second.IsFailure);
        Assert.Contains("уже входит", second.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Состав_НеПринимаетНесуществующийОбъект()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();

        var result = await context.Service
            .AddMemberAsync(campaignId, ContentTypeIds.Npcs, Guid.NewGuid());

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Состав_НеПринимаетНеизвестныйВид()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();

        var result = await context.Service.AddMemberAsync(campaignId, "лишний-вид", Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Contains("вид", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Состав_СообщаетОбУдалённомОбъекте()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();
        var npcId = await context.CreateObjectAsync(ContentTypeIds.Npcs, "Мадам Ева");

        await context.Service.AddMemberAsync(campaignId, ContentTypeIds.Npcs, npcId);

        // Состав ссылается на объект без внешнего ключа, поэтому удаление объекта
        // оставляет запись состава: она должна быть показана как потерянная.
        var deleted = await context.Content.DeleteAsync(ContentTypeIds.Npcs, npcId);
        Assert.True(deleted.IsSuccess, deleted.Error);

        var card = await context.GetCardAsync(campaignId);
        var member = Assert.Single(Assert.Single(card.Groups).Members);

        Assert.True(member.IsMissing);
        Assert.Equal("Объект удалён", member.ObjectName);
    }

    [Fact]
    public async Task Участник_СохраняетРольИЗаметки()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();
        var npcId = await context.CreateObjectAsync(ContentTypeIds.Npcs, "Мадам Ева");
        var added = await context.Service.AddMemberAsync(campaignId, ContentTypeIds.Npcs, npcId);

        var updated = await context.Service
            .UpdateMemberAsync(added.Value, "предсказательница", "знает про амулет");

        Assert.True(updated.IsSuccess, updated.Error);

        var card = await context.GetCardAsync(campaignId);
        var member = Assert.Single(Assert.Single(card.Groups).Members);

        Assert.Equal("предсказательница", member.Role);
        Assert.Equal("знает про амулет", member.Notes);
    }

    [Fact]
    public async Task Участник_УбираетсяИзСостава_НеУдаляяОбъект()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();
        var npcId = await context.CreateObjectAsync(ContentTypeIds.Npcs, "Мадам Ева");
        var added = await context.Service.AddMemberAsync(campaignId, ContentTypeIds.Npcs, npcId);

        var removed = await context.Service.RemoveMemberAsync(added.Value);
        Assert.True(removed.IsSuccess, removed.Error);

        var card = await context.GetCardAsync(campaignId);
        Assert.Empty(card.Groups);

        // Сам объект принадлежит не кампании, а приложению.
        var npc = await context.Content.GetAsync(ContentTypeIds.Npcs, npcId);
        Assert.NotNull(npc);
    }

    [Fact]
    public async Task ОдинОбъект_ВходитВНесколькоКампаний()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var first = await context.CreateCampaignAsync("Проклятие Страда");
        var second = await context.CreateCampaignAsync("Восстание Тиамат");
        var locationId = await context.CreateObjectAsync(ContentTypeIds.Locations, "Невервинтер");

        var toFirst = await context.Service.AddMemberAsync(first, ContentTypeIds.Locations, locationId);
        var toSecond = await context.Service.AddMemberAsync(second, ContentTypeIds.Locations, locationId);

        Assert.True(toFirst.IsSuccess, toFirst.Error);
        Assert.True(toSecond.IsSuccess, toSecond.Error);

        Assert.Single(Assert.Single((await context.GetCardAsync(first)).Groups).Members);
        Assert.Single(Assert.Single((await context.GetCardAsync(second)).Groups).Members);
    }

    [Fact]
    public async Task Событие_ДобавляетсяВКонецХронологии()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();

        await AddEventAsync(context, campaignId, "Прибытие в Баровию");
        await AddEventAsync(context, campaignId, "Встреча с Мадам Евой");

        var card = await context.GetCardAsync(campaignId);

        Assert.Equal(
            ["Прибытие в Баровию", "Встреча с Мадам Евой"],
            card.Events.Select(entry => entry.Title));
    }

    [Fact]
    public async Task Событие_БезНазвания_НеСохраняется()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();

        var result = await context.Service.SaveEventAsync(new CampaignEventDraft
        {
            CampaignId = campaignId,
            Title = " ",
        });

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Событие_ПеремещаетсяПоХронологии()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();

        await AddEventAsync(context, campaignId, "Первое");
        var second = await AddEventAsync(context, campaignId, "Второе");

        var moved = await context.Service.MoveEventAsync(second, -1);
        Assert.True(moved.IsSuccess, moved.Error);

        var card = await context.GetCardAsync(campaignId);

        Assert.Equal(["Второе", "Первое"], card.Events.Select(entry => entry.Title));
    }

    [Fact]
    public async Task Событие_НеПеремещаетсяЗаКрайХронологии()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();

        var first = await AddEventAsync(context, campaignId, "Первое");
        await AddEventAsync(context, campaignId, "Второе");

        var moved = await context.Service.MoveEventAsync(first, -1);
        Assert.True(moved.IsSuccess, moved.Error);

        var card = await context.GetCardAsync(campaignId);

        Assert.Equal(["Первое", "Второе"], card.Events.Select(entry => entry.Title));
    }

    [Fact]
    public async Task Событие_СохраняетИгровуюДатуИОписание()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();
        var eventId = await AddEventAsync(context, campaignId, "Падение Беровска");

        var saved = await context.Service.SaveEventAsync(new CampaignEventDraft
        {
            Id = eventId,
            CampaignId = campaignId,
            Title = "Падение Беровска",
            GameDate = "3 день месяца Молота, 1251 год",
            Description = "Город сгорел за одну ночь.",
        });

        Assert.True(saved.IsSuccess, saved.Error);

        var entry = Assert.Single((await context.GetCardAsync(campaignId)).Events);

        Assert.Equal("3 день месяца Молота, 1251 год", entry.GameDate);
        Assert.Equal("Город сгорел за одну ночь.", entry.Description);
    }

    [Fact]
    public async Task Событие_Удаляется()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();
        var eventId = await AddEventAsync(context, campaignId, "Прибытие в Баровию");

        var deleted = await context.Service.DeleteEventAsync(eventId);
        Assert.True(deleted.IsSuccess, deleted.Error);

        Assert.Empty((await context.GetCardAsync(campaignId)).Events);
    }

    [Fact]
    public async Task Кампания_УдаляетсяВместеССоставомИХронологией_НоНеСОбъектами()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var campaignId = await context.CreateCampaignAsync();
        var monsterId = await context.CreateObjectAsync(ContentTypeIds.Monsters, "Волколак");

        await context.Service.AddMemberAsync(campaignId, ContentTypeIds.Monsters, monsterId);
        await AddEventAsync(context, campaignId, "Прибытие в Баровию");

        var deleted = await context.Service.DeleteAsync(campaignId);
        Assert.True(deleted.IsSuccess, deleted.Error);

        var read = await context.Service.GetAsync(campaignId);
        Assert.True(read.IsFailure);

        var monster = await context.Content.GetAsync(ContentTypeIds.Monsters, monsterId);
        Assert.NotNull(monster);
    }

    private static async Task<Guid> AddEventAsync(
        CampaignTestContext context,
        Guid campaignId,
        string title)
    {
        var result = await context.Service.SaveEventAsync(new CampaignEventDraft
        {
            CampaignId = campaignId,
            Title = title,
        });

        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }
}
