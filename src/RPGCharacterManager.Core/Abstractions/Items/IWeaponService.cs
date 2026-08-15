using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Items;

/// <summary>
/// Имена переменных, доступных формулам оружия.
///
/// Помимо перечисленных здесь, формулам оружия доступны все переменные персонажа:
/// значения характеристик по внутренним именам, уровень, выбранные раса, класс,
/// подкласс и происхождение.
/// </summary>
public static class WeaponVariables
{
    /// <summary>Значение характеристики масштабирования.</summary>
    public const string ScalingValue = CharacterVariables.Value;

    /// <summary>Модификатор характеристики масштабирования.</summary>
    public const string ScalingModifier = CharacterVariables.LinkedModifier;

    /// <summary>Уровень владения навыком оружия.</summary>
    public const string Proficiency = CharacterVariables.Proficiency;

    /// <summary>Итоговое значение навыка оружия.</summary>
    public const string SkillValue = CharacterVariables.SkillValue;

    /// <summary>Обычный урон оружия внутри формулы критического урона.</summary>
    public const string Damage = CharacterVariables.Damage;

    /// <summary>Выпавшее значение кости попадания.</summary>
    public const string Roll = CharacterVariables.Roll;

    /// <summary>Итог броска попадания.</summary>
    public const string Attack = CharacterVariables.Attack;
}

/// <summary>
/// Состояние боеприпасов оружия персонажа.
/// </summary>
/// <param name="ItemId">Идентификатор предмета-боеприпаса.</param>
/// <param name="Name">Название боеприпаса.</param>
/// <param name="PerShot">Расход боеприпасов за одну атаку.</param>
/// <param name="MagazineSize">Вместимость магазина либо <see langword="null"/>, если магазина нет.</param>
/// <param name="Loaded">Количество боеприпасов в магазине либо <see langword="null"/>, если магазина нет.</param>
/// <param name="Reserve">Количество боеприпасов в инвентаре персонажа.</param>
public sealed record WeaponAmmunition(
    Guid ItemId,
    string Name,
    int PerShot,
    int? MagazineSize,
    int? Loaded,
    int Reserve)
{
    /// <summary>Оружие использует магазин и требует перезарядки.</summary>
    public bool HasMagazine => MagazineSize is > 0;

    /// <summary>Боеприпасов хватает на одну атаку.</summary>
    public bool IsReady => HasMagazine ? Loaded >= PerShot : Reserve >= PerShot;

    /// <summary>Магазин можно пополнить.</summary>
    public bool CanReload => HasMagazine && Loaded < MagazineSize && Reserve > 0;
}

/// <summary>
/// Оружие персонажа с уже вычисленными боевыми значениями.
/// </summary>
/// <param name="InventoryItemId">Идентификатор записи инвентаря.</param>
/// <param name="ItemId">Идентификатор предмета.</param>
/// <param name="Name">Название оружия.</param>
/// <param name="Description">Описание оружия.</param>
/// <param name="Category">Категория оружия.</param>
/// <param name="WeaponType">Тип оружия.</param>
/// <param name="Range">Дальность применения.</param>
/// <param name="DamageType">Тип наносимого урона.</param>
/// <param name="Properties">Свойства оружия.</param>
/// <param name="AttackDiceFormula">Формула кости попадания.</param>
/// <param name="AttackBonusFormula">Формула бонуса попадания.</param>
/// <param name="AttackBonus">Вычисленный бонус попадания.</param>
/// <param name="DamageFormula">Формула урона.</param>
/// <param name="Damage">Диапазон урона с учётом характеристик персонажа.</param>
/// <param name="CriticalFormula">Формула критического урона.</param>
/// <param name="CriticalThreshold">Значение кости, начиная с которого попадание критическое.</param>
/// <param name="ScalingAttributeName">Название характеристики масштабирования.</param>
/// <param name="ProficiencySkillName">Название навыка владения оружием.</param>
/// <param name="ProficiencyLevel">Уровень владения оружием.</param>
/// <param name="ReloadTime">Время перезарядки по правилам игровой системы.</param>
/// <param name="Ammunition">Состояние боеприпасов либо <see langword="null"/>, если они не нужны.</param>
/// <param name="UnavailableReason">Причина, по которой персонаж не может применить оружие.</param>
/// <param name="Issues">Замечания: ошибки формул оружия.</param>
public sealed record CharacterWeapon(
    Guid InventoryItemId,
    Guid ItemId,
    string Name,
    string? Description,
    string? Category,
    string? WeaponType,
    string? Range,
    string? DamageType,
    IReadOnlyList<string> Properties,
    string? AttackDiceFormula,
    string? AttackBonusFormula,
    double AttackBonus,
    string? DamageFormula,
    FormulaRange? Damage,
    string? CriticalFormula,
    int? CriticalThreshold,
    string? ScalingAttributeName,
    string? ProficiencySkillName,
    int ProficiencyLevel,
    string? ReloadTime,
    WeaponAmmunition? Ammunition,
    string? UnavailableReason,
    IReadOnlyList<string> Issues)
{
    /// <summary>Требования оружия выполнены.</summary>
    public bool IsAvailable => UnavailableReason is null;

    /// <summary>Оружие наносит критические попадания.</summary>
    public bool HasCritical => CriticalThreshold is not null;

    /// <summary>Оружие расходует боеприпасы.</summary>
    public bool UsesAmmunition => Ammunition is not null;
}

/// <summary>
/// Результат атаки оружием.
/// </summary>
/// <param name="WeaponName">Название оружия.</param>
/// <param name="Roll">Выпавшее значение кости попадания либо <see langword="null"/>, если кость не задана.</param>
/// <param name="AttackBonus">Бонус попадания.</param>
/// <param name="AttackTotal">Итог броска попадания либо <see langword="null"/>, если кость не задана.</param>
/// <param name="IsCritical">Попадание оказалось критическим.</param>
/// <param name="Damage">Нанесённый урон.</param>
/// <param name="DamageType">Тип нанесённого урона.</param>
/// <param name="AmmunitionSpent">Израсходовано боеприпасов.</param>
/// <param name="AmmunitionLeft">Осталось боеприпасов в магазине либо в запасе.</param>
/// <param name="AppliedRules">Названия применённых правил боя.</param>
/// <param name="Description">Готовое описание атаки для журнала и интерфейса.</param>
public sealed record WeaponAttackResult(
    string WeaponName,
    double? Roll,
    double AttackBonus,
    double? AttackTotal,
    bool IsCritical,
    double Damage,
    string? DamageType,
    int AmmunitionSpent,
    int? AmmunitionLeft,
    IReadOnlyList<string> AppliedRules,
    string Description);

/// <summary>
/// Результат перезарядки оружия.
/// </summary>
/// <param name="WeaponName">Название оружия.</param>
/// <param name="Loaded">Количество боеприпасов в магазине после перезарядки.</param>
/// <param name="MagazineSize">Вместимость магазина.</param>
/// <param name="Reserve">Остаток боеприпасов в инвентаре.</param>
/// <param name="ReloadTime">Время перезарядки по правилам игровой системы.</param>
/// <param name="Description">Готовое описание перезарядки для интерфейса.</param>
public sealed record WeaponReloadResult(
    string WeaponName,
    int Loaded,
    int MagazineSize,
    int Reserve,
    string? ReloadTime,
    string Description);

/// <summary>Параметры авторского оружия одного персонажа.</summary>
/// <param name="Name">Название оружия.</param>
/// <param name="Description">Описание.</param>
/// <param name="ItemType">Тип предмета.</param>
/// <param name="Category">Категория оружия.</param>
/// <param name="Range">Дальность.</param>
/// <param name="DamageType">Тип урона.</param>
/// <param name="Properties">Свойства оружия.</param>
/// <param name="AttackDiceFormula">Формула кости попадания.</param>
/// <param name="AttackFormula">Формула бонуса попадания.</param>
/// <param name="DamageFormula">Формула урона.</param>
/// <param name="CriticalFormula">Формула критического урона.</param>
/// <param name="CriticalThreshold">Порог критического попадания.</param>
/// <param name="ScalingAttributeId">Характеристика масштабирования.</param>
/// <param name="ProficiencySkillId">Навык владения.</param>
/// <param name="Weight">Вес.</param>
/// <param name="Price">Стоимость.</param>
/// <param name="Currency">Валюта стоимости.</param>
public sealed record LocalWeaponDraft(
    string Name,
    string? Description,
    string? ItemType,
    string? Category,
    string? Range,
    string? DamageType,
    string? Properties,
    string? AttackDiceFormula,
    string? AttackFormula,
    string? DamageFormula,
    string? CriticalFormula,
    int? CriticalThreshold,
    Guid? ScalingAttributeId,
    Guid? ProficiencySkillId,
    double Weight,
    double Price,
    string? Currency);
/// <summary>
/// Оружие персонажа: вычисление боевых значений, атака, перезарядка.
///
/// Служба не содержит правил ни одной конкретной игры. Бросок попадания, урон,
/// критическое попадание и расход боеприпасов определяются формулами оружия,
/// которые задал пользователь, и правилами событий «бой.попадание»
/// и «бой.критическое_попадание».
/// </summary>
public interface IWeaponService
{
    /// <summary>
    /// Возвращает оружие персонажа с вычисленными бонусом попадания, диапазоном урона
    /// и состоянием боеприпасов.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Оружие персонажа либо описание ошибки.</returns>
    Task<Result<IReadOnlyList<CharacterWeapon>>> GetWeaponsAsync(
        Guid characterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает оружие, доступное персонажу для получения, с проверкой требований.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="search">Строка поиска по названию.</param>
    /// <param name="includeUnavailable">Показывать оружие с невыполненными требованиями.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница вариантов выбора.</returns>
    Task<CharacterOptionPage> GetAvailableWeaponsAsync(
        Guid characterId,
        string? search,
        bool includeUnavailable,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Выдаёт персонажу оружие: создаёт запись инвентаря и заполняет магазин,
    /// если оружие его использует.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="itemId">Идентификатор предмета-оружия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор созданной записи инвентаря либо описание ошибки.</returns>
    Task<Result<Guid>> AddAsync(
        Guid characterId,
        Guid itemId,
        CancellationToken cancellationToken = default);

    /// <summary>Создаёт авторское оружие только для указанного персонажа и сразу выдаёт его.</summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="draft">Параметры оружия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор записи инвентаря либо ошибка.</returns>
    Task<Result<Guid>> CreateLocalAsync(
        Guid characterId,
        LocalWeaponDraft draft,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Убирает оружие у персонажа.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="inventoryItemId">Идентификатор записи инвентаря.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат выполнения.</returns>
    Task<Result> RemoveAsync(
        Guid characterId,
        Guid inventoryItemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполняет атаку оружием: бросает кость попадания, определяет критическое
    /// попадание, вычисляет урон, применяет правила боя и расходует боеприпасы.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="inventoryItemId">Идентификатор записи инвентаря.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат атаки либо причина, по которой она невозможна.</returns>
    Task<Result<WeaponAttackResult>> AttackAsync(
        Guid characterId,
        Guid inventoryItemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Задаёт количество боеприпасов оружия, имеющихся у персонажа.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="inventoryItemId">Идентификатор записи инвентаря с оружием.</param>
    /// <param name="count">Новое количество боеприпасов в запасе.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат выполнения.</returns>
    Task<Result> SetAmmunitionReserveAsync(
        Guid characterId,
        Guid inventoryItemId,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Перезаряжает оружие, перенося боеприпасы из инвентаря в магазин.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="inventoryItemId">Идентификатор записи инвентаря.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат перезарядки либо причина, по которой она невозможна.</returns>
    Task<Result<WeaponReloadResult>> ReloadAsync(
        Guid characterId,
        Guid inventoryItemId,
        CancellationToken cancellationToken = default);
}
