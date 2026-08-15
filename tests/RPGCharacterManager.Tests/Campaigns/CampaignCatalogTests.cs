using RPGCharacterManager.Campaigns;
using RPGCharacterManager.Core.Abstractions.Content;

namespace RPGCharacterManager.Tests.Campaigns;

/// <summary>
/// Проверка каталога объектов, доступных кампании.
/// </summary>
public sealed class CampaignCatalogTests
{
    [Fact]
    public async Task Каталог_СодержитПерсонажейИСоставЭтапа()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var kinds = context.Catalog.Kinds.Select(kind => kind.Id).ToList();

        // Состав задан ROADMAP: игроки, NPC, монстры, квесты, локации.
        Assert.Contains(CampaignCatalog.CharacterKindId, kinds);
        Assert.Contains(ContentTypeIds.Npcs, kinds);
        Assert.Contains(ContentTypeIds.Monsters, kinds);
        Assert.Contains(ContentTypeIds.Quests, kinds);
        Assert.Contains(ContentTypeIds.Locations, kinds);
    }

    [Fact]
    public async Task Каталог_НачинаетсяСВидов_ИзКоторыхСостоитКампания()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var first = context.Catalog.Kinds.Take(5).Select(kind => kind.Id).ToList();

        Assert.Equal(
            [
                CampaignCatalog.CharacterKindId,
                ContentTypeIds.Npcs,
                ContentTypeIds.Monsters,
                ContentTypeIds.Quests,
                ContentTypeIds.Locations,
            ],
            first);
    }

    [Fact]
    public async Task Каталог_ПринимаетЛюбойВидКонтента()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        // Документ 026 включает в состав кампании и предметы, и правила, поэтому
        // каталог не ограничивается перечисленными в ROADMAP видами.
        foreach (var type in context.Content.Types)
        {
            Assert.NotNull(context.Catalog.FindKind(type.Id));
        }
    }

    [Fact]
    public async Task Поиск_ОтбираетОбъектыПоНазванию()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        await context.CreateObjectAsync(ContentTypeIds.Monsters, "Волколак");
        await context.CreateObjectAsync(ContentTypeIds.Monsters, "Скелет");

        // Регистр не различается и у кириллицы: приложение заменяет встроенный
        // LIKE своей функцией (решение Р-95).
        var found = await context.Catalog.SearchAsync(ContentTypeIds.Monsters, "волк", 50);

        var single = Assert.Single(found);
        Assert.Equal("Волколак", single.Name);
    }

    [Fact]
    public async Task Поиск_НаходитПерсонажей()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        await context.CreateCharacterAsync("Люциус Морвейн");

        var found = await context.Catalog.SearchAsync(CampaignCatalog.CharacterKindId, null, 50);

        Assert.Equal("Люциус Морвейн", Assert.Single(found).Name);
    }

    [Fact]
    public async Task Названия_НеСодержатУдалённыхОбъектов()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        var monsterId = await context.CreateObjectAsync(ContentTypeIds.Monsters, "Волколак");
        var missingId = Guid.NewGuid();

        var names = await context.Catalog
            .GetNamesAsync(ContentTypeIds.Monsters, [monsterId, missingId]);

        Assert.Equal("Волколак", names[monsterId]);
        Assert.False(names.ContainsKey(missingId));
    }

    [Fact]
    public async Task НеизвестныйВид_НеДаётОбъектов()
    {
        await using var context = await CampaignTestContext.CreateAsync();

        Assert.Empty(await context.Catalog.SearchAsync("лишний-вид", null, 50));
        Assert.Empty(await context.Catalog.GetNamesAsync("лишний-вид", [Guid.NewGuid()]));
    }
}
