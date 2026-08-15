using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Content;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Tests.Support;

namespace RPGCharacterManager.Tests.Content;

/// <summary>
/// Проверка видов контента, описывающих игровой мир: NPC, локаций и квестов.
///
/// Эти виды описаны теми же средствами, что и остальной контент, поэтому редактор
/// и помощник получают их без отдельного кода — проверяется именно это.
/// </summary>
public sealed class WorldContentTests
{
    private static ContentService CreateService(TestDatabase database) => new(
        StandardContentTypes.Create(),
        database.ContextFactory,
        NullLogger<ContentService>.Instance);

    [Fact]
    public void ВидыМира_ЗарегистрированыСоСвоимиПолями()
    {
        var types = StandardContentTypes.Create().ToDictionary(type => type.Id, StringComparer.Ordinal);

        Assert.True(types.ContainsKey(ContentTypeIds.Npcs));
        Assert.True(types.ContainsKey(ContentTypeIds.Locations));
        Assert.True(types.ContainsKey(ContentTypeIds.Quests));

        // Поля-ссылки связывают мир воедино: NPC живёт в локации, квест выдаёт NPC.
        Assert.Contains(
            types[ContentTypeIds.Npcs].Fields,
            field => field.ReferenceTypeId == ContentTypeIds.Locations);

        Assert.Contains(
            types[ContentTypeIds.Quests].Fields,
            field => field.ReferenceTypeId == ContentTypeIds.Npcs);

        Assert.Contains(
            types[ContentTypeIds.Locations].Fields,
            field => field.ReferenceTypeId == ContentTypeIds.Locations);
    }

    [Fact]
    public async Task Локация_ВходитВРодительскуюЛокацию()
    {
        await using var database = await TestDatabase.CreateAsync();

        var service = CreateService(database);

        var city = new Location { Name = "Невервинтер" };
        Assert.True((await service.SaveAsync(ContentTypeIds.Locations, city)).IsSuccess);

        var district = new Location { Name = "Портовый квартал", ParentLocationId = city.Id };
        var saved = await service.SaveAsync(ContentTypeIds.Locations, district);

        Assert.True(saved.IsSuccess, saved.Error);

        var loaded = (Location)(await service.GetAsync(ContentTypeIds.Locations, district.Id))!;

        Assert.Equal(city.Id, loaded.ParentLocationId);
    }

    [Fact]
    public async Task NPC_ЖивётВЛокации()
    {
        await using var database = await TestDatabase.CreateAsync();

        var service = CreateService(database);

        var tavern = new Location { Name = "Таверна «Кровь на закате»" };
        Assert.True((await service.SaveAsync(ContentTypeIds.Locations, tavern)).IsSuccess);

        var npc = new Npc { Name = "Мадам Ева", Role = "предсказательница", LocationId = tavern.Id };
        var saved = await service.SaveAsync(ContentTypeIds.Npcs, npc);

        Assert.True(saved.IsSuccess, saved.Error);

        var loaded = (Npc)(await service.GetAsync(ContentTypeIds.Npcs, npc.Id))!;

        Assert.Equal("предсказательница", loaded.Role);
        Assert.Equal(tavern.Id, loaded.LocationId);

        // Внутреннее имя заполняется само: по нему на объект ссылаются формулы и правила.
        Assert.False(string.IsNullOrWhiteSpace(loaded.SystemName));
    }

    [Fact]
    public async Task Квест_СохраняетЭтапыСОтметкойВыполнения()
    {
        await using var database = await TestDatabase.CreateAsync();

        var service = CreateService(database);
        var type = StandardContentTypes.Create().Single(item => item.Id == ContentTypeIds.Quests);
        var steps = type.Collections.Single(list => list.Name == "steps");

        var quest = new Quest { Name = "Найти Солнечный меч", Status = QuestStatus.Active };

        var first = steps.AddItem(quest);
        SetField(steps, first, "title", "Расспросить Мадам Еву");
        steps.Fields.Single(field => field.Name == "isDone").SetBoolean(first, true);

        var second = steps.AddItem(quest);
        SetField(steps, second, "title", "Спуститься в склеп");

        var saved = await service.SaveAsync(ContentTypeIds.Quests, quest);
        Assert.True(saved.IsSuccess, saved.Error);

        var loaded = (Quest)(await service.GetAsync(ContentTypeIds.Quests, quest.Id))!;

        Assert.Equal(QuestStatus.Active, loaded.Status);
        Assert.Equal(2, loaded.Steps.Count);
        Assert.Contains(loaded.Steps, step => step is { Title: "Расспросить Мадам Еву", IsDone: true });
        Assert.Contains(loaded.Steps, step => step is { Title: "Спуститься в склеп", IsDone: false });
    }

    [Fact]
    public async Task ОбъектыПоИдентификаторам_ВозвращаютТолькоНайденные()
    {
        await using var database = await TestDatabase.CreateAsync();

        var service = CreateService(database);

        var monster = new Monster { Name = "Волколак" };
        Assert.True((await service.SaveAsync(ContentTypeIds.Monsters, monster)).IsSuccess);

        var items = await service.GetItemsAsync(ContentTypeIds.Monsters, [monster.Id, Guid.NewGuid()]);

        var single = Assert.Single(items);
        Assert.Equal("Волколак", single.Name);

        Assert.Empty(await service.GetItemsAsync(ContentTypeIds.Monsters, []));
    }

    private static void SetField(IContentList list, object item, string field, string value)
    {
        var description = list.Fields.Single(entry => entry.Name == field);

        Assert.True(description.TrySetText(item, value, out var error), error);
    }
}
