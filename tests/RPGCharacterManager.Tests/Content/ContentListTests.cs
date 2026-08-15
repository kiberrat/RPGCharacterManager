using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Content;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Tests.Support;

namespace RPGCharacterManager.Tests.Content;

/// <summary>
/// Проверка списков вложенных записей в редакторе контента на примере бонусов предмета.
///
/// Список описывается теми же полями, что и обычная форма, поэтому проверяется
/// именно механизм: добавление, изменение, удаление и копирование записей.
/// </summary>
public sealed class ContentListTests
{
    private static ContentService CreateService(TestDatabase database) => new(
        StandardContentTypes.Create(),
        database.ContextFactory,
        NullLogger<ContentService>.Instance);

    private static IContentTypeDescriptor ItemsType() =>
        StandardContentTypes.Create().Single(type => type.Id == ContentTypeIds.Items);

    private static IContentList Bonuses(IContentTypeDescriptor type) =>
        type.Collections.Single(list => list.Name == "bonuses");

    private static void Fill(IContentList list, object bonus, string formula, string name)
    {
        SetField(list, bonus, "formula", formula);
        SetField(list, bonus, "name", name);
        SetField(list, bonus, "target", "Величина");
    }

    private static void SetField(IContentList list, object bonus, string field, string value)
    {
        var description = list.Fields.Single(item => item.Name == field);

        Assert.True(description.TrySetText(bonus, value, out var error), error);
    }

    private static string GetField(IContentList list, object bonus, string field) =>
        list.Fields.Single(item => item.Name == field).GetText(bonus);

    [Fact]
    public async Task НоваяЗапись_УСохранённогоОбъекта_Добавляется()
    {
        await using var database = await TestDatabase.CreateAsync();

        var service = CreateService(database);
        var type = ItemsType();
        var list = Bonuses(type);

        var item = type.CreateInstance();
        type.SetName(item, "Кольчуга");

        Assert.True((await service.SaveAsync(type.Id, item)).IsSuccess);

        // Запись добавляется объекту, который уже существует в базе. Её ключ
        // заполнен конструктором, и без явного добавления EF Core принимает
        // её за изменение несуществующей строки и обрывает сохранение.
        var loaded = (await service.GetAsync(type.Id, item.Id))!;

        Fill(list, list.AddItem(loaded), "6", "защита_от_брони");

        var saved = await service.SaveAsync(type.Id, loaded);
        Assert.True(saved.IsSuccess, saved.Error);

        var reloaded = (await service.GetAsync(type.Id, item.Id))!;
        var bonus = Assert.Single(list.GetItems(reloaded));

        Assert.Equal("6", GetField(list, bonus, "formula"));
    }

    [Fact]
    public void СписокБонусов_ОписанУПредметовИОружия()
    {
        var types = StandardContentTypes.Create().ToList();

        foreach (var id in new[] { ContentTypeIds.Items, ContentTypeIds.Weapons })
        {
            var type = types.Single(item => item.Id == id);
            var list = type.Collections.Single(collection => collection.Name == "bonuses");

            Assert.NotEmpty(list.Fields);
            Assert.False(string.IsNullOrWhiteSpace(list.DisplayName), id);
            Assert.False(string.IsNullOrWhiteSpace(list.SingularName), id);
        }
    }

    [Fact]
    public async Task ЗаписиСписка_СохраняютсяИЧитаютсяБезПотерь()
    {
        await using var database = await TestDatabase.CreateAsync();

        var service = CreateService(database);
        var type = ItemsType();
        var list = Bonuses(type);

        var item = type.CreateInstance();
        type.SetName(item, "Кольчуга");

        Fill(list, list.AddItem(item), "6", "защита_от_брони");
        Fill(list, list.AddItem(item), "2", "ловкость_в_броне");

        var saved = await service.SaveAsync(type.Id, item);
        Assert.True(saved.IsSuccess, saved.Error);

        var loaded = await service.GetAsync(type.Id, item.Id);
        Assert.NotNull(loaded);

        var bonuses = list.GetItems(loaded!);

        Assert.Equal(2, bonuses.Count);
        Assert.Contains(bonuses, bonus => GetField(list, bonus, "name") == "защита_от_брони");
        Assert.Contains(bonuses, bonus => GetField(list, bonus, "formula") == "2");
    }

    [Fact]
    public async Task ИзменённаяЗапись_ОбновляетсяАУдалённая_Исчезает()
    {
        await using var database = await TestDatabase.CreateAsync();

        var service = CreateService(database);
        var type = ItemsType();
        var list = Bonuses(type);

        var item = type.CreateInstance();
        type.SetName(item, "Кольчуга");

        Fill(list, list.AddItem(item), "6", "защита_от_брони");
        Fill(list, list.AddItem(item), "2", "лишний");

        Assert.True((await service.SaveAsync(type.Id, item)).IsSuccess);

        var loaded = (await service.GetAsync(type.Id, item.Id))!;
        var bonuses = list.GetItems(loaded);

        var kept = bonuses.Single(bonus => GetField(list, bonus, "name") == "защита_от_брони");
        var removed = bonuses.Single(bonus => GetField(list, bonus, "name") == "лишний");

        SetField(list, kept, "formula", "8");
        list.RemoveItem(loaded, removed);

        Assert.True((await service.SaveAsync(type.Id, loaded)).IsSuccess);

        var reloaded = (await service.GetAsync(type.Id, item.Id))!;
        var result = Assert.Single(list.GetItems(reloaded));

        Assert.Equal("8", GetField(list, result, "formula"));
    }

    [Fact]
    public async Task КопияОбъекта_ПолучаетСобственныеЗаписиСписка()
    {
        await using var database = await TestDatabase.CreateAsync();

        var service = CreateService(database);
        var type = ItemsType();
        var list = Bonuses(type);

        var item = type.CreateInstance();
        type.SetName(item, "Кольчуга");

        Fill(list, list.AddItem(item), "6", "защита_от_брони");

        Assert.True((await service.SaveAsync(type.Id, item)).IsSuccess);

        var duplicate = await service.DuplicateAsync(type.Id, item.Id);
        Assert.True(duplicate.IsSuccess, duplicate.Error);

        var copied = Assert.Single(list.GetItems(duplicate.Value));
        var original = Assert.Single(list.GetItems((await service.GetAsync(type.Id, item.Id))!));

        Assert.Equal("защита_от_брони", GetField(list, copied, "name"));

        // Записи копии должны быть собственными: иначе правка копии изменила бы оригинал.
        Assert.NotEqual(((EntityBase)original).Id, ((EntityBase)copied).Id);

        Assert.True((await service.SaveAsync(type.Id, duplicate.Value)).IsSuccess);

        var originalAfterCopy = Assert.Single(list.GetItems((await service.GetAsync(type.Id, item.Id))!));
        Assert.Equal("6", GetField(list, originalAfterCopy, "formula"));
    }
}
