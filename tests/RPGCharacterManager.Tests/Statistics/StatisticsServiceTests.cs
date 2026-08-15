using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Core.Abstractions.Dice;
using RPGCharacterManager.Core.Abstractions.Statistics;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Statistics;
using RPGCharacterManager.Tests.Characters;
using RPGCharacterManager.Tests.History;

namespace RPGCharacterManager.Tests.Statistics;

/// <summary>
/// Проверка статистики: подсчёт бросков, критов, урона, применённых заклинаний
/// и изменений ресурсов по записям журнала.
/// </summary>
public sealed class StatisticsServiceTests
{
    private static async Task<StatisticsReport> ReportAsync(
        CharacterTestContext context,
        Guid? characterId = null,
        int? days = null)
    {
        var service = new StatisticsService(
            context.ContextFactory, NullLogger<StatisticsService>.Instance);

        var result = await service.GetAsync(new StatisticsQuery(characterId, days));
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static Task<StatisticsReport> ReportAsync(
        HistoryTestContext context,
        Guid? characterId = null,
        int? days = null) =>
        ReportAsync(context.Characters, characterId, days);

    private static async Task<Guid> CreateCharacterAsync(
        CharacterTestContext context,
        string name = "Аргус")
    {
        var draft = new CharacterDraft { Level = 3 };
        draft.Character.Name = name;

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static async Task<Guid> GiveWeaponAsync(
        CharacterTestContext context,
        Guid characterId,
        Item weapon)
    {
        await context.AddAsync(weapon);

        var added = await context.Weapons.AddAsync(characterId, weapon.Id);
        Assert.True(added.IsSuccess, added.Error);

        return added.Value;
    }

    private static async Task<Guid> LearnAsync(
        CharacterTestContext context,
        Guid characterId,
        Spell spell)
    {
        await context.AddAsync(spell);

        var learned = await context.Spellbook.LearnAsync(characterId, spell.Id);
        Assert.True(learned.IsSuccess, learned.Error);

        var state = await context.Spellbook.GetAsync(characterId);
        Assert.True(state.IsSuccess, state.Error);

        return state.Value.Levels
            .SelectMany(level => level.Spells)
            .Single(entry => entry.SpellId == spell.Id)
            .CharacterSpellId;
    }

    // ---------- Броски ----------

    [Fact]
    public async Task Броски_СчитаютсяПоЖурналуБросков()
    {
        await using var context = await HistoryTestContext.CreateAsync(4);

        Assert.True((await context.Dice.Service.RollAsync(new RollRequest("1d6"))).IsSuccess);
        Assert.True((await context.Dice.Service.RollAsync(new RollRequest("1d6 + 2"))).IsSuccess);

        var rolls = (await ReportAsync(context)).Rolls;

        Assert.Equal(2, rolls.Count);
        Assert.Equal(5, rolls.Average);
        Assert.Equal(6, rolls.Best);
        Assert.Equal(4, rolls.Worst);
    }

    [Fact]
    public async Task Кости_СчитаютМаксимумыИМинимумы()
    {
        // Кость выдаёт значения по кругу: шесть, потом один.
        await using var context = await HistoryTestContext.CreateAsync(6, 1);

        Assert.True((await context.Dice.Service.RollAsync(new RollRequest("1d6"))).IsSuccess);
        Assert.True((await context.Dice.Service.RollAsync(new RollRequest("1d6"))).IsSuccess);

        var die = Assert.Single((await ReportAsync(context)).Rolls.Dice);

        Assert.Equal(6, die.Sides);
        Assert.Equal("d6", die.Notation);
        Assert.Equal(2, die.Casts);
        Assert.Equal(1, die.Maximums);
        Assert.Equal(1, die.Minimums);

        // Среднее по двум крайним граням совпало с ожидаемым — так и должно быть.
        Assert.Equal(3.5, die.Average);
        Assert.Equal(3.5, die.Expected);
    }

    [Fact]
    public async Task Броски_РазныеКости_СчитаютсяПоОтдельности()
    {
        await using var context = await HistoryTestContext.CreateAsync(3);

        Assert.True((await context.Dice.Service.RollAsync(new RollRequest("2d6"))).IsSuccess);
        Assert.True((await context.Dice.Service.RollAsync(new RollRequest("1d20"))).IsSuccess);

        var dice = (await ReportAsync(context)).Rolls.Dice;

        Assert.Equal(2, dice.Count);

        // Первой идёт кость, которую бросали чаще.
        Assert.Equal(6, dice[0].Sides);
        Assert.Equal(2, dice[0].Casts);
        Assert.Equal(20, dice[1].Sides);
        Assert.Equal(1, dice[1].Casts);
    }

    [Fact]
    public async Task Бросок_СПреимуществом_УчитываетТолькоПринятуюПопытку()
    {
        await using var context = await HistoryTestContext.CreateAsync(2, 5);

        var roll = await context.Dice.Service.RollAsync(new RollRequest("1d6", RollMode.Advantage));
        Assert.True(roll.IsSuccess, roll.Error);

        var rolls = (await ReportAsync(context)).Rolls;

        Assert.Equal(1, rolls.Count);
        Assert.Equal(1, rolls.Advantage);
        Assert.Equal(0, rolls.Disadvantage);

        // Выражение вычислялось дважды, но в игре произошёл один бросок.
        var die = Assert.Single(rolls.Dice);

        Assert.Equal(1, die.Casts);
        Assert.Equal(5, die.Average);
    }

    // ---------- Криты и урон ----------

    [Fact]
    public async Task Атаки_КритическоеПопадание_СчитаетсяОтдельно()
    {
        // Кость попадания всегда показывает двадцать: проверяется подсчёт, а не удача.
        await using var context = await CharacterTestContext.CreateWithDiceAsync(20);

        var characterId = await CreateCharacterAsync(context);
        var weaponId = await GiveWeaponAsync(
            context,
            characterId,
            CharacterContent.Weapon(
                "Меч",
                "меч",
                damageFormula: "10",
                attackDiceFormula: "1d20",
                attackFormula: "4",
                criticalThreshold: 20,
                criticalFormula: "урон * 2"));

        var attack = await context.Weapons.AttackAsync(characterId, weaponId);
        Assert.True(attack.IsSuccess, attack.Error);
        Assert.True(attack.Value.IsCritical);

        var attacks = (await ReportAsync(context)).Attacks;

        Assert.Equal(1, attacks.Attacks);
        Assert.Equal(1, attacks.Criticals);
        Assert.Equal(1, attacks.CriticalShare);
        Assert.Equal(20, attacks.Damage);

        var weapon = Assert.Single(attacks.Weapons);

        Assert.Equal("Меч", weapon.Name);
        Assert.Equal(1, weapon.Criticals);
    }

    [Fact]
    public async Task Атаки_УронСчитаетсяПоКаждомуОружию()
    {
        await using var context = await CharacterTestContext.CreateWithDiceAsync(20);

        var characterId = await CreateCharacterAsync(context);

        // Порог крита у топора достигнут, у дубины — нет: приложение считает
        // критом ровно то, что задано в оружии.
        var axeId = await GiveWeaponAsync(
            context,
            characterId,
            CharacterContent.Weapon(
                "Топор",
                "топор",
                damageFormula: "8",
                attackDiceFormula: "1d20",
                attackFormula: "0",
                criticalThreshold: 20,
                criticalFormula: "урон * 2"));

        var clubId = await GiveWeaponAsync(
            context,
            characterId,
            CharacterContent.Weapon(
                "Дубина",
                "дубина",
                damageFormula: "6",
                attackDiceFormula: "1d20",
                attackFormula: "0",
                criticalThreshold: 21,
                criticalFormula: "урон * 2"));

        Assert.True((await context.Weapons.AttackAsync(characterId, axeId)).IsSuccess);
        Assert.True((await context.Weapons.AttackAsync(characterId, clubId)).IsSuccess);

        var attacks = (await ReportAsync(context)).Attacks;

        Assert.Equal(2, attacks.Attacks);
        Assert.Equal(1, attacks.Criticals);
        Assert.Equal(0.5, attacks.CriticalShare);
        Assert.Equal(22, attacks.Damage);
        Assert.Equal(16, attacks.Best);

        var axe = attacks.Weapons.Single(weapon => weapon.Name == "Топор");
        var club = attacks.Weapons.Single(weapon => weapon.Name == "Дубина");

        Assert.Equal(16, axe.Damage);
        Assert.Equal(1, axe.Criticals);
        Assert.Equal(6, club.Damage);
        Assert.Equal(0, club.Criticals);
    }

    // ---------- Заклинания ----------

    [Fact]
    public async Task Заклинание_НаРазныхУровнях_ОстаётсяОднимЗаклинанием()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);
        var spellId = await LearnAsync(
            context,
            characterId,
            CharacterContent.Spell("Огненный шар", "огненный_шар", level: 1, formula: "1d6"));

        Assert.True((await context.Spellbook.CastAsync(characterId, spellId, 1)).IsSuccess);
        Assert.True((await context.Spellbook.CastAsync(characterId, spellId, 3)).IsSuccess);

        // Описание у двух применений разное — «уровень 1» и «уровень 3», —
        // но заклинание одно, и считается оно как одно.
        var spell = Assert.Single((await ReportAsync(context)).Spells);

        Assert.Equal("Огненный шар", spell.Name);
        Assert.Equal(2, spell.Casts);
    }

    // ---------- Ресурсы ----------

    [Fact]
    public async Task Ресурсы_РасходИВосстановление_РазделеныЗнакомИзменения()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await WriteResourceChangesAsync(context, characterId, ("Здоровье", 20, 12), ("Здоровье", 12, 18));

        var resource = Assert.Single((await ReportAsync(context)).Resources);

        Assert.Equal("Здоровье", resource.Name);
        Assert.Equal(2, resource.Changes);
        Assert.Equal(8, resource.Spent);
        Assert.Equal(6, resource.Restored);
        Assert.Equal(-2, resource.Balance);
    }

    // ---------- Отбор ----------

    [Fact]
    public async Task Отбор_ПоПерсонажу_НеСчитаетЧужиеСобытия()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var first = await CreateCharacterAsync(context, "Аргус");
        var second = await CreateCharacterAsync(context, "Люциус");

        await WriteResourceChangesAsync(context, first, ("Мана", 10, 4));
        await WriteResourceChangesAsync(context, second, ("Мана", 10, 9));

        var resource = Assert.Single((await ReportAsync(context, first)).Resources);

        Assert.Equal(6, resource.Spent);
        Assert.Equal(1, resource.Changes);
    }

    [Fact]
    public async Task Отбор_ЗаПериод_НеСчитаетСобытияСтаршеЕго()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await WriteResourceChangesAsync(context, characterId, ("Заряды", 5, 3));
        await AgeHistoryAsync(context, TimeSpan.FromDays(10));
        await WriteResourceChangesAsync(context, characterId, ("Заряды", 3, 2));

        var recent = Assert.Single((await ReportAsync(context, days: 7)).Resources);

        Assert.Equal(1, recent.Changes);
        Assert.Equal(1, recent.Spent);

        var everything = Assert.Single((await ReportAsync(context)).Resources);

        Assert.Equal(2, everything.Changes);
        Assert.Equal(3, everything.Spent);
    }

    [Fact]
    public async Task Сводка_БезСобытий_Пуста()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var report = await ReportAsync(context);

        Assert.True(report.IsEmpty);
        Assert.True(report.Rolls.IsEmpty);
        Assert.True(report.Attacks.IsEmpty);
        Assert.Empty(report.Spells);
        Assert.Empty(report.Resources);
    }

    [Fact]
    public async Task Событие_БезНазванияОбъекта_ПопадаетВОтдельнуюСтроку()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        await using (var database = await context.CreateContextAsync())
        {
            // Так выглядят записи, сделанные до появления названия объекта.
            database.History.Add(new HistoryEntry
            {
                CharacterId = characterId,
                Action = HistoryActions.ResourceChanged,
                Amount = -3,
            });

            await database.SaveChangesAsync();
        }

        var resource = Assert.Single((await ReportAsync(context)).Resources);

        Assert.Equal(StatisticsService.UnnamedSubject, resource.Name);
        Assert.Equal(3, resource.Spent);
    }

    /// <summary>
    /// Записывает изменения ресурсов той же заготовкой, что и подсистемы приложения.
    /// </summary>
    /// <param name="context">Окружение теста.</param>
    /// <param name="characterId">Персонаж.</param>
    /// <param name="changes">Изменения: название ресурса, значение до и после.</param>
    /// <returns>Задача, завершающаяся после записи.</returns>
    private static async Task WriteResourceChangesAsync(
        CharacterTestContext context,
        Guid characterId,
        params (string Name, double Before, double After)[] changes)
    {
        await using var database = await context.CreateContextAsync();

        foreach (var change in changes)
        {
            database.History.Add(HistoryEntries.ResourceChanged(
                characterId, change.Name, change.Before, change.After));
        }

        await database.SaveChangesAsync();
    }

    /// <summary>
    /// Отодвигает все записи журнала в прошлое.
    ///
    /// Запросом, а не через контекст: приложение намеренно запрещает менять момент
    /// создания записи, и обходить этот запрет обычным способом нечем.
    /// </summary>
    /// <param name="context">Окружение теста.</param>
    /// <param name="age">На сколько отодвинуть.</param>
    /// <returns>Задача, завершающаяся после изменения.</returns>
    private static async Task AgeHistoryAsync(CharacterTestContext context, TimeSpan age)
    {
        await using var database = await context.CreateContextAsync();

        // Моменты времени хранятся тиками, поэтому вычитается их количество.
        await database.Database.ExecuteSqlRawAsync(
            "UPDATE History SET CreatedAt = CreatedAt - {0}", age.Ticks);
    }
}
