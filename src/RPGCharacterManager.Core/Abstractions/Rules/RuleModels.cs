namespace RPGCharacterManager.Core.Abstractions.Rules;

/// <summary>
/// Логическая связка между условиями группы.
/// </summary>
public enum RuleLogicalOperator
{
    /// <summary>Все вложенные условия должны выполняться.</summary>
    And = 0,

    /// <summary>Достаточно выполнения хотя бы одного вложенного условия.</summary>
    Or = 1,
}

/// <summary>
/// Оператор сравнения, доступный в конструкторе условий.
/// Состав операторов определён документом 019_Редактор_правил.md.
/// </summary>
public enum RuleComparisonOperator
{
    /// <summary>Равно.</summary>
    Equal = 0,

    /// <summary>Не равно.</summary>
    NotEqual = 1,

    /// <summary>Меньше.</summary>
    Less = 2,

    /// <summary>Больше.</summary>
    Greater = 3,

    /// <summary>Меньше или равно.</summary>
    LessOrEqual = 4,

    /// <summary>Больше или равно.</summary>
    GreaterOrEqual = 5,

    /// <summary>Текст слева содержит текст справа.</summary>
    Contains = 6,

    /// <summary>У объекта есть указанный признак: эффект, черта, владение.</summary>
    Has = 7,

    /// <summary>У объекта отсутствует указанный признак.</summary>
    HasNot = 8,
}

/// <summary>
/// Узел дерева условий правила.
///
/// Дерево является той самой структурой «узлов», которую пользователь собирает
/// в визуальном редакторе: группы задают логику И/ИЛИ/НЕ, а сравнения — проверки
/// конкретных параметров.
/// </summary>
public abstract class RuleCondition
{
    /// <summary>
    /// Создаёт копию узла вместе со всем поддеревом.
    /// Используется редактором для отмены незавершённого изменения.
    /// </summary>
    /// <returns>Независимая копия узла.</returns>
    public abstract RuleCondition Clone();
}

/// <summary>
/// Группа условий, объединённых логической связкой.
/// </summary>
public sealed class RuleConditionGroup : RuleCondition
{
    /// <summary>Логическая связка между вложенными условиями.</summary>
    public RuleLogicalOperator Operator { get; set; } = RuleLogicalOperator.And;

    /// <summary>Результат группы инвертируется — логическое «НЕ».</summary>
    public bool IsNegated { get; set; }

    /// <summary>Вложенные условия.</summary>
    public IList<RuleCondition> Children { get; init; } = [];

    /// <inheritdoc />
    public override RuleCondition Clone()
    {
        var copy = new RuleConditionGroup
        {
            Operator = Operator,
            IsNegated = IsNegated,
        };

        foreach (var child in Children)
        {
            copy.Children.Add(child.Clone());
        }

        return copy;
    }
}

/// <summary>
/// Сравнение двух выражений.
///
/// Левая и правая части являются выражениями движка формул, поэтому условие может
/// сравнивать не только параметры объекта, но и результаты вычислений:
/// <c>Сила + Уровень &gt;= 20</c>.
/// </summary>
public sealed class RuleComparison : RuleCondition
{
    /// <summary>Левая часть сравнения: выражение или имя параметра.</summary>
    public string Left { get; set; } = string.Empty;

    /// <summary>Оператор сравнения.</summary>
    public RuleComparisonOperator Operator { get; set; } = RuleComparisonOperator.Equal;

    /// <summary>
    /// Правая часть сравнения: выражение, значение либо имя признака
    /// для операторов «Имеет» и «Не имеет».
    /// </summary>
    public string Right { get; set; } = string.Empty;

    /// <inheritdoc />
    public override RuleCondition Clone() => new RuleComparison
    {
        Left = Left,
        Operator = Operator,
        Right = Right,
    };
}

/// <summary>
/// Действие, выполняемое правилом.
///
/// Действие описывается видом и набором именованных параметров, поэтому новый вид
/// действия добавляется регистрацией обработчика и не требует изменения модели.
/// </summary>
public sealed class RuleAction
{
    /// <summary>Вид действия, соответствующий ключу обработчика.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Параметры действия, заданные пользователем.</summary>
    public IDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Создаёт независимую копию действия.
    /// </summary>
    /// <returns>Копия действия.</returns>
    public RuleAction Clone()
    {
        var copy = new RuleAction { Kind = Kind };

        foreach (var pair in Parameters)
        {
            copy.Parameters[pair.Key] = pair.Value;
        }

        return copy;
    }

    /// <summary>
    /// Возвращает значение параметра или пустую строку, если параметр не задан.
    /// </summary>
    /// <param name="name">Имя параметра.</param>
    /// <returns>Значение параметра.</returns>
    public string GetParameter(string name) =>
        Parameters.TryGetValue(name, out var value) ? value : string.Empty;
}

/// <summary>
/// Правило игровой системы в виде, пригодном для выполнения.
///
/// Хранимая запись <see cref="Models.Entities.GameRule"/> содержит условия и действия
/// в текстовом виде; данный класс представляет их разобранными структурами.
/// </summary>
public sealed class RuleDefinition
{
    /// <summary>Идентификатор правила.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Название правила.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Описание правила.</summary>
    public string? Description { get; set; }

    /// <summary>Категория правила: бой, персонаж, магия, предметы, отдых, пользовательская.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Ключ события, запускающего правило.</summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>
    /// Приоритет правила. Чем больше значение, тем позже применяется правило,
    /// поэтому его результат переопределяет результат правил с меньшим приоритетом.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>Правило активно.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Дерево условий. Отсутствие условий означает, что правило выполняется всегда.</summary>
    public RuleCondition? Condition { get; set; }

    /// <summary>Выполняемые действия в порядке их применения.</summary>
    public IList<RuleAction> Actions { get; init; } = [];

    /// <summary>Идентификатор игровой системы, которой принадлежит правило.</summary>
    public Guid? GameSystemId { get; set; }

    /// <summary>Идентификатор персонажа, если правило применяется только к нему.</summary>
    public Guid? CharacterId { get; set; }

    /// <summary>Идентификатор кампании, если правило применяется только в ней.</summary>
    public Guid? CampaignId { get; set; }

    /// <summary>Версия правила.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Автор правила.</summary>
    public string? Author { get; set; }

    /// <summary>
    /// Создаёт независимую копию правила вместе с условиями и действиями.
    /// </summary>
    /// <returns>Копия правила.</returns>
    public RuleDefinition Clone()
    {
        var copy = new RuleDefinition
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Category = Category,
            Trigger = Trigger,
            Priority = Priority,
            Enabled = Enabled,
            Condition = Condition?.Clone(),
            GameSystemId = GameSystemId,
            CharacterId = CharacterId,
            CampaignId = CampaignId,
            Version = Version,
            Author = Author,
        };

        foreach (var action in Actions)
        {
            copy.Actions.Add(action.Clone());
        }

        return copy;
    }
}

/// <summary>
/// Описание события, способного запустить правило.
/// </summary>
/// <param name="Key">Внутренний ключ события, сохраняемый в правиле.</param>
/// <param name="DisplayName">Отображаемое название события.</param>
/// <param name="Category">Категория события.</param>
/// <param name="Description">Пояснение, когда событие возникает.</param>
public sealed record RuleTrigger(string Key, string DisplayName, string Category, string Description);

/// <summary>
/// Категория правил и событий.
/// Значения задают порядок и названия разделов в редакторе правил.
/// </summary>
public static class RuleCategories
{
    /// <summary>Правила персонажа: характеристики, уровни, опыт, здоровье.</summary>
    public const string Character = "Персонаж";

    /// <summary>Правила боя: инициатива, атаки, критические удары, действия.</summary>
    public const string Combat = "Бой";

    /// <summary>Правила магии: ячейки, стоимость заклинаний, концентрация.</summary>
    public const string Magic = "Магия";

    /// <summary>Правила предметов: свойства оружия, броня, эффекты.</summary>
    public const string Items = "Предметы";

    /// <summary>Правила отдыха: короткий и длительный отдых, восстановление.</summary>
    public const string Rest = "Отдых";

    /// <summary>Пользовательские правила, не относящиеся к остальным категориям.</summary>
    public const string Custom = "Пользовательские";

    /// <summary>Все встроенные категории в порядке отображения.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Character, Combat, Magic, Items, Rest, Custom];
}
