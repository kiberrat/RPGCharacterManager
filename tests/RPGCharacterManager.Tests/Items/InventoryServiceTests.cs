using Microsoft.EntityFrameworkCore;
using RPGCharacterManager.Core.Abstractions.Items;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Tests.Characters;

namespace RPGCharacterManager.Tests.Items;

/// <summary>
/// Проверка инвентаря: категорий, веса, стоимости, зарядов, использования,
/// вместилищ, поиска и сортировки.
/// </summary>
public sealed class InventoryServiceTests
{
    private static async Task<Guid> CreateCharacterAsync(CharacterTestContext context, int level = 1)
    {
        var draft = new CharacterDraft { Level = level };
        draft.Character.Name = "Странник";

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static async Task<InventoryState> LoadAsync(
        CharacterTestContext context,
        Guid characterId,
        InventoryQuery? query = null)
    {
        var result = await context.Inventory.GetAsync(characterId, query ?? new InventoryQuery());
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static async Task GiveAsync(
        CharacterTestContext context,
        Guid characterId,
        Item item,
        int count = 1)
    {
        await context.AddAsync(item);

        var added = await context.Inventory.AddAsync(characterId, item.Id, count);
        Assert.True(added.IsSuccess, added.Error);
    }

    // ---------- Вес ----------

    [Fact]
    public async Task Вес_СуммируетсяПоКоличествуПредметов()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await GiveAsync(context, characterId, CharacterContent.Item("Камень", "камень", weight: 2.5), 4);

        var state = await LoadAsync(context, characterId);

        Assert.Equal(10, state.Weight.Total);
        Assert.Null(state.Weight.Capacity);
        Assert.False(state.Weight.IsOverloaded);
    }

    [Fact]
    public async Task ПереносимыйВес_ВычисляетсяФормулойИгровойСистемы()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 12);
        await context.AddAsync(strength);

        var system = new GameSystem
        {
            Name = "Своя система",
            SystemName = "своя",
            CarryCapacityFormula = "сила * 2",
            WeightUnit = "кг",
        };

        await context.AddAsync(system);

        var draft = new CharacterDraft { Level = 1, GameSystemId = system.Id };
        draft.Character.Name = "Носильщик";

        var created = await context.Builder.CreateAsync(draft);
        Assert.True(created.IsSuccess, created.Error);

        await GiveAsync(context, created.Value, CharacterContent.Item("Сундук", "сундук", weight: 30));

        var state = await LoadAsync(context, created.Value);

        Assert.Equal(24, state.Weight.Capacity);
        Assert.Equal("кг", state.Weight.Unit);
        Assert.True(state.Weight.IsOverloaded);
    }

    // ---------- Стоимость ----------

    [Fact]
    public async Task Стоимость_СчитаетсяОтдельноПоКаждойВалюте()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await GiveAsync(
            context,
            characterId,
            CharacterContent.Item("Слиток", "слиток", price: 50, currency: "золото"),
            2);

        await GiveAsync(
            context,
            characterId,
            CharacterContent.Item("Кристалл", "кристалл", price: 7, currency: "кредиты"));

        var state = await LoadAsync(context, characterId);

        Assert.Equal(2, state.Money.Count);
        Assert.Equal(100, state.Money.Single(total => total.Currency == "золото").Amount);
        Assert.Equal(7, state.Money.Single(total => total.Currency == "кредиты").Amount);
    }

    // ---------- Стопки ----------

    [Fact]
    public async Task СкладывающийсяПредмет_ДополняетСуществующуюСтопку()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);
        var arrow = CharacterContent.Item("Стрела", "стрела", stackable: true);

        await GiveAsync(context, characterId, arrow, 20);

        var added = await context.Inventory.AddAsync(characterId, arrow.Id, 30);
        Assert.True(added.IsSuccess, added.Error);

        var entry = Assert.Single((await LoadAsync(context, characterId)).Entries);

        Assert.Equal(50, entry.Count);
    }

    [Fact]
    public async Task ПревышениеРазмераСтопки_СоздаётВторуюЗапись()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await GiveAsync(
            context,
            characterId,
            CharacterContent.Item("Патрон", "патрон", stackable: true, maximumStackSize: 30),
            70);

        var entries = (await LoadAsync(context, characterId)).Entries;

        Assert.Equal(3, entries.Count);
        Assert.Equal(70, entries.Sum(entry => entry.Count));
        Assert.All(entries, entry => Assert.True(entry.Count <= 30));
    }

    // ---------- Заряды ----------

    [Fact]
    public async Task Заряды_ВычисляютсяФормулойПредмета()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var intellect = CharacterContent.Attribute("Интеллект", "интеллект", defaultValue: 4);
        await context.AddAsync(intellect);

        var characterId = await CreateCharacterAsync(context);

        await GiveAsync(
            context,
            characterId,
            CharacterContent.Item("Жезл", "жезл", chargesFormula: "интеллект + 1"));

        var entry = Assert.Single((await LoadAsync(context, characterId)).Entries);

        Assert.Equal(5, entry.MaximumCharges);
        Assert.Equal(5, entry.RemainingCharges);
    }

    [Fact]
    public async Task ИспользованиеЗаряда_УменьшаетОстатокИВосстанавливаетсяПолностью()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await GiveAsync(
            context,
            characterId,
            CharacterContent.Item(
                "Амулет",
                "амулет",
                chargesFormula: "3",
                useCost: ItemUseCost.Charge,
                useEffects: CharacterContent.UseEffect(name: "Вспышка света")));

        var entry = Assert.Single((await LoadAsync(context, characterId)).Entries);

        var used = await context.Inventory.UseAsync(characterId, entry.InventoryItemId);
        Assert.True(used.IsSuccess, used.Error);
        Assert.True(used.Value.SpentCharge);
        Assert.Equal(2, used.Value.RemainingCharges);

        var restored = await context.Inventory.RestoreChargesAsync(characterId, entry.InventoryItemId);
        Assert.True(restored.IsSuccess, restored.Error);

        Assert.Equal(3, Assert.Single((await LoadAsync(context, characterId)).Entries).RemainingCharges);
    }

    [Fact]
    public async Task ЗарядыЗакончились_ПредметБольшеНеИспользуется()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await GiveAsync(
            context,
            characterId,
            CharacterContent.Item(
                "Свисток",
                "свисток",
                chargesFormula: "1",
                useCost: ItemUseCost.Charge,
                useEffects: CharacterContent.UseEffect(name: "Сигнал")));

        var entry = Assert.Single((await LoadAsync(context, characterId)).Entries);

        Assert.True((await context.Inventory.UseAsync(characterId, entry.InventoryItemId)).IsSuccess);

        var second = await context.Inventory.UseAsync(characterId, entry.InventoryItemId);

        Assert.True(second.IsFailure);
        Assert.Contains("зарядов", second.Error, StringComparison.CurrentCulture);
        Assert.False(Assert.Single((await LoadAsync(context, characterId)).Entries).CanUse);
    }

    // ---------- Использование ----------

    [Fact]
    public async Task ИспользованиеПредмета_ВосстанавливаетРесурсПоФормуле()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var health = CharacterContent.Resource("Здоровье", "здоровье", maximumFormula: "30");
        await context.AddAsync(health);

        var characterId = await CreateCharacterAsync(context);

        await SetResourceAsync(context, characterId, health.Id, current: 10);

        await GiveAsync(
            context,
            characterId,
            CharacterContent.Item(
                "Зелье лечения",
                "зелье_лечения",
                stackable: true,
                useCost: ItemUseCost.Unit,
                useEffects: CharacterContent.UseEffect("8", health.Id, "Лечение")),
            2);

        var entry = Assert.Single((await LoadAsync(context, characterId)).Entries);

        var used = await context.Inventory.UseAsync(characterId, entry.InventoryItemId);

        Assert.True(used.IsSuccess, used.Error);
        Assert.True(used.Value.SpentUnit);
        Assert.Equal(1, used.Value.RemainingCount);

        var effect = Assert.Single(used.Value.Effects);
        Assert.True(effect.IsApplied);
        Assert.Equal(8, effect.Value);

        Assert.Equal(18, await ResourceValueAsync(context, characterId, health.Id));
    }

    [Fact]
    public async Task ВосстановлениеРесурса_НеПревышаетМаксимум()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var health = CharacterContent.Resource("Здоровье", "здоровье", maximumFormula: "20");
        await context.AddAsync(health);

        var characterId = await CreateCharacterAsync(context);

        await SetResourceAsync(context, characterId, health.Id, current: 18);

        await GiveAsync(
            context,
            characterId,
            CharacterContent.Item(
                "Эликсир",
                "эликсир",
                useCost: ItemUseCost.Unit,
                useEffects: CharacterContent.UseEffect("100", health.Id, "Лечение")));

        var entry = Assert.Single((await LoadAsync(context, characterId)).Entries);

        Assert.True((await context.Inventory.UseAsync(characterId, entry.InventoryItemId)).IsSuccess);
        Assert.Equal(20, await ResourceValueAsync(context, characterId, health.Id));
    }

    [Fact]
    public async Task ПоследняяЕдиницаПредмета_УбираетЗаписьИнвентаря()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await GiveAsync(
            context,
            characterId,
            CharacterContent.Item(
                "Паёк",
                "паёк",
                useCost: ItemUseCost.Unit,
                useEffects: CharacterContent.UseEffect(name: "Сытость")));

        var entry = Assert.Single((await LoadAsync(context, characterId)).Entries);

        Assert.True((await context.Inventory.UseAsync(characterId, entry.InventoryItemId)).IsSuccess);
        Assert.Empty((await LoadAsync(context, characterId)).Entries);
    }

    [Fact]
    public async Task ПредметБезДействий_НеИспользуется()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await GiveAsync(context, characterId, CharacterContent.Item("Ключ", "ключ"));

        var entry = Assert.Single((await LoadAsync(context, characterId)).Entries);

        Assert.False(entry.CanUse);
        Assert.Null(entry.UnusableReason);
        Assert.True((await context.Inventory.UseAsync(characterId, entry.InventoryItemId)).IsFailure);
    }

    // ---------- Контейнеры ----------

    [Fact]
    public async Task ВместилищеСОблегчением_УменьшаетНошу()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var bag = CharacterContent.Container("Сумка хранения", "сумка", weight: 2, contentWeightFactor: 0);
        var stone = CharacterContent.Item("Слиток", "слиток", weight: 20);

        await GiveAsync(context, characterId, bag);
        await GiveAsync(context, characterId, stone);

        var state = await LoadAsync(context, characterId);

        Assert.Equal(22, state.Weight.Total);

        var bagEntry = state.Entries.Single(entry => entry.ItemId == bag.Id);
        var stoneEntry = state.Entries.Single(entry => entry.ItemId == stone.Id);

        var moved = await context.Inventory
            .MoveAsync(characterId, stoneEntry.InventoryItemId, bagEntry.InventoryItemId);

        Assert.True(moved.IsSuccess, moved.Error);

        var afterMove = await LoadAsync(context, characterId);

        Assert.Equal(2, afterMove.Weight.Total);
        Assert.Equal(1, afterMove.Entries.Single(entry => entry.ItemId == stone.Id).Depth);
    }

    [Fact]
    public async Task ПревышениеВместимости_НеРазрешаетПереложитьПредмет()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var bag = CharacterContent.Container("Мешок", "мешок", capacity: 5);
        var anvil = CharacterContent.Item("Наковальня", "наковальня", weight: 40);

        await GiveAsync(context, characterId, bag);
        await GiveAsync(context, characterId, anvil);

        var state = await LoadAsync(context, characterId);
        var bagEntry = state.Entries.Single(entry => entry.ItemId == bag.Id);
        var anvilEntry = state.Entries.Single(entry => entry.ItemId == anvil.Id);

        var moved = await context.Inventory
            .MoveAsync(characterId, anvilEntry.InventoryItemId, bagEntry.InventoryItemId);

        Assert.True(moved.IsFailure);
        Assert.Contains("Мешок", moved.Error, StringComparison.CurrentCulture);
    }

    [Fact]
    public async Task Вместилище_НельзяПоложитьВСамоСебя()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var outer = CharacterContent.Container("Сундук", "сундук");
        var inner = CharacterContent.Container("Шкатулка", "шкатулка");

        await GiveAsync(context, characterId, outer);
        await GiveAsync(context, characterId, inner);

        var state = await LoadAsync(context, characterId);
        var outerEntry = state.Entries.Single(entry => entry.ItemId == outer.Id);
        var innerEntry = state.Entries.Single(entry => entry.ItemId == inner.Id);

        Assert.True((await context.Inventory
            .MoveAsync(characterId, innerEntry.InventoryItemId, outerEntry.InventoryItemId)).IsSuccess);

        var loop = await context.Inventory
            .MoveAsync(characterId, outerEntry.InventoryItemId, innerEntry.InventoryItemId);

        Assert.True(loop.IsFailure);
    }

    [Fact]
    public async Task УдалениеВместилища_ОставляетСодержимоеУПерсонажа()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var bag = CharacterContent.Container("Рюкзак", "рюкзак");
        var rope = CharacterContent.Item("Верёвка", "верёвка", weight: 3);

        await GiveAsync(context, characterId, bag);
        await GiveAsync(context, characterId, rope);

        var state = await LoadAsync(context, characterId);
        var bagEntry = state.Entries.Single(entry => entry.ItemId == bag.Id);
        var ropeEntry = state.Entries.Single(entry => entry.ItemId == rope.Id);

        Assert.True((await context.Inventory
            .MoveAsync(characterId, ropeEntry.InventoryItemId, bagEntry.InventoryItemId)).IsSuccess);

        Assert.True((await context.Inventory.RemoveAsync(characterId, bagEntry.InventoryItemId)).IsSuccess);

        var afterRemoval = await LoadAsync(context, characterId);

        var remaining = Assert.Single(afterRemoval.Entries);
        Assert.Equal(rope.Id, remaining.ItemId);
        Assert.Null(remaining.ContainerId);
        Assert.Equal(3, afterRemoval.Weight.Total);
    }

    // ---------- Категории ----------

    [Fact]
    public async Task КатегорияОтбирает_ПредметыВложенныхКатегорий()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var gear = CharacterContent.Category("Снаряжение", "снаряжение");
        var armour = CharacterContent.Category("Броня", "броня", gear.Id);

        await context.AddAsync(gear, armour);

        var characterId = await CreateCharacterAsync(context);

        await GiveAsync(context, characterId, CharacterContent.Item("Шлем", "шлем", categoryId: armour.Id));
        await GiveAsync(context, characterId, CharacterContent.Item("Верёвка", "верёвка", categoryId: gear.Id));
        await GiveAsync(context, characterId, CharacterContent.Item("Хлеб", "хлеб"));

        var state = await LoadAsync(context, characterId);

        var gearNode = state.Categories.Single(node => node.CategoryId == gear.Id);
        var armourNode = state.Categories.Single(node => node.CategoryId == armour.Id);

        Assert.Equal(2, gearNode.Count);
        Assert.Equal(1, armourNode.Count);

        // Раздел «Все предметы» — корень дерева, поэтому созданные пользователем
        // категории начинаются с первого уровня вложенности.
        Assert.Equal(1, gearNode.Depth);
        Assert.Equal(2, armourNode.Depth);

        var filtered = await LoadAsync(context, characterId, new InventoryQuery(CategoryId: gear.Id));

        Assert.Equal(2, filtered.Entries.Count);
        Assert.DoesNotContain(filtered.Entries, entry => entry.Name == "Хлеб");
    }

    // ---------- Поиск ----------

    [Fact]
    public async Task Поиск_НаходитПредметыПоНазваниюИТипу()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await GiveAsync(context, characterId, CharacterContent.Item("Зелье лечения", "зелье_лечения"));
        await GiveAsync(context, characterId, CharacterContent.Item("Верёвка", "верёвка"));

        var found = await LoadAsync(context, characterId, new InventoryQuery("зелье"));

        Assert.Equal("Зелье лечения", Assert.Single(found.Entries).Name);

        var missing = await LoadAsync(context, characterId, new InventoryQuery("посох"));

        Assert.Empty(missing.Entries);
    }

    // ---------- Сортировка ----------

    [Fact]
    public async Task Сортировка_УпорядочиваетПредметыПоВыбранномуПризнаку()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await GiveAsync(context, characterId, CharacterContent.Item("Перо", "перо", weight: 0.1, price: 1));
        await GiveAsync(context, characterId, CharacterContent.Item("Щит", "щит", weight: 6, price: 10));
        await GiveAsync(context, characterId, CharacterContent.Item("Кинжал", "кинжал", weight: 1, price: 20));

        var byName = await LoadAsync(context, characterId, new InventoryQuery(Sort: InventorySort.Name));
        Assert.Equal(["Кинжал", "Перо", "Щит"], byName.Entries.Select(entry => entry.Name));

        var byWeight = await LoadAsync(
            context,
            characterId,
            new InventoryQuery(Sort: InventorySort.Weight, Descending: true));

        Assert.Equal(["Щит", "Кинжал", "Перо"], byWeight.Entries.Select(entry => entry.Name));

        var byPrice = await LoadAsync(context, characterId, new InventoryQuery(Sort: InventorySort.Price));
        Assert.Equal(["Перо", "Щит", "Кинжал"], byPrice.Entries.Select(entry => entry.Name));
    }

    [Fact]
    public async Task ЛокальныйПредмет_ПринадлежитТолькоСоздавшемуЕгоПерсонажу()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var ownerId = await CreateCharacterAsync(context);
        var otherCharacterId = await CreateCharacterAsync(context);
        var draft = new LocalInventoryItemDraft(
            "Клинок снов",
            "Авторское оружие персонажа.",
            "Оружие",
            1.5,
            125,
            "зм",
            true,
            "2к6",
            "психический");

        var created = await context.Inventory.CreateLocalAsync(ownerId, draft, 1);
        Assert.True(created.IsSuccess, created.Error);

        var entry = Assert.Single((await LoadAsync(context, ownerId)).Entries);
        Assert.Equal("Клинок снов", entry.Name);
        Assert.Equal("Авторское оружие персонажа.", entry.Description);
        Assert.Equal("Оружие", entry.ItemType);

        var otherOptions = await context.Inventory.GetAvailableItemsAsync(otherCharacterId, null);
        Assert.DoesNotContain(otherOptions.Options, option => option.Id == entry.ItemId);

        var forbidden = await context.Inventory.AddAsync(otherCharacterId, entry.ItemId, 1);
        Assert.True(forbidden.IsFailure);

        await using var database = await context.CreateContextAsync();
        var stored = await database.Items
            .Include(item => item.Weapon)
            .SingleAsync(item => item.Id == entry.ItemId);
        Assert.Equal(ownerId, stored.OwnerCharacterId);
        Assert.NotNull(stored.Weapon);
        Assert.Equal("2к6", stored.Weapon.DamageFormula);
        Assert.Equal("психический", stored.Weapon.DamageType);
    }
    // ---------- Количество ----------

    [Fact]
    public async Task ИзменениеКоличестваДоНуля_УбираетЗапись()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await GiveAsync(context, characterId, CharacterContent.Item("Факел", "факел", stackable: true), 2);

        var entry = Assert.Single((await LoadAsync(context, characterId)).Entries);

        Assert.True((await context.Inventory.ChangeCountAsync(characterId, entry.InventoryItemId, -1)).IsSuccess);
        Assert.Equal(1, Assert.Single((await LoadAsync(context, characterId)).Entries).Count);

        Assert.True((await context.Inventory.ChangeCountAsync(characterId, entry.InventoryItemId, -1)).IsSuccess);
        Assert.Empty((await LoadAsync(context, characterId)).Entries);
    }

    [Fact]
    public async Task УдалениеНадетогоПредмета_СнимаетЕгоСоСлота()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var slot = CharacterContent.Slot("Тело", "тело");
        await context.AddAsync(slot);

        var characterId = await CreateCharacterAsync(context);
        var armour = CharacterContent.Equipment("Кираса", "кираса", slot.Id);

        await context.AddAsync(armour);

        var equipped = await context.Equipment.EquipAsync(characterId, armour.Id);
        Assert.True(equipped.IsSuccess, equipped.Error);

        var entry = Assert.Single((await LoadAsync(context, characterId)).Entries);
        Assert.True(entry.IsEquipped);

        Assert.True((await context.Inventory.RemoveAsync(characterId, entry.InventoryItemId)).IsSuccess);

        var slots = await context.Equipment.GetSlotsAsync(characterId);
        Assert.True(slots.IsSuccess, slots.Error);
        Assert.Empty(Assert.Single(slots.Value).Items);
    }

    private static async Task SetResourceAsync(
        CharacterTestContext context,
        Guid characterId,
        Guid resourceId,
        double current)
    {
        await using var database = await context.CreateContextAsync();

        var record = await database.CharacterResources
            .SingleAsync(entry => entry.CharacterId == characterId && entry.ResourceId == resourceId);

        record.Current = current;

        await database.SaveChangesAsync();
    }

    private static async Task<double> ResourceValueAsync(
        CharacterTestContext context,
        Guid characterId,
        Guid resourceId)
    {
        await using var database = await context.CreateContextAsync();

        var record = await database.CharacterResources
            .SingleAsync(entry => entry.CharacterId == characterId && entry.ResourceId == resourceId);

        return record.Current;
    }
}
