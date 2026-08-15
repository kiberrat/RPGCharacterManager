namespace RPGCharacterManager.Core.Abstractions.Rules;

/// <summary>
/// Ключи встроенных событий приложения.
///
/// Ключи собраны в ядре, поскольку их используют одновременно поставщик событий
/// редактора правил и подсистемы, вызывающие правила: мастер создания персонажа,
/// развитие персонажа, бой, магия, предметы и отдых.
/// </summary>
public static class RuleTriggers
{
    /// <summary>Создание нового персонажа.</summary>
    public const string CharacterCreated = "персонаж.создание";

    /// <summary>Повышение уровня персонажа.</summary>
    public const string CharacterLevelUp = "персонаж.повышение_уровня";

    /// <summary>Изменение значения характеристики.</summary>
    public const string CharacterAttributeChanged = "персонаж.изменение_характеристики";

    /// <summary>Полный пересчёт параметров персонажа.</summary>
    public const string CharacterRecalculated = "персонаж.пересчёт";

    /// <summary>Начало боевого столкновения.</summary>
    public const string CombatStarted = "бой.начало";

    /// <summary>Завершение хода персонажа.</summary>
    public const string CombatTurnEnded = "бой.конец_хода";

    /// <summary>Успешная атака.</summary>
    public const string CombatHit = "бой.попадание";

    /// <summary>Критическое попадание.</summary>
    public const string CombatCriticalHit = "бой.критическое_попадание";

    /// <summary>Получение урона.</summary>
    public const string CombatDamageTaken = "бой.получение_урона";

    /// <summary>Гибель персонажа.</summary>
    public const string CombatDeath = "бой.смерть";

    /// <summary>Применение заклинания или способности.</summary>
    public const string MagicSpellCast = "магия.применение_заклинания";

    /// <summary>Истечение срока действия эффекта.</summary>
    public const string MagicEffectEnded = "магия.окончание_эффекта";

    /// <summary>Прекращение концентрации на заклинании.</summary>
    public const string MagicConcentrationLost = "магия.потеря_концентрации";

    /// <summary>Добавление предмета в инвентарь.</summary>
    public const string ItemObtained = "предметы.получение";

    /// <summary>Надевание предмета в слот экипировки.</summary>
    public const string ItemEquipped = "предметы.экипировка";

    /// <summary>Снятие предмета со слота экипировки.</summary>
    public const string ItemUnequipped = "предметы.снятие";

    /// <summary>
    /// Начало ключа события отдыха. Полный ключ включает внутреннее имя вида
    /// отдыха, поэтому у каждого созданного пользователем отдыха своё событие.
    /// </summary>
    public const string RestPrefix = "отдых.";

    /// <summary>Завершение короткого отдыха.</summary>
    public const string RestShort = RestPrefix + "короткий";

    /// <summary>Завершение длительного отдыха.</summary>
    public const string RestLong = RestPrefix + "длительный";

    /// <summary>Событие, вызываемое механикой игровой системы вручную.</summary>
    public const string Custom = "пользовательское.событие";

    /// <summary>
    /// Возвращает ключ события отдыха по внутреннему имени вида отдыха.
    ///
    /// Виды отдыха создаёт пользователь, поэтому перечислить их события заранее
    /// нельзя. Отдых с внутренним именем «короткий» даёт ключ
    /// <see cref="RestShort"/> — привычные виды отдыха попадают в те же события,
    /// что перечислены выше, а созданный пользователем получает своё.
    /// </summary>
    /// <param name="systemName">Внутреннее имя вида отдыха.</param>
    /// <returns>Ключ события отдыха.</returns>
    public static string Rest(string? systemName) =>
        string.IsNullOrWhiteSpace(systemName) ? RestPrefix.TrimEnd('.') : RestPrefix + systemName.Trim();
}
