using RPGCharacterManager.Core.Abstractions.Dice;
using RPGCharacterManager.Core.Abstractions.History;
using RPGCharacterManager.Core.Abstractions.Items;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Tests.Characters;

namespace RPGCharacterManager.Tests.History;

/// <summary>
/// Проверка журнала событий: запись бросков, изменений ресурсов, применения
/// заклинаний, использования предметов и смены экипировки, отбор и очистка.
/// </summary>
public sealed class HistoryServiceTests
{
    private static async Task<Guid> CreateCharacterAsync(CharacterTestContext context, string name = "Странник")
    {
        var draft = new CharacterDraft { Level = 3 };
        draft.Character.Name = name;

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static async Task<HistoryPage> LoadAsync(
        HistoryTestContext context,
        Guid? characterId = null,
        HistoryKind kind = HistoryKind.Any,
        string? search = null)
    {
        var result = await context.Service.GetAsync(HistoryQuery.ForCharacter(characterId, kind, search));
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static async Task<Guid> GiveResourceAsync(
        CharacterTestContext context,
        string name = "Здоровье",
        string systemName = "здоровье")
    {
        var resource = CharacterContent.Resource(name, systemName, "20", "20");
        await context.AddAsync(resource);

        return resource.Id;
    }

    // ---------- Создание персонажа ----------

    [Fact]
    public async Task Журнал_СозданиеПерсонажа_ПопадаетВЖурнал()
    {
        await using var context = await HistoryTestContext.CreateAsync(1);

        var characterId = await CreateCharacterAsync(context.Characters, "Аргус");

        var record = Assert.Single(
            (await LoadAsync(context, kind: HistoryKind.Character)).Records,
            entry => entry.Action == HistoryActions.CharacterCreated);

        Assert.Equal(characterId, record.CharacterId);
        Assert.Equal("Аргус", record.CharacterName);
        Assert.Contains("Аргус", record.Description, StringComparison.Ordinal);
    }

    // ---------- Броски ----------

    [Fact]
    public async Task Журнал_Бросок_ПоказанСВыражениемИИтогом()
    {
        await using var context = await HistoryTestContext.CreateAsync(4, 5);

        var roll = await context.Dice.Service.RollAsync(new RollRequest("2d6", Title: "Проверка"));
        Assert.True(roll.IsSuccess, roll.Error);

        var record = Assert.Single(
            (await LoadAsync(context, kind: HistoryKind.Roll)).Records,
            entry => entry.Kind == HistoryKind.Roll);

        Assert.Equal(HistoryActions.Roll, record.Action);
        Assert.Contains("2d6", record.Description, StringComparison.Ordinal);
        Assert.Contains("9", record.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Журнал_БроскиИДействия_СведеныВОдинПотокПоВремени()
    {
        await using var context = await HistoryTestContext.CreateAsync(3);

        await CreateCharacterAsync(context.Characters);

        var roll = await context.Dice.Service.RollAsync(new RollRequest("1d6"));
        Assert.True(roll.IsSuccess, roll.Error);

        var page = await LoadAsync(context);

        Assert.Equal(2, page.Total);
        Assert.Equal(HistoryKind.Roll, page.Records[0].Kind);
        Assert.Equal(HistoryKind.Character, page.Records[1].Kind);
    }

    [Fact]
    public async Task Журнал_ОтборБросков_НеПоказываетДругиеСобытия()
    {
        await using var context = await HistoryTestContext.CreateAsync(3);

        await CreateCharacterAsync(context.Characters);

        var roll = await context.Dice.Service.RollAsync(new RollRequest("1d6"));
        Assert.True(roll.IsSuccess, roll.Error);

        var page = await LoadAsync(context, kind: HistoryKind.Roll);

        Assert.Equal(1, page.Total);
        Assert.All(page.Records, record => Assert.Equal(HistoryKind.Roll, record.Kind));
    }

    // ---------- Ресурсы ----------

    [Fact]
    public async Task Журнал_ИзменениеЗдоровьяНаЛисте_ЗаписаноСоСтарымИНовымЗначением()
    {
        await using var context = await HistoryTestContext.CreateAsync(1);

        await GiveResourceAsync(context.Characters);

        var characterId = await CreateCharacterAsync(context.Characters);

        var sheet = await context.Characters.Sheets.LoadAsync(characterId);
        Assert.True(sheet.IsSuccess, sheet.Error);

        var resource = Assert.Single(sheet.Value.Character.Resources);
        resource.Current = 12;

        var saved = await context.Characters.Sheets.SaveAsync(sheet.Value.Character, new Dictionary<Guid, string?>());
        Assert.True(saved.IsSuccess, saved.Error);

        var record = Assert.Single(
            (await LoadAsync(context, kind: HistoryKind.Resource)).Records);

        Assert.Equal("20", record.OldValue);
        Assert.Equal("12", record.NewValue);
        Assert.Contains("Здоровье", record.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Журнал_СохранениеЛистаБезПравок_НеЗасоряетЖурнал()
    {
        await using var context = await HistoryTestContext.CreateAsync(1);

        await GiveResourceAsync(context.Characters);

        var characterId = await CreateCharacterAsync(context.Characters);

        var sheet = await context.Characters.Sheets.LoadAsync(characterId);
        Assert.True(sheet.IsSuccess, sheet.Error);

        var saved = await context.Characters.Sheets.SaveAsync(sheet.Value.Character, new Dictionary<Guid, string?>());
        Assert.True(saved.IsSuccess, saved.Error);

        Assert.Empty((await LoadAsync(context, kind: HistoryKind.Resource)).Records);
    }

    // ---------- Предметы ----------

    [Fact]
    public async Task Журнал_ИспользованиеПредмета_ЗаписываетДействиеИИзменениеРесурса()
    {
        await using var context = await HistoryTestContext.CreateAsync(1);

        var resourceId = await GiveResourceAsync(context.Characters);
        var characterId = await CreateCharacterAsync(context.Characters);

        var potion = CharacterContent.Item(
            "Зелье лечения",
            "зелье_лечения",
            useCost: ItemUseCost.Unit,
            useEffects: CharacterContent.UseEffect("5", resourceId, "Здоровье"));

        await context.Characters.AddAsync(potion);

        var added = await context.Characters.Inventory.AddAsync(characterId, potion.Id, 1);
        Assert.True(added.IsSuccess, added.Error);

        var inventory = await context.Characters.Inventory.GetAsync(characterId, new InventoryQuery());
        Assert.True(inventory.IsSuccess, inventory.Error);

        var entry = Assert.Single(inventory.Value.Entries);

        // Ресурс тратится до использования, иначе лечение упёрлось бы в максимум.
        var sheet = await context.Characters.Sheets.LoadAsync(characterId);
        Assert.True(sheet.IsSuccess, sheet.Error);
        Assert.Single(sheet.Value.Character.Resources).Current = 10;
        await context.Characters.Sheets.SaveAsync(sheet.Value.Character, new Dictionary<Guid, string?>());

        var used = await context.Characters.Inventory.UseAsync(characterId, entry.InventoryItemId);
        Assert.True(used.IsSuccess, used.Error);

        var records = (await LoadAsync(context, characterId)).Records;

        var use = Assert.Single(records, record => record.Kind == HistoryKind.Item);
        Assert.Contains("Зелье лечения", use.Description, StringComparison.Ordinal);

        var change = Assert.Single(
            records,
            record => record.Kind == HistoryKind.Resource
                && record.Description!.Contains("Зелье лечения", StringComparison.Ordinal));

        Assert.Equal("10", change.OldValue);
        Assert.Equal("15", change.NewValue);
    }

    // ---------- Экипировка ----------

    [Fact]
    public async Task Журнал_НадеваниеИСнятие_ЗаписаныСНазваниямиПредметаИСлота()
    {
        await using var context = await HistoryTestContext.CreateAsync(1);

        var slot = CharacterContent.Slot("Тело", "тело");
        await context.Characters.AddAsync(slot);

        var characterId = await CreateCharacterAsync(context.Characters);

        var armour = CharacterContent.Equipment("Кираса", "кираса", slot.Id);
        await context.Characters.AddAsync(armour);

        var equipped = await context.Characters.Equipment.EquipAsync(characterId, armour.Id);
        Assert.True(equipped.IsSuccess, equipped.Error);

        var slots = await context.Characters.Equipment.GetSlotsAsync(characterId);
        Assert.True(slots.IsSuccess, slots.Error);

        var worn = Assert.Single(Assert.Single(slots.Value).Items);

        var removed = await context.Characters.Equipment.UnequipAsync(characterId, worn.InventoryItemId);
        Assert.True(removed.IsSuccess, removed.Error);

        var records = (await LoadAsync(context, characterId, HistoryKind.Equipment)).Records;

        Assert.Equal(2, records.Count);

        Assert.Contains(
            records,
            record => record.Action == HistoryActions.ItemEquipped
                && record.Description!.Contains("Кираса", StringComparison.Ordinal)
                && record.Description.Contains("Тело", StringComparison.Ordinal));

        Assert.Contains(
            records,
            record => record.Action == HistoryActions.ItemUnequipped
                && record.Description!.Contains("Кираса", StringComparison.Ordinal));
    }

    // ---------- Заклинания ----------

    [Fact]
    public async Task Журнал_ПрименениеЗаклинания_ЗаписываетПрименениеИРасходРесурса()
    {
        await using var context = await HistoryTestContext.CreateAsync(1);

        var resource = CharacterContent.Resource("Мана", "мана", "10", "10");
        await context.Characters.AddAsync(resource);

        var characterId = await CreateCharacterAsync(context.Characters);

        var spell = CharacterContent.Spell(
            "Огненный шар",
            "огненный_шар",
            level: 1,
            resourceId: resource.Id,
            resourceCostFormula: "3");

        await context.Characters.AddAsync(spell);

        var learned = await context.Characters.Spellbook.LearnAsync(characterId, spell.Id);
        Assert.True(learned.IsSuccess, learned.Error);

        var book = await context.Characters.Spellbook.GetAsync(characterId);
        Assert.True(book.IsSuccess, book.Error);

        var entry = book.Value.Levels.SelectMany(level => level.Spells).Single();

        var cast = await context.Characters.Spellbook.CastAsync(characterId, entry.CharacterSpellId);
        Assert.True(cast.IsSuccess, cast.Error);

        var records = (await LoadAsync(context, characterId)).Records;

        Assert.Contains(records, record => record.Kind == HistoryKind.Spell);

        var change = Assert.Single(records, record => record.Kind == HistoryKind.Resource);

        Assert.Equal("10", change.OldValue);
        Assert.Equal("7", change.NewValue);
        Assert.Contains("Огненный шар", change.Description, StringComparison.Ordinal);
    }

    // ---------- Отбор ----------

    [Fact]
    public async Task Журнал_ОтборПоПерсонажу_ПоказываетТолькоЕгоСобытия()
    {
        await using var context = await HistoryTestContext.CreateAsync(1);

        var first = await CreateCharacterAsync(context.Characters, "Аргус");
        await CreateCharacterAsync(context.Characters, "Мира");

        var page = await LoadAsync(context, first);

        var record = Assert.Single(page.Records);
        Assert.Equal("Аргус", record.CharacterName);
    }

    [Fact]
    public async Task Журнал_ОтборПоНесколькимПерсонажам_ПоказываетСобытияПартии()
    {
        await using var context = await HistoryTestContext.CreateAsync(1);

        var first = await CreateCharacterAsync(context.Characters, "Аргус");
        var second = await CreateCharacterAsync(context.Characters, "Мира");
        await CreateCharacterAsync(context.Characters, "Посторонний");

        // Общий журнал режима мастера отбирает события сразу всей партии.
        var result = await context.Service.GetAsync(new HistoryQuery([first, second]));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Value.Records.Count);
        Assert.DoesNotContain(result.Value.Records, record => record.CharacterName == "Посторонний");
    }

    [Fact]
    public async Task Журнал_Поиск_ОтбираетПоОписанию()
    {
        await using var context = await HistoryTestContext.CreateAsync(1);

        await CreateCharacterAsync(context.Characters, "Аргус");
        await CreateCharacterAsync(context.Characters, "Мира");

        var page = await LoadAsync(context, search: "Мира");

        var record = Assert.Single(page.Records);
        Assert.Equal("Мира", record.CharacterName);
    }

    [Fact]
    public async Task Журнал_Предел_ОграничиваетПоказНоНеОбщееЧисло()
    {
        await using var context = await HistoryTestContext.CreateAsync(1);

        for (var index = 0; index < 4; index++)
        {
            var roll = await context.Dice.Service.RollAsync(new RollRequest("1d6"));
            Assert.True(roll.IsSuccess, roll.Error);
        }

        var result = await context.Service.GetAsync(new HistoryQuery(Limit: 2));
        Assert.True(result.IsSuccess, result.Error);

        Assert.Equal(2, result.Value.Records.Count);
        Assert.Equal(4, result.Value.Total);
        Assert.True(result.Value.HasMore);
    }

    [Fact]
    public async Task Персонажи_ВЖурнале_ПеречисленыПоИмени()
    {
        await using var context = await HistoryTestContext.CreateAsync(1);

        await CreateCharacterAsync(context.Characters, "Мира");
        await CreateCharacterAsync(context.Characters, "Аргус");

        var result = await context.Service.GetCharactersAsync();
        Assert.True(result.IsSuccess, result.Error);

        Assert.Equal(["Аргус", "Мира"], result.Value.Select(character => character.Name));
    }

    // ---------- Очистка ----------

    [Fact]
    public async Task Очистка_УбираетИДействияИБроски()
    {
        await using var context = await HistoryTestContext.CreateAsync(1);

        await CreateCharacterAsync(context.Characters);

        var roll = await context.Dice.Service.RollAsync(new RollRequest("1d6"));
        Assert.True(roll.IsSuccess, roll.Error);

        var cleared = await context.Service.ClearAsync(null);
        Assert.True(cleared.IsSuccess, cleared.Error);
        Assert.Equal(2, cleared.Value);

        Assert.Empty((await LoadAsync(context)).Records);
    }

    [Fact]
    public async Task Очистка_СохраняетЛюбимыеБроски()
    {
        await using var context = await HistoryTestContext.CreateAsync(1);

        var roll = await context.Dice.Service.RollAsync(new RollRequest("1d6", Title: "Атака"));
        Assert.True(roll.IsSuccess, roll.Error);

        var favorite = await context.Dice.Service.SetFavoriteAsync(roll.Value.Id, true);
        Assert.True(favorite.IsSuccess, favorite.Error);

        var cleared = await context.Service.ClearAsync(null);
        Assert.True(cleared.IsSuccess, cleared.Error);

        var record = Assert.Single((await LoadAsync(context)).Records);
        Assert.Equal(HistoryKind.Roll, record.Kind);
    }

    [Fact]
    public async Task Очистка_ОдногоПерсонажа_НеТрогаетОстальных()
    {
        await using var context = await HistoryTestContext.CreateAsync(1);

        var first = await CreateCharacterAsync(context.Characters, "Аргус");
        await CreateCharacterAsync(context.Characters, "Мира");

        var cleared = await context.Service.ClearAsync(first);
        Assert.True(cleared.IsSuccess, cleared.Error);

        var record = Assert.Single((await LoadAsync(context)).Records);
        Assert.Equal("Мира", record.CharacterName);
    }
}
