namespace RPGCharacterManager.Core.Models.Entities;

/// <summary>
/// Класс персонажа: основной путь развития.
/// Понятие охватывает классы, профессии, архетипы, роли и специализации любых игровых систем.
/// </summary>
public class CharacterClass : ContentEntity
{
    /// <summary>Формула кости хитов или прироста здоровья за уровень.</summary>
    public string? HitDiceFormula { get; set; }

    /// <summary>Идентификатор основной характеристики класса.</summary>
    public Guid? PrimaryAttributeId { get; set; }

    /// <summary>Основная характеристика класса.</summary>
    public AttributeDefinition? PrimaryAttribute { get; set; }

    /// <summary>Роль класса в группе.</summary>
    public string? Role { get; set; }

    /// <summary>Требования к выбору класса в виде выражения.</summary>
    public string? Requirements { get; set; }

    /// <summary>Начальный уровень класса.</summary>
    public int StartingLevel { get; set; } = 1;

    /// <summary>Максимальный уровень класса.</summary>
    public int MaximumLevel { get; set; } = 20;

    /// <summary>Цвет оформления класса в интерфейсе.</summary>
    public string? Color { get; set; }

    /// <summary>Подклассы этого класса.</summary>
    public ICollection<Subclass> Subclasses { get; set; } = [];
}

/// <summary>
/// Подкласс: специализация внутри класса.
/// </summary>
public class Subclass : ContentEntity
{
    /// <summary>Идентификатор родительского класса.</summary>
    public Guid ClassId { get; set; }

    /// <summary>Родительский класс.</summary>
    public CharacterClass? Class { get; set; }

    /// <summary>Уровень, на котором становится доступен выбор подкласса.</summary>
    public int AvailableAtLevel { get; set; } = 1;

    /// <summary>Требования к выбору подкласса в виде выражения.</summary>
    public string? Requirements { get; set; }
}

/// <summary>
/// Раса или происхождение персонажа.
/// </summary>
public class Race : ContentEntity
{
    /// <summary>Базовая скорость перемещения.</summary>
    public double Speed { get; set; }

    /// <summary>Размер существа, определяемый игровой системой.</summary>
    public string? Size { get; set; }

    /// <summary>Известные языки, перечисленные через запятую.</summary>
    public string? Languages { get; set; }

    /// <summary>Требования к выбору расы в виде выражения.</summary>
    public string? Requirements { get; set; }
}

/// <summary>
/// Происхождение персонажа: предыстория, культура, профессия, фракция.
/// </summary>
public class Background : ContentEntity
{
    /// <summary>Требования к выбору происхождения в виде выражения.</summary>
    public string? Requirements { get; set; }
}

/// <summary>
/// Черта: постоянный или временный модификатор возможностей персонажа.
/// Понятие охватывает feats, traits, perks, talents и аналогичные механики.
/// </summary>
public class Trait : ContentEntity
{
    /// <summary>Категория черты.</summary>
    public string? Category { get; set; }

    /// <summary>Требования к получению черты в виде выражения.</summary>
    public string? Requirements { get; set; }

    /// <summary>Формула вычисления эффекта черты.</summary>
    public string? Formula { get; set; }

    /// <summary>Количество использований. Пустое значение означает пассивную черту.</summary>
    public string? UsesFormula { get; set; }

    /// <summary>Условие восстановления использований, например после отдыха.</summary>
    public string? RechargeRule { get; set; }

    /// <summary>Уровень или ранг черты для черт с несколькими ступенями.</summary>
    public int Level { get; set; }

    /// <summary>Идентификатор черты, требуемой для получения этой черты.</summary>
    public Guid? RequiredTraitId { get; set; }

    /// <summary>Черта, требуемая для получения этой черты.</summary>
    public Trait? RequiredTrait { get; set; }

    /// <summary>Черта действует только при выполнении условия.</summary>
    public string? ActivationCondition { get; set; }
}

/// <summary>
/// Способность: активное или пассивное действие персонажа.
/// </summary>
public class Ability : ContentEntity
{
    /// <summary>Категория способности.</summary>
    public string? Category { get; set; }

    /// <summary>Формула вычисления результата способности.</summary>
    public string? Formula { get; set; }

    /// <summary>Идентификатор расходуемого ресурса.</summary>
    public Guid? ResourceId { get; set; }

    /// <summary>Расходуемый ресурс.</summary>
    public GameResource? Resource { get; set; }

    /// <summary>Формула количества расходуемого ресурса.</summary>
    public string? ResourceCostFormula { get; set; }

    /// <summary>Условие восстановления использований.</summary>
    public string? RechargeRule { get; set; }

    /// <summary>Требования к использованию способности.</summary>
    public string? Requirements { get; set; }
}

/// <summary>
/// Черта, полученная персонажем.
/// </summary>
public class CharacterTrait : EntityBase
{
    /// <summary>Идентификатор персонажа.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Персонаж.</summary>
    public Character? Character { get; set; }

    /// <summary>Идентификатор черты.</summary>
    public Guid TraitId { get; set; }

    /// <summary>Черта.</summary>
    public Trait? Trait { get; set; }

    /// <summary>Источник получения черты: раса, класс, уровень, предмет.</summary>
    public string? Source { get; set; }

    /// <summary>Оставшееся количество использований.</summary>
    public int RemainingUses { get; set; }

    /// <summary>Черта активна. Позволяет временно отключать переключаемые черты.</summary>
    public bool IsActive { get; set; } = true;
}
