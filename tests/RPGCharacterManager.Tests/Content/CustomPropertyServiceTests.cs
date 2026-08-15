using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Content;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Tests.Support;

namespace RPGCharacterManager.Tests.Content;

/// <summary>
/// Проверка пользовательских свойств: добавление собственного поля любому виду
/// контента без изменения структуры базы данных.
/// </summary>
public sealed class CustomPropertyServiceTests
{
    private static CustomPropertyService CreateService(TestDatabase database) =>
        new(database.ContextFactory, NullLogger<CustomPropertyService>.Instance);

    private static PropertyDefinition CreateDefinition(string targetType, string displayName) => new()
    {
        DisplayName = displayName,
        TargetType = targetType,
        DataType = GameValueType.WholeNumber,
    };

    [Fact]
    public async Task Свойство_СоздаётсяИЧитаетсяДляВидаКонтента()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var saved = await service.SaveDefinitionAsync(
            CreateDefinition(ContentTypeIds.Races, "Устойчивость к холоду"));

        Assert.True(saved.IsSuccess, saved.Error);

        var definitions = await service.GetDefinitionsAsync(ContentTypeIds.Races);
        var definition = Assert.Single(definitions);

        Assert.Equal("Устойчивость к холоду", definition.DisplayName);
        Assert.Equal("устойчивость_к_холоду", definition.SystemName);
    }

    [Fact]
    public async Task Свойства_РазделяютсяПоВидамКонтента()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        await service.SaveDefinitionAsync(CreateDefinition(ContentTypeIds.Races, "Ночное зрение"));
        await service.SaveDefinitionAsync(CreateDefinition(ContentTypeIds.Spells, "Школа заклинателя"));

        Assert.Single(await service.GetDefinitionsAsync(ContentTypeIds.Races));
        Assert.Single(await service.GetDefinitionsAsync(ContentTypeIds.Spells));
        Assert.Empty(await service.GetDefinitionsAsync(ContentTypeIds.Items));
    }

    [Fact]
    public async Task Значение_СохраняетсяДляКонкретногоОбъекта()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var definition = CreateDefinition(ContentTypeIds.Races, "Удача");
        await service.SaveDefinitionAsync(definition);

        var objectId = Guid.NewGuid();
        await service.SaveValuesAsync(objectId, new Dictionary<Guid, string?> { [definition.Id] = "7" });

        var values = await service.GetValuesAsync(objectId);

        Assert.Equal("7", values[definition.Id]);
    }

    [Fact]
    public async Task ПустоеЗначение_УдаляетЗаписьСвойства()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var definition = CreateDefinition(ContentTypeIds.Races, "Удача");
        await service.SaveDefinitionAsync(definition);

        var objectId = Guid.NewGuid();
        await service.SaveValuesAsync(objectId, new Dictionary<Guid, string?> { [definition.Id] = "7" });
        await service.SaveValuesAsync(objectId, new Dictionary<Guid, string?> { [definition.Id] = null });

        Assert.Empty(await service.GetValuesAsync(objectId));
    }

    [Fact]
    public async Task ПовторноеСохранение_ОбновляетЗначение()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var definition = CreateDefinition(ContentTypeIds.Races, "Удача");
        await service.SaveDefinitionAsync(definition);

        var objectId = Guid.NewGuid();
        await service.SaveValuesAsync(objectId, new Dictionary<Guid, string?> { [definition.Id] = "3" });
        await service.SaveValuesAsync(objectId, new Dictionary<Guid, string?> { [definition.Id] = "9" });

        var values = await service.GetValuesAsync(objectId);

        Assert.Equal("9", Assert.Single(values).Value);
    }

    [Fact]
    public async Task УдалениеСвойства_УдаляетЕгоЗначения()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var definition = CreateDefinition(ContentTypeIds.Races, "Удача");
        await service.SaveDefinitionAsync(definition);

        var objectId = Guid.NewGuid();
        await service.SaveValuesAsync(objectId, new Dictionary<Guid, string?> { [definition.Id] = "7" });

        Assert.True(await service.DeleteDefinitionAsync(definition.Id));

        // Значения удаляются каскадно, сами игровые объекты при этом не затрагиваются.
        Assert.Empty(await service.GetValuesAsync(objectId));
        Assert.Empty(await service.GetDefinitionsAsync(ContentTypeIds.Races));
    }

    [Fact]
    public async Task СвойствоБезНазвания_НеСохраняется()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var result = await service.SaveDefinitionAsync(new PropertyDefinition
        {
            TargetType = ContentTypeIds.Races,
        });

        Assert.True(result.IsFailure);
    }
}
