using RPGCharacterManager.Core.Abstractions.Engine;

namespace RPGCharacterManager.Core.Abstractions.Rules;

/// <summary>
/// Объект, к которому применяются правила.
///
/// Абстракция намеренно не привязана к персонажу: правила должны одинаково
/// применяться к персонажу, монстру, предмету или пробному объекту в окне
/// тестирования. Параметры доступны как переменные формул, а признаки —
/// эффекты, черты, владения — как набор меток.
/// </summary>
public interface IRuleTarget : IFormulaContext
{
    /// <summary>Название объекта для журнала и отчёта о выполнении.</summary>
    string DisplayName { get; }

    /// <summary>Имена доступных параметров.</summary>
    IReadOnlyCollection<string> VariableNames { get; }

    /// <summary>Признаки объекта: эффекты, черты, владения.</summary>
    IReadOnlyCollection<string> Tags { get; }

    /// <summary>
    /// Задаёт значение параметра.
    /// </summary>
    /// <param name="name">Имя параметра.</param>
    /// <param name="value">Новое значение.</param>
    void SetVariable(string name, FormulaValue value);

    /// <summary>
    /// Проверяет наличие признака.
    /// </summary>
    /// <param name="tag">Название признака.</param>
    /// <returns><see langword="true"/>, если признак присутствует.</returns>
    bool HasTag(string tag);

    /// <summary>
    /// Добавляет признак объекту.
    /// </summary>
    /// <param name="tag">Название признака.</param>
    /// <returns><see langword="true"/>, если признак был добавлен.</returns>
    bool AddTag(string tag);

    /// <summary>
    /// Удаляет признак у объекта.
    /// </summary>
    /// <param name="tag">Название признака.</param>
    /// <returns><see langword="true"/>, если признак был удалён.</returns>
    bool RemoveTag(string tag);
}

/// <summary>
/// Тип значения параметра действия. Определяет способ ввода в редакторе.
/// </summary>
public enum RuleParameterKind
{
    /// <summary>Выражение движка формул.</summary>
    Expression = 0,

    /// <summary>Произвольный текст.</summary>
    Text = 1,

    /// <summary>Имя параметра объекта.</summary>
    VariableName = 2,

    /// <summary>Название признака: эффекта, черты, владения.</summary>
    TagName = 3,
}

/// <summary>
/// Описание параметра действия.
/// </summary>
/// <param name="Name">Внутреннее имя параметра.</param>
/// <param name="DisplayName">Отображаемое название.</param>
/// <param name="Kind">Тип значения.</param>
/// <param name="IsRequired">Параметр обязателен к заполнению.</param>
public sealed record RuleActionParameter(
    string Name,
    string DisplayName,
    RuleParameterKind Kind,
    bool IsRequired = true);

/// <summary>
/// Результат выполнения одного действия.
/// </summary>
/// <param name="RuleName">Название правила, выполнившего действие.</param>
/// <param name="ActionKind">Вид действия.</param>
/// <param name="Description">Описание произошедшего для пользователя.</param>
/// <param name="Succeeded">Действие выполнено успешно.</param>
public sealed record RuleActionOutcome(
    string RuleName,
    string ActionKind,
    string Description,
    bool Succeeded);

/// <summary>
/// Службы, доступные обработчику действия во время выполнения.
/// </summary>
public interface IRuleActionServices
{
    /// <summary>Единый движок вычислений. Все выражения действий вычисляются им.</summary>
    IFormulaEngine Formulas { get; }
}

/// <summary>
/// Обработчик одного вида действия правила.
///
/// Новый вид действия добавляется регистрацией реализации в контейнере зависимостей
/// и сразу появляется в редакторе правил — изменять движок и редактор не требуется.
/// </summary>
public interface IRuleActionHandler
{
    /// <summary>Ключ вида действия, сохраняемый в правиле.</summary>
    string Kind { get; }

    /// <summary>Отображаемое название действия.</summary>
    string DisplayName { get; }

    /// <summary>Пояснение к действию для редактора.</summary>
    string Description { get; }

    /// <summary>Описание параметров действия.</summary>
    IReadOnlyList<RuleActionParameter> Parameters { get; }

    /// <summary>
    /// Выполняет действие над объектом.
    /// </summary>
    /// <param name="action">Действие с заданными пользователем параметрами.</param>
    /// <param name="target">Объект, к которому применяется действие.</param>
    /// <param name="ruleName">Название выполняемого правила.</param>
    /// <param name="services">Службы движка.</param>
    /// <returns>Результат выполнения действия.</returns>
    RuleActionOutcome Execute(
        RuleAction action,
        IRuleTarget target,
        string ruleName,
        IRuleActionServices services);
}

/// <summary>
/// Поставщик событий, способных запускать правила.
/// Подсистема регистрирует собственный поставщик и добавляет свои события.
/// </summary>
public interface IRuleTriggerProvider
{
    /// <summary>
    /// Возвращает предоставляемые события.
    /// </summary>
    /// <returns>Последовательность описаний событий.</returns>
    IEnumerable<RuleTrigger> GetTriggers();
}

/// <summary>
/// Сводный перечень событий приложения.
/// </summary>
public interface IRuleTriggerCatalog
{
    /// <summary>Все известные события, упорядоченные по категории и названию.</summary>
    IReadOnlyList<RuleTrigger> Triggers { get; }

    /// <summary>
    /// Находит событие по ключу.
    /// </summary>
    /// <param name="key">Ключ события.</param>
    /// <returns>Описание события или <see langword="null"/>, если оно неизвестно.</returns>
    RuleTrigger? Find(string key);
}

/// <summary>
/// Отчёт о применении правил к объекту.
/// Отображается в окне тестирования и записывается в журнал.
/// </summary>
/// <param name="Trigger">Ключ события, вызвавшего выполнение.</param>
/// <param name="ExecutedRules">Названия правил, условия которых выполнились.</param>
/// <param name="SkippedRules">Названия правил, условия которых не выполнились.</param>
/// <param name="Outcomes">Результаты выполненных действий.</param>
public sealed record RuleExecutionReport(
    string Trigger,
    IReadOnlyList<string> ExecutedRules,
    IReadOnlyList<string> SkippedRules,
    IReadOnlyList<RuleActionOutcome> Outcomes);

/// <summary>
/// Движок игровых правил.
///
/// Согласно документу 019_Редактор_правил.md любая игровая механика описывается
/// правилом и не может быть жёстко встроена в код приложения.
/// </summary>
public interface IRuleEngine
{
    /// <summary>Зарегистрированные обработчики действий.</summary>
    IReadOnlyCollection<IRuleActionHandler> ActionHandlers { get; }

    /// <summary>
    /// Проверяет выполнение дерева условий для объекта.
    /// </summary>
    /// <param name="condition">Дерево условий. Отсутствие условий означает выполнение.</param>
    /// <param name="target">Проверяемый объект.</param>
    /// <returns><see langword="true"/>, если условия выполнены.</returns>
    bool EvaluateCondition(RuleCondition? condition, IRuleTarget target);

    /// <summary>
    /// Применяет к объекту все правила указанного события.
    ///
    /// Правила выполняются в порядке возрастания приоритета: правило с большим
    /// приоритетом применяется последним и переопределяет результат предыдущих.
    /// Именно так документ 019_Редактор_правил.md разрешает конфликт правил,
    /// изменяющих одно и то же значение.
    /// </summary>
    /// <param name="trigger">Ключ события.</param>
    /// <param name="target">Объект, к которому применяются правила.</param>
    /// <param name="rules">Правила, среди которых выполняется отбор.</param>
    /// <returns>Отчёт о выполнении.</returns>
    RuleExecutionReport Execute(string trigger, IRuleTarget target, IEnumerable<RuleDefinition> rules);
}
