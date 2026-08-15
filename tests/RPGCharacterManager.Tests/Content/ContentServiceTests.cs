using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Content;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Tests.Support;

namespace RPGCharacterManager.Tests.Content;

/// <summary>
/// Проверка менеджера контента на настоящей базе данных SQLite.
/// </summary>
public sealed class ContentServiceTests
{
    private static ContentService CreateService(TestDatabase database) => new(
        StandardContentTypes.Create(),
        database.ContextFactory,
        NullLogger<ContentService>.Instance);

    private static EntityBase CreateEntity(IContentTypeDescriptor type, string name)
    {
        var entity = type.CreateInstance();
        type.SetName(entity, name);
        return entity;
    }

    [Fact]
    public void ВидыКонтента_ПокрываютСоставЭтапа()
    {
        var types = StandardContentTypes.Create().Select(type => type.Id).ToList();

        // Состав задан ROADMAP: расы, классы, подклассы, заклинания, черты,
        // предметы, оружие, эффекты, ресурсы, монстры, игровые системы.
        Assert.Contains(ContentTypeIds.Races, types);
        Assert.Contains(ContentTypeIds.Classes, types);
        Assert.Contains(ContentTypeIds.Subclasses, types);
        Assert.Contains(ContentTypeIds.Spells, types);
        Assert.Contains(ContentTypeIds.Traits, types);
        Assert.Contains(ContentTypeIds.Items, types);
        Assert.Contains(ContentTypeIds.Weapons, types);
        Assert.Contains(ContentTypeIds.Effects, types);
        Assert.Contains(ContentTypeIds.Resources, types);
        Assert.Contains(ContentTypeIds.Monsters, types);
        Assert.Contains(ContentTypeIds.GameSystems, types);
    }

    [Fact]
    public void КаждыйВид_ИмеетПоляИНазвание()
    {
        foreach (var type in StandardContentTypes.Create())
        {
            Assert.NotEmpty(type.Fields);
            Assert.False(string.IsNullOrWhiteSpace(type.DisplayName), type.Id);
            Assert.False(string.IsNullOrWhiteSpace(type.SingularName), type.Id);

            // Название объекта должно читаться и записываться: на нём строится список.
            var entity = type.CreateInstance();
            type.SetName(entity, "Проверка");
            Assert.Equal("Проверка", type.GetName(entity));
        }
    }

    [Theory]
    [InlineData(ContentTypeIds.Races, "Эльф")]
    [InlineData(ContentTypeIds.Classes, "Воин")]
    [InlineData(ContentTypeIds.Spells, "Огненный шар")]
    [InlineData(ContentTypeIds.Traits, "Меткий стрелок")]
    [InlineData(ContentTypeIds.Effects, "Отравление")]
    [InlineData(ContentTypeIds.Resources, "Мана")]
    [InlineData(ContentTypeIds.Items, "Верёвка")]
    [InlineData(ContentTypeIds.Monsters, "Гоблин")]
    [InlineData(ContentTypeIds.Attributes, "Сила")]
    [InlineData(ContentTypeIds.Skills, "Скрытность")]
    public async Task Объект_СоздаётсяИЧитаетсяДляЛюбогоВида(string typeId, string name)
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var type = service.FindType(typeId)!;

        var saved = await service.SaveAsync(typeId, CreateEntity(type, name));
        Assert.True(saved.IsSuccess, saved.Error);

        var page = await service.SearchAsync(typeId, null, 0, 50);
        var item = Assert.Single(page.Items);

        Assert.Equal(name, item.Name);
    }

    [Fact]
    public async Task ВнутреннееИмя_ЗаполняетсяАвтоматически()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var type = service.FindType(ContentTypeIds.Races)!;

        var entity = CreateEntity(type, "Горный дварф");
        await service.SaveAsync(ContentTypeIds.Races, entity);

        var loaded = (Race)(await service.GetAsync(ContentTypeIds.Races, entity.Id))!;

        Assert.Equal("горный_дварф", loaded.SystemName);
    }

    [Fact]
    public async Task Поиск_ОтбираетОбъектыПоНазванию()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var type = service.FindType(ContentTypeIds.Spells)!;

        await service.SaveAsync(ContentTypeIds.Spells, CreateEntity(type, "Огненный шар"));
        await service.SaveAsync(ContentTypeIds.Spells, CreateEntity(type, "Ледяная стрела"));
        await service.SaveAsync(ContentTypeIds.Spells, CreateEntity(type, "Огненная стена"));

        var found = await service.SearchAsync(ContentTypeIds.Spells, "Огненн", 0, 50);

        Assert.Equal(2, found.TotalCount);
    }

    [Fact]
    public async Task ПостраничнаяВыборка_ОграничиваетРазмерСтраницы()
    {
        const int TotalItems = 25;
        const int PageSize = 10;

        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var type = service.FindType(ContentTypeIds.Items)!;

        for (var index = 0; index < TotalItems; index++)
        {
            await service.SaveAsync(ContentTypeIds.Items, CreateEntity(type, $"Предмет {index:D2}"));
        }

        var page = await service.SearchAsync(ContentTypeIds.Items, null, 1, PageSize);

        Assert.Equal(TotalItems, page.TotalCount);
        Assert.Equal(PageSize, page.Items.Count);
    }

    [Fact]
    public async Task ОбъектБезНазвания_НеСохраняется()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var type = service.FindType(ContentTypeIds.Races)!;

        var result = await service.SaveAsync(ContentTypeIds.Races, CreateEntity(type, string.Empty));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task СистемныйОбъект_НеИзменяется()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var type = service.FindType(ContentTypeIds.Races)!;

        var entity = (Race)CreateEntity(type, "Системная раса");
        entity.IsSystem = true;

        var result = await service.SaveAsync(ContentTypeIds.Races, entity);

        Assert.True(result.IsFailure);
        Assert.Contains("копию", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Копия_СоздаётПользовательскийОбъектИзСистемного()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var type = service.FindType(ContentTypeIds.Races)!;

        // Системный объект добавляется напрямую: служба не позволяет сохранить такой.
        var original = new Race
        {
            Name = "Эльф",
            SystemName = "эльф",
            Speed = 30,
            IsSystem = true,
        };

        await using (var context = await database.ContextFactory.CreateDbContextAsync())
        {
            context.Races.Add(original);
            await context.SaveChangesAsync();
        }

        var copyResult = await service.DuplicateAsync(ContentTypeIds.Races, original.Id);
        Assert.True(copyResult.IsSuccess, copyResult.Error);

        var copy = (Race)copyResult.Value;
        Assert.False(copy.IsSystem);
        Assert.Contains("копия", copy.Name, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(30, copy.Speed);

        // Копия сохраняется, а исходный системный объект остаётся неизменным.
        var saved = await service.SaveAsync(ContentTypeIds.Races, copy);
        Assert.True(saved.IsSuccess, saved.Error);

        var page = await service.SearchAsync(ContentTypeIds.Races, null, 0, 50);
        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task Обновление_НеСоздаётВторойОбъект()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var type = service.FindType(ContentTypeIds.Classes)!;

        var entity = (CharacterClass)CreateEntity(type, "Воин");
        await service.SaveAsync(ContentTypeIds.Classes, entity);

        entity.Name = "Воитель";
        entity.MaximumLevel = 30;
        await service.SaveAsync(ContentTypeIds.Classes, entity);

        var page = await service.SearchAsync(ContentTypeIds.Classes, null, 0, 50);
        var item = Assert.Single(page.Items);

        Assert.Equal("Воитель", item.Name);

        var loaded = (CharacterClass)(await service.GetAsync(ContentTypeIds.Classes, entity.Id))!;
        Assert.Equal(30, loaded.MaximumLevel);
    }

    [Fact]
    public async Task Удаление_УбираетОбъектИзСписка()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var type = service.FindType(ContentTypeIds.Effects)!;

        var entity = CreateEntity(type, "Благословение");
        await service.SaveAsync(ContentTypeIds.Effects, entity);

        var deleted = await service.DeleteAsync(ContentTypeIds.Effects, entity.Id);
        Assert.True(deleted.IsSuccess, deleted.Error);

        var page = await service.SearchAsync(ContentTypeIds.Effects, null, 0, 50);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Оружие_СохраняетБоевыеСвойстваПредмета()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var type = service.FindType(ContentTypeIds.Weapons)!;

        var weapon = (Item)CreateEntity(type, "Длинный меч");

        // Значения задаются через описание полей — так же, как это делает редактор.
        var damage = type.Fields.First(field => field.Name == "damage");
        var attack = type.Fields.First(field => field.Name == "attack");

        Assert.True(damage.TrySetText(weapon, "1d8 + Сила", out _));
        Assert.True(attack.TrySetText(weapon, "1d20 + Сила", out _));

        var saved = await service.SaveAsync(ContentTypeIds.Weapons, weapon);
        Assert.True(saved.IsSuccess, saved.Error);

        var loaded = (Item)(await service.GetAsync(ContentTypeIds.Weapons, weapon.Id))!;

        Assert.NotNull(loaded.Weapon);
        Assert.Equal("1d8 + Сила", loaded.Weapon!.DamageFormula);
        Assert.Equal("1d20 + Сила", loaded.Weapon.AttackFormula);
    }

    [Fact]
    public async Task Оружие_ОтбираетсяОтдельноОтОбычныхПредметов()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var itemType = service.FindType(ContentTypeIds.Items)!;
        var weaponType = service.FindType(ContentTypeIds.Weapons)!;

        await service.SaveAsync(ContentTypeIds.Items, CreateEntity(itemType, "Верёвка"));
        await service.SaveAsync(ContentTypeIds.Weapons, CreateEntity(weaponType, "Кинжал"));

        var weapons = await service.SearchAsync(ContentTypeIds.Weapons, null, 0, 50);
        var items = await service.SearchAsync(ContentTypeIds.Items, null, 0, 50);

        // Вид «Оружие» показывает только предметы с боевыми свойствами,
        // а «Предметы» — все записи, включая оружие.
        Assert.Equal("Кинжал", Assert.Single(weapons.Items).Name);
        Assert.Equal(2, items.TotalCount);
    }

    [Fact]
    public async Task ПолеСсылки_СвязываетОбъектыРазныхВидов()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var attributeType = service.FindType(ContentTypeIds.Attributes)!;
        var skillType = service.FindType(ContentTypeIds.Skills)!;

        var strength = CreateEntity(attributeType, "Ловкость");
        await service.SaveAsync(ContentTypeIds.Attributes, strength);

        var skill = CreateEntity(skillType, "Акробатика");
        var attributeField = skillType.Fields.First(field => field.Name == "attribute");
        attributeField.SetReference(skill, strength.Id);

        await service.SaveAsync(ContentTypeIds.Skills, skill);

        var loaded = (Skill)(await service.GetAsync(ContentTypeIds.Skills, skill.Id))!;
        Assert.Equal(strength.Id, loaded.LinkedAttributeId);

        var references = await service.GetReferencesAsync(ContentTypeIds.Attributes);
        Assert.Equal("Ловкость", Assert.Single(references).Name);
    }

    [Fact]
    public async Task ПовторноеВнутреннееИмя_ДаётПонятноеСообщение()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var type = service.FindType(ContentTypeIds.Races)!;

        await service.SaveAsync(ContentTypeIds.Races, CreateEntity(type, "Эльф"));

        var duplicate = CreateEntity(type, "Эльф");
        var result = await service.SaveAsync(ContentTypeIds.Races, duplicate);

        Assert.True(result.IsFailure);
        Assert.Contains("внутренним именем", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ЧисловоеПоле_ПринимаетЗапятуюИТочку()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var type = service.FindType(ContentTypeIds.Items)!;

        var item = CreateEntity(type, "Кольчуга");
        var weight = type.Fields.First(field => field.Name == "weight");

        Assert.True(weight.TrySetText(item, "12,5", out _));
        await service.SaveAsync(ContentTypeIds.Items, item);

        var loaded = (Item)(await service.GetAsync(ContentTypeIds.Items, item.Id))!;
        Assert.Equal(12.5, loaded.Weight);
    }

    [Fact]
    public void ЧисловоеПоле_СообщаетОбОшибкеРазбора()
    {
        var type = StandardContentTypes.Create().First(item => item.Id == ContentTypeIds.Items);
        var entity = type.CreateInstance();
        var weight = type.Fields.First(field => field.Name == "weight");

        Assert.False(weight.TrySetText(entity, "тяжёлый", out var error));
        Assert.Contains("число", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ОбязательноеПоле_НеПринимаетПустоеЗначение()
    {
        var type = StandardContentTypes.Create().First(item => item.Id == ContentTypeIds.Races);
        var entity = type.CreateInstance();
        var name = type.Fields.First(field => field.Name == "name");

        Assert.False(name.TrySetText(entity, "   ", out var error));
        Assert.Contains("обязательно", error, StringComparison.OrdinalIgnoreCase);
    }
}
