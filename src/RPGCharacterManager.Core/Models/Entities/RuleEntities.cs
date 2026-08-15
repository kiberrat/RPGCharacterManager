namespace RPGCharacterManager.Core.Models.Entities;

/// <summary>
/// Именованная формула, доступная для повторного использования во всём приложении.
/// </summary>
public class Formula : ContentEntity
{
    /// <summary>Текст выражения, вычисляемый единым движком формул.</summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>Тип возвращаемого значения.</summary>
    public GameValueType ReturnType { get; set; } = GameValueType.FractionalNumber;

    /// <summary>Категория формулы.</summary>
    public string? Category { get; set; }
}

/// <summary>
/// Игровое правило: событие, условие и выполняемое действие.
///
/// Согласно документу 019_Редактор_правил.md любая игровая механика описывается
/// правилом и не может быть жёстко встроена в код приложения.
/// </summary>
public class GameRule : ContentEntity
{
    /// <summary>Категория правила: бой, персонаж, магия, предметы, отдых.</summary>
    public string? Category { get; set; }

    /// <summary>Событие, запускающее правило.</summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>Условие выполнения правила в виде выражения.</summary>
    public string? Condition { get; set; }

    /// <summary>Описание выполняемых действий в формате JSON.</summary>
    public string ActionsJson { get; set; } = "[]";

    /// <summary>
    /// Приоритет правила. Чем больше значение, тем выше приоритет
    /// при изменении одного и того же значения несколькими правилами.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>Правило активно.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Идентификатор персонажа, если правило применяется только к нему.</summary>
    public Guid? CharacterId { get; set; }

    /// <summary>Идентификатор кампании, если правило применяется только в ней.</summary>
    public Guid? CampaignId { get; set; }

    /// <summary>Версия правила.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Автор правила.</summary>
    public string? Author { get; set; }
}

/// <summary>
/// Описание пользовательского свойства игрового объекта.
///
/// Позволяет добавлять новые поля любому объекту без изменения структуры базы данных —
/// одна из ключевых возможностей приложения.
/// </summary>
public class PropertyDefinition : ContentEntity
{
    /// <summary>Отображаемое имя свойства.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Тип объекта, к которому применяется свойство: <c>Character</c>, <c>Item</c>,
    /// <c>Spell</c> и любой другой тип сущности.
    /// </summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>Тип данных свойства.</summary>
    public GameValueType DataType { get; set; } = GameValueType.Text;

    /// <summary>Значение по умолчанию.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Категория свойства.</summary>
    public string? Category { get; set; }

    /// <summary>Группа свойства внутри категории.</summary>
    public string? Group { get; set; }

    /// <summary>Свойство обязательно к заполнению.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Свойство отображается в интерфейсе.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Свойство доступно для редактирования пользователем.</summary>
    public bool IsEditable { get; set; } = true;

    /// <summary>Формула вычисления значения свойства.</summary>
    public string? Formula { get; set; }

    /// <summary>Правило проверки значения в виде выражения.</summary>
    public string? ValidationRule { get; set; }

    /// <summary>
    /// Тип объекта, на который ссылается свойство,
    /// если <see cref="DataType"/> равен <see cref="GameValueType.ObjectReference"/>.
    /// </summary>
    public string? ReferenceTargetType { get; set; }

    /// <summary>Допустимые варианты для перечислений, разделённые переводом строки.</summary>
    public string? AllowedValues { get; set; }

    /// <summary>Порядок отображения свойства.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Значение пользовательского свойства конкретного объекта.
/// </summary>
public class PropertyValue : EntityBase
{
    /// <summary>Идентификатор объекта, которому принадлежит значение.</summary>
    public Guid ObjectId { get; set; }

    /// <summary>Идентификатор описания свойства.</summary>
    public Guid PropertyDefinitionId { get; set; }

    /// <summary>Описание свойства.</summary>
    public PropertyDefinition? PropertyDefinition { get; set; }

    /// <summary>
    /// Значение свойства в строковом представлении.
    /// Преобразование к типу выполняется в соответствии с
    /// <see cref="PropertyDefinition.DataType"/>.
    /// </summary>
    public string? Value { get; set; }
}
