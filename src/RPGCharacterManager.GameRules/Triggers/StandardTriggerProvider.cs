using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.GameRules.Triggers;

/// <summary>
/// События приложения, перечисленные в документе 019_Редактор_правил.md.
///
/// Список является данными, а не жёстко заданной механикой: подсистема, добавляющая
/// собственное событие, регистрирует свой <see cref="IRuleTriggerProvider"/>,
/// и событие сразу появляется в редакторе правил.
/// </summary>
public sealed class StandardTriggerProvider : IRuleTriggerProvider
{
    /// <inheritdoc />
    public IEnumerable<RuleTrigger> GetTriggers()
    {
        // ---------- Персонаж ----------
        yield return new RuleTrigger(
            RuleTriggers.CharacterCreated,
            "Создание персонажа",
            RuleCategories.Character,
            "Возникает после создания нового персонажа.");

        yield return new RuleTrigger(
            RuleTriggers.CharacterLevelUp,
            "Повышение уровня",
            RuleCategories.Character,
            "Возникает при увеличении уровня персонажа.");

        yield return new RuleTrigger(
            RuleTriggers.CharacterAttributeChanged,
            "Изменение характеристики",
            RuleCategories.Character,
            "Возникает при изменении любой характеристики персонажа.");

        yield return new RuleTrigger(
            RuleTriggers.CharacterRecalculated,
            "Пересчёт персонажа",
            RuleCategories.Character,
            "Возникает при полном пересчёте параметров персонажа.");

        // ---------- Бой ----------
        yield return new RuleTrigger(
            RuleTriggers.CombatStarted,
            "Начало боя",
            RuleCategories.Combat,
            "Возникает при начале боевого столкновения.");

        yield return new RuleTrigger(
            RuleTriggers.CombatTurnEnded,
            "Конец хода",
            RuleCategories.Combat,
            "Возникает по завершении хода персонажа.");

        yield return new RuleTrigger(
            RuleTriggers.CombatHit,
            "Попадание",
            RuleCategories.Combat,
            "Возникает при успешной атаке.");

        yield return new RuleTrigger(
            RuleTriggers.CombatCriticalHit,
            "Критическое попадание",
            RuleCategories.Combat,
            "Возникает при критическом результате броска атаки.");

        yield return new RuleTrigger(
            RuleTriggers.CombatDamageTaken,
            "Получение урона",
            RuleCategories.Combat,
            "Возникает при получении персонажем урона.");

        yield return new RuleTrigger(
            RuleTriggers.CombatDeath,
            "Смерть",
            RuleCategories.Combat,
            "Возникает при гибели персонажа.");

        // ---------- Магия ----------
        yield return new RuleTrigger(
            RuleTriggers.MagicSpellCast,
            "Использование заклинания",
            RuleCategories.Magic,
            "Возникает при применении заклинания или способности.");

        yield return new RuleTrigger(
            RuleTriggers.MagicEffectEnded,
            "Окончание эффекта",
            RuleCategories.Magic,
            "Возникает при истечении срока действия эффекта.");

        yield return new RuleTrigger(
            RuleTriggers.MagicConcentrationLost,
            "Потеря концентрации",
            RuleCategories.Magic,
            "Возникает при прекращении концентрации на заклинании.");

        // ---------- Предметы ----------
        yield return new RuleTrigger(
            RuleTriggers.ItemObtained,
            "Получение предмета",
            RuleCategories.Items,
            "Возникает при добавлении предмета в инвентарь.");

        yield return new RuleTrigger(
            RuleTriggers.ItemEquipped,
            "Экипировка предмета",
            RuleCategories.Items,
            "Возникает при надевании предмета в слот экипировки.");

        yield return new RuleTrigger(
            RuleTriggers.ItemUnequipped,
            "Снятие предмета",
            RuleCategories.Items,
            "Возникает при снятии предмета со слота экипировки.");

        // ---------- Отдых ----------
        yield return new RuleTrigger(
            RuleTriggers.RestShort,
            "Короткий отдых",
            RuleCategories.Rest,
            "Возникает по завершении короткого отдыха.");

        yield return new RuleTrigger(
            RuleTriggers.RestLong,
            "Длительный отдых",
            RuleCategories.Rest,
            "Возникает по завершении длительного отдыха.");

        // ---------- Пользовательские ----------
        yield return new RuleTrigger(
            RuleTriggers.Custom,
            "Пользовательское событие",
            RuleCategories.Custom,
            "Событие, вызываемое вручную или собственной механикой игровой системы.");
    }
}

/// <summary>
/// Сводный перечень событий, собранный из всех зарегистрированных поставщиков.
/// </summary>
public sealed class RuleTriggerCatalog : IRuleTriggerCatalog
{
    private readonly Dictionary<string, RuleTrigger> _byKey;

    /// <summary>
    /// Создаёт перечень событий.
    /// </summary>
    /// <param name="providers">Зарегистрированные поставщики событий.</param>
    public RuleTriggerCatalog(IEnumerable<IRuleTriggerProvider> providers)
    {
        Guard.NotNull(providers);

        var map = new Dictionary<string, RuleTrigger>(StringComparer.OrdinalIgnoreCase);

        foreach (var trigger in providers.SelectMany(provider => provider.GetTriggers()))
        {
            map[trigger.Key] = trigger;
        }

        _byKey = map;

        Triggers = map.Values
            .OrderBy(trigger => Array.IndexOf([.. RuleCategories.All], trigger.Category))
            .ThenBy(trigger => trigger.DisplayName, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<RuleTrigger> Triggers { get; }

    /// <inheritdoc />
    public RuleTrigger? Find(string key) =>
        !string.IsNullOrWhiteSpace(key) && _byKey.TryGetValue(key, out var trigger) ? trigger : null;
}
