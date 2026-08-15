using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Campaigns;
using RPGCharacterManager.Content;
using RPGCharacterManager.Core.Abstractions.Campaigns;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Tests.Support;

namespace RPGCharacterManager.Tests.Campaigns;

/// <summary>
/// Окружение проверок кампаний: настоящая база данных, служба контента и каталог.
/// </summary>
internal sealed class CampaignTestContext : IAsyncDisposable
{
    private readonly TestDatabase _database;

    private CampaignTestContext(TestDatabase database)
    {
        _database = database;

        Content = new ContentService(
            StandardContentTypes.Create(),
            database.ContextFactory,
            NullLogger<ContentService>.Instance);

        Catalog = new CampaignCatalog(Content, database.ContextFactory);

        Service = new CampaignService(
            database.ContextFactory,
            Catalog,
            NullLogger<CampaignService>.Instance);
    }

    /// <summary>Служба контента.</summary>
    public IContentService Content { get; }

    /// <summary>Каталог объектов кампании.</summary>
    public ICampaignCatalog Catalog { get; }

    /// <summary>Менеджер кампаний.</summary>
    public ICampaignService Service { get; }

    /// <summary>
    /// Создаёт окружение проверки.
    /// </summary>
    /// <returns>Готовое окружение.</returns>
    public static async Task<CampaignTestContext> CreateAsync() =>
        new(await TestDatabase.CreateAsync());

    /// <summary>
    /// Создаёт кампанию.
    /// </summary>
    /// <param name="name">Название кампании.</param>
    /// <returns>Идентификатор кампании.</returns>
    public async Task<Guid> CreateCampaignAsync(string name = "Проклятие Страда")
    {
        var result = await Service.SaveAsync(new CampaignDraft { Name = name });

        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    /// <summary>
    /// Создаёт игровой объект указанного вида.
    /// </summary>
    /// <param name="typeId">Идентификатор вида контента.</param>
    /// <param name="name">Название объекта.</param>
    /// <returns>Идентификатор созданного объекта.</returns>
    public async Task<Guid> CreateObjectAsync(string typeId, string name)
    {
        var type = Content.FindType(typeId)!;
        var entity = type.CreateInstance();

        type.SetName(entity, name);

        var result = await Content.SaveAsync(typeId, entity);

        Assert.True(result.IsSuccess, result.Error);

        return entity.Id;
    }

    /// <summary>
    /// Создаёт персонажа напрямую в базе данных.
    /// </summary>
    /// <param name="name">Имя персонажа.</param>
    /// <returns>Идентификатор персонажа.</returns>
    public async Task<Guid> CreateCharacterAsync(string name = "Люциус")
    {
        await using var context = await _database.ContextFactory.CreateDbContextAsync();

        var character = new Character { Name = name };

        context.Characters.Add(character);
        await context.SaveChangesAsync();

        return character.Id;
    }

    /// <summary>
    /// Возвращает карточку кампании.
    /// </summary>
    /// <param name="campaignId">Идентификатор кампании.</param>
    /// <returns>Карточка кампании.</returns>
    public async Task<CampaignCard> GetCardAsync(Guid campaignId)
    {
        var result = await Service.GetAsync(campaignId);

        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _database.DisposeAsync();
}
