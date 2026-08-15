using RPGCharacterManager.Core.Abstractions.Items;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Tests.Characters;
using RPGCharacterManager.Tests.Rules;

namespace RPGCharacterManager.Tests.Items;

/// <summary>
/// Проверка подсистемы оружия: формул попадания и урона, масштабирования,
/// критических попаданий, боеприпасов, магазинов и перезарядки.
///
/// Кубики в тестах предсказуемы: источник случайных значений всегда возвращает
/// заданное число, поэтому проверяется вычисление, а не удача.
/// </summary>
public sealed class WeaponServiceTests
{
    private const string HalfOfValue = "ОкруглитьВниз((значение - 10) / 2)";

    private static async Task<Guid> CreateCharacterAsync(
        CharacterTestContext context,
        string name = "Стрелок",
        int level = 1)
    {
        var draft = new CharacterDraft { Level = level };
        draft.Character.Name = name;

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    private static async Task<Guid> GiveAsync(CharacterTestContext context, Guid characterId, Item weapon)
    {
        await context.AddAsync(weapon);

        var added = await context.Weapons.AddAsync(characterId, weapon.Id);
        Assert.True(added.IsSuccess, added.Error);

        return added.Value;
    }

    private static async Task<CharacterWeapon> LoadWeaponAsync(
        CharacterTestContext context,
        Guid characterId)
    {
        var result = await context.Weapons.GetWeaponsAsync(characterId);
        Assert.True(result.IsSuccess, result.Error);

        return Assert.Single(result.Value);
    }

    // ---------- Формулы урона и масштабирование ----------

    [Fact]
    public async Task Урон_МасштабируетсяВыбраннойХарактеристикой()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", 16, HalfOfValue);
        await context.AddAsync(strength);

        var characterId = await CreateCharacterAsync(context);

        // Формула не называет характеристику по имени: она обращается к переменной
        // «характеристика», значение которой задаёт масштабирование оружия.
        var sword = CharacterContent.Weapon(
            "Катана",
            "катана",
            damageFormula: "1d8 + характеристика",
            scalingAttributeId: strength.Id);

        await GiveAsync(context, characterId, sword);

        var weapon = await LoadWeaponAsync(context, characterId);

        Assert.Equal("Сила", weapon.ScalingAttributeName);

        // Модификатор Силы 16 равен 3, поэтому урон лежит в пределах от 1 + 3 до 8 + 3.
        Assert.Equal(4, weapon.Damage!.Value.Minimum);
        Assert.Equal(11, weapon.Damage!.Value.Maximum);
    }

    [Fact]
    public async Task БонусПопадания_УчитываетВладениеОружием()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var dexterity = CharacterContent.Attribute("Ловкость", "ловкость", 14, HalfOfValue);
        await context.AddAsync(dexterity);

        var shooting = CharacterContent.Skill("Стрельба", "стрельба", dexterity.Id);
        await context.AddAsync(shooting);

        var characterId = await CreateCharacterAsync(context);

        var character = await context.LoadCharacterAsync(characterId);
        character.Skills.Add(new CharacterSkill
        {
            CharacterId = characterId,
            SkillId = shooting.Id,
            ProficiencyLevel = 3,
        });

        var saved = await context.Sheets.SaveAsync(character, new Dictionary<Guid, string?>());
        Assert.True(saved.IsSuccess, saved.Error);

        var rifle = CharacterContent.Weapon(
            "Винтовка",
            "винтовка",
            damageFormula: "2d6",
            attackDiceFormula: "1d20",
            attackFormula: "характеристика + владение",
            scalingAttributeId: dexterity.Id,
            proficiencySkillId: shooting.Id);

        await GiveAsync(context, characterId, rifle);

        var weapon = await LoadWeaponAsync(context, characterId);

        Assert.Equal("Стрельба", weapon.ProficiencySkillName);
        Assert.Equal(3, weapon.ProficiencyLevel);

        // Модификатор Ловкости 14 равен 2, уровень владения — 3.
        Assert.Equal(5, weapon.AttackBonus);
    }

    [Fact]
    public async Task Урон_ОшибкаФормулы_ПопадаетВЗамечанияИНеЛомаетКарточку()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);

        var broken = CharacterContent.Weapon("Дубина", "дубина", damageFormula: "1d6 + мощь");
        await GiveAsync(context, characterId, broken);

        var weapon = await LoadWeaponAsync(context, characterId);

        Assert.Null(weapon.Damage);
        Assert.Contains(weapon.Issues, issue => issue.Contains("мощь", StringComparison.CurrentCulture));
    }

    // ---------- Атака ----------

    [Fact]
    public async Task Атака_БезКостиПопадания_ВычисляетТолькоУрон()
    {
        await using var context = await CharacterTestContext.CreateWithDiceAsync(4);

        var characterId = await CreateCharacterAsync(context);
        var inventoryItemId = await GiveAsync(
            context,
            characterId,
            CharacterContent.Weapon("Кинжал", "кинжал", damageFormula: "1d6 + 1"));

        var result = await context.Weapons.AttackAsync(characterId, inventoryItemId);
        Assert.True(result.IsSuccess, result.Error);

        Assert.Null(result.Value.Roll);
        Assert.Null(result.Value.AttackTotal);
        Assert.False(result.Value.IsCritical);
        Assert.Equal(5, result.Value.Damage);
    }

    [Fact]
    public async Task Атака_КостьНижеПорога_ОстаётсяОбычной()
    {
        await using var context = await CharacterTestContext.CreateWithDiceAsync(15);

        var characterId = await CreateCharacterAsync(context);
        var inventoryItemId = await GiveAsync(
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

        var result = await context.Weapons.AttackAsync(characterId, inventoryItemId);
        Assert.True(result.IsSuccess, result.Error);

        Assert.Equal(15, result.Value.Roll);
        Assert.Equal(19, result.Value.AttackTotal);
        Assert.False(result.Value.IsCritical);
        Assert.Equal(10, result.Value.Damage);
    }

    [Fact]
    public async Task Атака_КостьДостиглаПорога_ПрименяетФормулуКритическогоУрона()
    {
        await using var context = await CharacterTestContext.CreateWithDiceAsync(20);

        var characterId = await CreateCharacterAsync(context);
        var inventoryItemId = await GiveAsync(
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

        var result = await context.Weapons.AttackAsync(characterId, inventoryItemId);
        Assert.True(result.IsSuccess, result.Error);

        Assert.True(result.Value.IsCritical);
        Assert.Equal(20, result.Value.Damage);
    }

    [Fact]
    public async Task Атака_БонусНеДелаетПопаданиеКритическим()
    {
        await using var context = await CharacterTestContext.CreateWithDiceAsync(18);

        var characterId = await CreateCharacterAsync(context);

        // Итог броска равен 28 и превышает порог, но критическое попадание
        // определяется по выпавшей кости, а не по сумме с бонусами.
        var inventoryItemId = await GiveAsync(
            context,
            characterId,
            CharacterContent.Weapon(
                "Молот",
                "молот",
                damageFormula: "6",
                attackDiceFormula: "1d20",
                attackFormula: "10",
                criticalThreshold: 20,
                criticalFormula: "урон * 3"));

        var result = await context.Weapons.AttackAsync(characterId, inventoryItemId);
        Assert.True(result.IsSuccess, result.Error);

        Assert.Equal(28, result.Value.AttackTotal);
        Assert.False(result.Value.IsCritical);
        Assert.Equal(6, result.Value.Damage);
    }

    [Fact]
    public async Task Атака_ТребованияНеВыполнены_Отклоняется()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", 8);
        await context.AddAsync(strength);

        var characterId = await CreateCharacterAsync(context);
        var inventoryItemId = await GiveAsync(
            context,
            characterId,
            CharacterContent.Weapon(
                "Двуручный молот",
                "двуручный_молот",
                damageFormula: "2d6",
                requirements: "сила >= 16"));

        var weapon = await LoadWeaponAsync(context, characterId);
        Assert.False(weapon.IsAvailable);

        var result = await context.Weapons.AttackAsync(characterId, inventoryItemId);

        Assert.True(result.IsFailure);
        Assert.Contains("Двуручный молот", result.Error, StringComparison.CurrentCulture);
    }

    // ---------- Правила боя ----------

    [Fact]
    public async Task Атака_ПравилоКритическогоПопадания_ИзменяетУрон()
    {
        await using var context = await CharacterTestContext.CreateWithDiceAsync(20);

        var characterId = await CreateCharacterAsync(context);

        // Уникальная механика оружия описана правилом, а не кодом приложения:
        // критическое попадание добавляет пять единиц урона.
        var rule = new RuleDefinition
        {
            Name = "Сокрушительный удар",
            Trigger = RuleTriggers.CombatCriticalHit,
            Category = RuleCategories.Combat,
        };

        rule.Actions.Add(RuleTestFactory.Action(
            "изменить_значение",
            ("параметр", "урон"),
            ("значение", "5")));

        await context.Rules.SaveAsync(rule);

        var inventoryItemId = await GiveAsync(
            context,
            characterId,
            CharacterContent.Weapon(
                "Секира",
                "секира",
                damageFormula: "8",
                attackDiceFormula: "1d20",
                criticalThreshold: 20,
                properties: "тяжёлое, острое"));

        var result = await context.Weapons.AttackAsync(characterId, inventoryItemId);
        Assert.True(result.IsSuccess, result.Error);

        Assert.Equal(13, result.Value.Damage);
        Assert.Contains("Сокрушительный удар", result.Value.AppliedRules);
    }

    [Fact]
    public async Task СвойстваОружия_ДоступныПравиламКакПризнаки()
    {
        await using var context = await CharacterTestContext.CreateWithDiceAsync(20);

        var characterId = await CreateCharacterAsync(context);

        // Свойство оружия становится признаком объекта правил, поэтому условие
        // правила проверяет его так же, как любой другой признак персонажа.
        var rule = new RuleDefinition
        {
            Name = "Пробивающий удар",
            Trigger = RuleTriggers.CombatHit,
            Category = RuleCategories.Combat,
            Condition = new RuleComparison
            {
                Left = "признак",
                Operator = RuleComparisonOperator.Has,
                Right = "пробивающее",
            },
        };

        rule.Actions.Add(RuleTestFactory.Action(
            "изменить_значение",
            ("параметр", "урон"),
            ("значение", "2")));

        await context.Rules.SaveAsync(rule);

        var withProperty = await GiveAsync(
            context,
            characterId,
            CharacterContent.Weapon(
                "Копьё",
                "копьё",
                damageFormula: "6",
                properties: "пробивающее"));

        var attacked = await context.Weapons.AttackAsync(characterId, withProperty);
        Assert.True(attacked.IsSuccess, attacked.Error);

        Assert.Equal(8, attacked.Value.Damage);
    }

    // ---------- Боеприпасы, магазин и перезарядка ----------

    [Fact]
    public async Task Атака_БезМагазина_РасходуетБоеприпасыИзЗапаса()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var arrows = CharacterContent.Item("Стрелы", "стрелы");
        await context.AddAsync(arrows);

        var characterId = await CreateCharacterAsync(context);
        var inventoryItemId = await GiveAsync(
            context,
            characterId,
            CharacterContent.Weapon(
                "Лук",
                "лук",
                damageFormula: "1d8",
                ammunitionItemId: arrows.Id));

        var reserve = await context.Weapons.SetAmmunitionReserveAsync(characterId, inventoryItemId, 3);
        Assert.True(reserve.IsSuccess, reserve.Error);

        var result = await context.Weapons.AttackAsync(characterId, inventoryItemId);
        Assert.True(result.IsSuccess, result.Error);

        Assert.Equal(1, result.Value.AmmunitionSpent);
        Assert.Equal(2, result.Value.AmmunitionLeft);

        var weapon = await LoadWeaponAsync(context, characterId);
        Assert.Equal(2, weapon.Ammunition!.Reserve);
    }

    [Fact]
    public async Task Атака_ЗапасИсчерпан_Отклоняется()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var arrows = CharacterContent.Item("Стрелы", "стрелы");
        await context.AddAsync(arrows);

        var characterId = await CreateCharacterAsync(context);
        var inventoryItemId = await GiveAsync(
            context,
            characterId,
            CharacterContent.Weapon(
                "Лук",
                "лук",
                damageFormula: "1d8",
                ammunitionItemId: arrows.Id));

        var result = await context.Weapons.AttackAsync(characterId, inventoryItemId);

        Assert.True(result.IsFailure);
        Assert.Contains("боеприпас", result.Error, StringComparison.CurrentCulture);
    }

    [Fact]
    public async Task Атака_ПустойМагазин_ТребуетПерезарядки()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var rounds = CharacterContent.Item("Патроны 7,62", "патроны_762");
        await context.AddAsync(rounds);

        var characterId = await CreateCharacterAsync(context);
        var inventoryItemId = await GiveAsync(
            context,
            characterId,
            CharacterContent.Weapon(
                "Винтовка",
                "винтовка",
                damageFormula: "2d6",
                ammunitionItemId: rounds.Id,
                magazineSize: 30));

        await context.Weapons.SetAmmunitionReserveAsync(characterId, inventoryItemId, 60);

        var result = await context.Weapons.AttackAsync(characterId, inventoryItemId);

        Assert.True(result.IsFailure);
        Assert.Contains("перезарядка", result.Error, StringComparison.CurrentCulture);
    }

    [Fact]
    public async Task Перезарядка_ПереноситБоеприпасыИзЗапасаВМагазин()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var rounds = CharacterContent.Item("Патроны 7,62", "патроны_762");
        await context.AddAsync(rounds);

        var characterId = await CreateCharacterAsync(context);
        var inventoryItemId = await GiveAsync(
            context,
            characterId,
            CharacterContent.Weapon(
                "Винтовка",
                "винтовка",
                damageFormula: "2d6",
                ammunitionItemId: rounds.Id,
                ammunitionPerShot: 3,
                magazineSize: 30));

        await context.Weapons.SetAmmunitionReserveAsync(characterId, inventoryItemId, 40);

        var reloaded = await context.Weapons.ReloadAsync(characterId, inventoryItemId);
        Assert.True(reloaded.IsSuccess, reloaded.Error);

        Assert.Equal(30, reloaded.Value.Loaded);
        Assert.Equal(10, reloaded.Value.Reserve);

        var attacked = await context.Weapons.AttackAsync(characterId, inventoryItemId);
        Assert.True(attacked.IsSuccess, attacked.Error);

        Assert.Equal(3, attacked.Value.AmmunitionSpent);
        Assert.Equal(27, attacked.Value.AmmunitionLeft);

        // Запас не тронут: очередь стреляет тем, что уже заряжено.
        var weapon = await LoadWeaponAsync(context, characterId);
        Assert.Equal(10, weapon.Ammunition!.Reserve);
        Assert.Equal(27, weapon.Ammunition!.Loaded);
    }

    [Fact]
    public async Task Перезарядка_НетБоеприпасов_СообщаетОбЭтом()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var rounds = CharacterContent.Item("Патроны 7,62", "патроны_762");
        await context.AddAsync(rounds);

        var characterId = await CreateCharacterAsync(context);
        var inventoryItemId = await GiveAsync(
            context,
            characterId,
            CharacterContent.Weapon(
                "Винтовка",
                "винтовка",
                damageFormula: "2d6",
                ammunitionItemId: rounds.Id,
                magazineSize: 30));

        var result = await context.Weapons.ReloadAsync(characterId, inventoryItemId);

        Assert.True(result.IsFailure);
        Assert.Contains("Патроны 7,62", result.Error, StringComparison.CurrentCulture);
    }

    [Fact]
    public async Task Перезарядка_ОружиюБезМагазина_НеТребуется()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);
        var inventoryItemId = await GiveAsync(
            context,
            characterId,
            CharacterContent.Weapon("Меч", "меч", damageFormula: "1d8"));

        var result = await context.Weapons.ReloadAsync(characterId, inventoryItemId);

        Assert.True(result.IsFailure);
        Assert.Contains("магазина", result.Error, StringComparison.CurrentCulture);
    }

    // ---------- Выдача оружия и журнал ----------

    [Fact]
    public async Task ДоступноеОружие_ОтбираетсяПоИгровойСистемеИТребованиям()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", 10);
        await context.AddAsync(strength);

        var characterId = await CreateCharacterAsync(context);

        var simple = CharacterContent.Weapon("Дубина", "дубина", damageFormula: "1d4");
        var heavy = CharacterContent.Weapon(
            "Двуручный молот",
            "двуручный_молот",
            damageFormula: "2d6",
            requirements: "сила >= 16");

        await context.AddAsync(simple, heavy);

        var available = await context.Weapons.GetAvailableWeaponsAsync(characterId, null, false);
        Assert.Equal("Дубина", Assert.Single(available.Options).Name);

        var all = await context.Weapons.GetAvailableWeaponsAsync(characterId, null, true);
        Assert.Equal(2, all.Options.Count);
        Assert.Contains(all.Options, option => !option.IsAvailable);
    }

    [Fact]
    public async Task Атака_ЗаписываетсяВЖурналБросковИИзменений()
    {
        await using var context = await CharacterTestContext.CreateWithDiceAsync(5);

        var characterId = await CreateCharacterAsync(context);
        var inventoryItemId = await GiveAsync(
            context,
            characterId,
            CharacterContent.Weapon("Кинжал", "кинжал", damageFormula: "1d6"));

        var result = await context.Weapons.AttackAsync(characterId, inventoryItemId);
        Assert.True(result.IsSuccess, result.Error);

        var history = await context.LoadHistoryAsync(characterId);

        Assert.Contains(history, entry => entry.Action == "атака_оружием");
    }

    [Fact]
    public async Task УбратьОружие_УдаляетЕгоУПерсонажа()
    {
        await using var context = await CharacterTestContext.CreateAsync();

        var characterId = await CreateCharacterAsync(context);
        var inventoryItemId = await GiveAsync(
            context,
            characterId,
            CharacterContent.Weapon("Меч", "меч", damageFormula: "1d8"));

        var removed = await context.Weapons.RemoveAsync(characterId, inventoryItemId);
        Assert.True(removed.IsSuccess, removed.Error);

        var weapons = await context.Weapons.GetWeaponsAsync(characterId);
        Assert.True(weapons.IsSuccess, weapons.Error);
        Assert.Empty(weapons.Value);
    }
}
