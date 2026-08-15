using Microsoft.EntityFrameworkCore;
using RPGCharacterManager.Core.Abstractions.Items;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Tests.Characters;

namespace RPGCharacterManager.Tests.Items;

/// <summary>
/// Проверка подсистемы экипировки: слотов, конструктора бонусов и автоматического
/// применения усилений к характеристикам, ресурсам, величинам и признакам.
/// </summary>
public sealed class EquipmentServiceTests
{
    private const string HalfOfValue = "ОкруглитьВниз((значение - 10) / 2)";

    private static async Task<Guid> CreateCharacterAsync(
        CharacterTestContext context,
        string name = "Воин",
        int level = 1)
    {
        var draft = new CharacterDraft { Level = level };
        draft.Character.Name = name;

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static async Task EquipAsync(CharacterTestContext context, Guid characterId, Item item)
    {
        await context.AddAsync(item);

        var equipped = await context.Equipment.EquipAsync(characterId, item.Id);
        Assert.True(equipped.IsSuccess, equipped.Error);
    }

    private static async Task<double> AttributeValueAsync(
        CharacterTestContext context,
        Guid characterId)
    {
        var sheet = await context.Sheets.LoadAsync(characterId);
        Assert.True(sheet.IsSuccess, sheet.Error);

        return Assert.Single(sheet.Value.Attributes).Value;
    }

    // ---------- Характеристики ----------

    [Fact]
    public async Task НадетыйПредмет_ДобавляетХарактеристику()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        var slot = CharacterContent.Slot("Тело", "тело");

        await context.AddAsync(strength);
        await context.AddAsync(slot);

        var characterId = await CreateCharacterAsync(context);

        Assert.Equal(10, await AttributeValueAsync(context, characterId));

        await EquipAsync(
            context,
            characterId,
            CharacterContent.Equipment(
                "Пояс великана",
                "пояс_великана",
                slot.Id,
                bonuses: CharacterContent.Bonus(BonusTargetKind.Attribute, "4", attributeId: strength.Id)));

        Assert.Equal(14, await AttributeValueAsync(context, characterId));
    }

    [Fact]
    public async Task СнятыйПредмет_ВозвращаетПрежнееЗначение()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        var slot = CharacterContent.Slot("Тело", "тело");

        await context.AddAsync(strength);
        await context.AddAsync(slot);

        var characterId = await CreateCharacterAsync(context);

        await EquipAsync(
            context,
            characterId,
            CharacterContent.Equipment(
                "Пояс великана",
                "пояс_великана",
                slot.Id,
                bonuses: CharacterContent.Bonus(BonusTargetKind.Attribute, "4", attributeId: strength.Id)));

        var slots = await context.Equipment.GetSlotsAsync(characterId);
        Assert.True(slots.IsSuccess, slots.Error);

        var equipped = Assert.Single(Assert.Single(slots.Value).Items);

        var removed = await context.Equipment.UnequipAsync(characterId, equipped.InventoryItemId);
        Assert.True(removed.IsSuccess, removed.Error);

        Assert.Equal(10, await AttributeValueAsync(context, characterId));
    }

    [Fact]
    public async Task БонусХарактеристики_УчитываетсяВычисляемымиЗначениями()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute(
            "Сила",
            "сила",
            defaultValue: 10,
            modifierFormula: HalfOfValue);

        var slot = CharacterContent.Slot("Тело", "тело");

        await context.AddAsync(strength);
        await context.AddAsync(slot);
        await context.AddAsync(CharacterContent.Resource("Здоровье", "здоровье", "10 + сила"));

        var characterId = await CreateCharacterAsync(context);

        await EquipAsync(
            context,
            characterId,
            CharacterContent.Equipment(
                "Пояс великана",
                "пояс_великана",
                slot.Id,
                bonuses: CharacterContent.Bonus(BonusTargetKind.Attribute, "6", attributeId: strength.Id)));

        var sheet = await context.Sheets.LoadAsync(characterId);
        Assert.True(sheet.IsSuccess, sheet.Error);

        var attribute = Assert.Single(sheet.Value.Attributes);

        // Базовое значение остаётся собственным значением персонажа: иначе бонус
        // сохранился бы в нём и остался после снятия предмета.
        Assert.Equal(10, attribute.BaseValue);
        Assert.Equal(16, attribute.Value);
        Assert.Equal(3, attribute.Modifier);

        // Формула ресурса ссылается на Силу и видит её вместе с бонусом.
        Assert.Equal(26, Assert.Single(sheet.Value.Resources).Maximum);
    }

    // ---------- Ресурсы ----------

    [Fact]
    public async Task НадетыйПредмет_ДобавляетМаксимумРесурса()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var health = CharacterContent.Resource("Здоровье", "здоровье", "20");
        var slot = CharacterContent.Slot("Тело", "тело");

        await context.AddAsync(health);
        await context.AddAsync(slot);

        var characterId = await CreateCharacterAsync(context);

        await EquipAsync(
            context,
            characterId,
            CharacterContent.Equipment(
                "Латы",
                "латы",
                slot.Id,
                bonuses: CharacterContent.Bonus(BonusTargetKind.Resource, "8", resourceId: health.Id)));

        var sheet = await context.Sheets.LoadAsync(characterId);
        Assert.True(sheet.IsSuccess, sheet.Error);

        Assert.Equal(28, Assert.Single(sheet.Value.Resources).Maximum);
    }

    // ---------- Величины и признаки ----------

    [Fact]
    public async Task БонусВеличины_ДоступенФормуламИгровойСистемы()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var dexterity = CharacterContent.Attribute("Ловкость", "ловкость", defaultValue: 14);
        var slot = CharacterContent.Slot("Тело", "тело");

        await context.AddAsync(dexterity);
        await context.AddAsync(slot);

        // Вычисляемая характеристика ссылается на величину, которой в приложении нет:
        // её создаёт бонус предмета.
        await context.AddAsync(CharacterContent.Attribute(
            "Защита",
            "защита",
            formula: "10 + защита_от_брони"));

        var characterId = await CreateCharacterAsync(context);

        await EquipAsync(
            context,
            characterId,
            CharacterContent.Equipment(
                "Кольчуга",
                "кольчуга",
                slot.Id,
                bonuses: CharacterContent.Bonus(
                    BonusTargetKind.Variable,
                    "6",
                    name: "защита_от_брони")));

        var sheet = await context.Sheets.LoadAsync(characterId);
        Assert.True(sheet.IsSuccess, sheet.Error);

        var defence = sheet.Value.Attributes.Single(attribute => attribute.SystemName == "защита");

        Assert.Equal(16, defence.Value);
    }

    [Fact]
    public async Task БонусПризнака_ВиденТребованиямДругихОбъектов()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var slot = CharacterContent.Slot("Тело", "тело");
        await context.AddAsync(slot);

        var characterId = await CreateCharacterAsync(context);

        await EquipAsync(
            context,
            characterId,
            CharacterContent.Equipment(
                "Плащ теней",
                "плащ_теней",
                slot.Id,
                bonuses: CharacterContent.Bonus(BonusTargetKind.Tag, name: "скрытность")));

        var slots = await context.Equipment.GetSlotsAsync(characterId);
        Assert.True(slots.IsSuccess, slots.Error);

        var item = Assert.Single(Assert.Single(slots.Value).Items);
        var bonus = Assert.Single(item.Bonuses);

        Assert.True(bonus.IsApplied);
        Assert.Contains("скрытность", bonus.Description, StringComparison.CurrentCulture);
    }

    // ---------- Условия ----------

    [Fact]
    public async Task БонусСУсловием_ДействуетТолькоПриЕгоВыполнении()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        var slot = CharacterContent.Slot("Тело", "тело");

        await context.AddAsync(strength);
        await context.AddAsync(slot);

        var characterId = await CreateCharacterAsync(context, level: 1);

        await EquipAsync(
            context,
            characterId,
            CharacterContent.Equipment(
                "Растущий клинок",
                "растущий_клинок",
                slot.Id,
                bonuses: CharacterContent.Bonus(
                    BonusTargetKind.Attribute,
                    "5",
                    attributeId: strength.Id,
                    condition: "уровень >= 5")));

        Assert.Equal(10, await AttributeValueAsync(context, characterId));

        var levelUp = await context.Progression.LevelUpAsync(characterId, levels: 4);
        Assert.True(levelUp.IsSuccess, levelUp.Error);

        Assert.Equal(15, await AttributeValueAsync(context, characterId));
    }

    [Fact]
    public async Task ФормулаБонуса_ВычисляетсяБезУчётаДругихПредметов()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        var body = CharacterContent.Slot("Тело", "тело");
        var hands = CharacterContent.Slot("Руки", "руки");

        await context.AddAsync(strength);
        await context.AddAsync(body, hands);

        var characterId = await CreateCharacterAsync(context);

        // Оба бонуса ссылаются на Силу. Если бы формулы вычислялись по очереди,
        // второй предмет удваивал бы вклад первого и итог зависел бы от порядка.
        await EquipAsync(
            context,
            characterId,
            CharacterContent.Equipment(
                "Латы",
                "латы",
                body.Id,
                bonuses: CharacterContent.Bonus(
                    BonusTargetKind.Attribute,
                    "сила / 10",
                    attributeId: strength.Id)));

        await EquipAsync(
            context,
            characterId,
            CharacterContent.Equipment(
                "Перчатки",
                "перчатки",
                hands.Id,
                bonuses: CharacterContent.Bonus(
                    BonusTargetKind.Attribute,
                    "сила / 10",
                    attributeId: strength.Id)));

        Assert.Equal(12, await AttributeValueAsync(context, characterId));
    }

    [Fact]
    public async Task АвторскаяЭкипировка_СоздаётсяНадеваетсяИСразуПрименяетБонусы()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        var health = CharacterContent.Resource("Хиты", "хиты", "20");
        var slot = CharacterContent.Slot("Голова", "голова");
        await context.AddAsync(strength);
        await context.AddAsync(health);
        await context.AddAsync(slot);

        var ownerId = await CreateCharacterAsync(context, "Владелец шлема");
        var otherId = await CreateCharacterAsync(context, "Другой герой");
        var draft = new LocalEquipmentDraft(
            "Шлем исполина",
            "Сделан специально для героя.",
            "Шлем",
            "Редкий",
            2,
            350,
            "зм",
            [
                new LocalEquipmentBonusDraft(
                    BonusTargetKind.Attribute, strength.Id, null, null, "2", null),
                new LocalEquipmentBonusDraft(
                    BonusTargetKind.Resource, null, health.Id, null, "5", null),
            ]);

        var created = await context.Equipment.CreateLocalAndEquipAsync(ownerId, slot.Id, draft);
        Assert.True(created.IsSuccess, created.Error);

        var sheet = await context.Sheets.LoadAsync(ownerId);
        Assert.True(sheet.IsSuccess, sheet.Error);
        Assert.Equal(12, Assert.Single(sheet.Value.Attributes).Value);
        Assert.Equal(25, Assert.Single(sheet.Value.Resources).Maximum);

        var slots = await context.Equipment.GetSlotsAsync(ownerId);
        Assert.True(slots.IsSuccess, slots.Error);
        var item = Assert.Single(Assert.Single(slots.Value).Items);
        Assert.Equal(created.Value, item.InventoryItemId);
        Assert.Equal("Шлем исполина", item.Name);
        Assert.Equal(2, item.Bonuses.Count);
        Assert.All(item.Bonuses, bonus => Assert.True(bonus.IsApplied));

        var otherOptions = await context.Equipment.GetAvailableItemsAsync(otherId, slot.Id, null, true);
        Assert.DoesNotContain(otherOptions.Options, option => option.Id == item.ItemId);
        Assert.True((await context.Equipment.EquipAsync(otherId, item.ItemId)).IsFailure);

        await using var database = await context.CreateContextAsync();
        var stored = await database.Items
            .Include(entry => entry.Bonuses)
            .SingleAsync(entry => entry.Id == item.ItemId);
        Assert.Equal(ownerId, stored.OwnerCharacterId);
        Assert.Equal(2, stored.Bonuses.Count);
    }
    // ---------- Слоты ----------

    [Fact]
    public async Task Слот_НеПринимаетБольшеПредметовЧемВмещает()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var slot = CharacterContent.Slot("Кольцо", "кольцо", maximumItems: 2);
        await context.AddAsync(slot);

        var characterId = await CreateCharacterAsync(context);

        await EquipAsync(context, characterId, CharacterContent.Equipment("Кольцо силы", "кольцо_силы", slot.Id));
        await EquipAsync(context, characterId, CharacterContent.Equipment("Кольцо огня", "кольцо_огня", slot.Id));

        var third = CharacterContent.Equipment("Кольцо льда", "кольцо_льда", slot.Id);
        await context.AddAsync(third);

        var result = await context.Equipment.EquipAsync(characterId, third.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("Кольцо", result.Error, StringComparison.CurrentCulture);
    }

    [Fact]
    public async Task ПредметБезСлота_НеНадевается()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var item = CharacterContent.Equipment("Верёвка", "верёвка");
        await context.AddAsync(item);

        var result = await context.Equipment.EquipAsync(characterId, item.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("слот", result.Error, StringComparison.CurrentCulture);
    }

    [Fact]
    public async Task Надевание_ПроверяетТребованияПредмета()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 8);
        var slot = CharacterContent.Slot("Тело", "тело");

        await context.AddAsync(strength);
        await context.AddAsync(slot);

        var characterId = await CreateCharacterAsync(context);

        var heavy = CharacterContent.Equipment("Латы", "латы", slot.Id, requirements: "сила >= 15");
        await context.AddAsync(heavy);

        var result = await context.Equipment.EquipAsync(characterId, heavy.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("Латы", result.Error, StringComparison.CurrentCulture);
    }

    [Fact]
    public async Task ДоступныеПредметы_ОтбираютсяПоСлоту()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var body = CharacterContent.Slot("Тело", "тело");
        var hands = CharacterContent.Slot("Руки", "руки");

        await context.AddAsync(body, hands);

        var characterId = await CreateCharacterAsync(context);

        await context.AddAsync(
            CharacterContent.Equipment("Латы", "латы", body.Id),
            CharacterContent.Equipment("Перчатки", "перчатки", hands.Id));

        var options = await context.Equipment.GetAvailableItemsAsync(characterId, body.Id, null, true);

        Assert.Equal("Латы", Assert.Single(options.Options).Name);
    }
}
