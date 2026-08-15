using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Rules;

/// <summary>
/// Уровень важности замечания, найденного при проверке правил.
/// </summary>
public enum RuleIssueSeverity
{
    /// <summary>Предупреждение: правило работоспособно, но поведение может быть неожиданным.</summary>
    Warning = 0,

    /// <summary>Ошибка: правило не может быть выполнено.</summary>
    Error = 1,
}

/// <summary>
/// Замечание, найденное при проверке правила или набора правил.
/// </summary>
/// <param name="Severity">Важность замечания.</param>
/// <param name="RuleName">Название правила, к которому относится замечание.</param>
/// <param name="Message">Описание замечания для пользователя.</param>
public sealed record RuleIssue(RuleIssueSeverity Severity, string RuleName, string Message);

/// <summary>
/// Проверка правил на ошибки и конфликты.
/// </summary>
public interface IRuleValidator
{
    /// <summary>
    /// Проверяет одно правило: корректность формул, заполнение обязательных
    /// параметров действий, существование события и обработчиков действий,
    /// а также заведомо невыполнимые условия.
    /// </summary>
    /// <param name="rule">Проверяемое правило.</param>
    /// <returns>Список найденных замечаний.</returns>
    IReadOnlyList<RuleIssue> Validate(RuleDefinition rule);

    /// <summary>
    /// Проверяет набор правил дополнительно на взаимные конфликты:
    /// совпадение имён и одновременное изменение одного параметра
    /// правилами с одинаковым приоритетом.
    /// </summary>
    /// <param name="rules">Проверяемый набор правил.</param>
    /// <returns>Список найденных замечаний.</returns>
    IReadOnlyList<RuleIssue> ValidateSet(IReadOnlyList<RuleDefinition> rules);
}

/// <summary>
/// Хранение и загрузка игровых правил.
/// </summary>
public interface IRuleService
{
    /// <summary>
    /// Загружает все правила.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список правил.</returns>
    Task<IReadOnlyList<RuleDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Загружает правила, привязанные к указанному событию и включённые в работу.
    /// </summary>
    /// <param name="trigger">Ключ события.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список правил в порядке применения: от меньшего приоритета к большему.</returns>
    Task<IReadOnlyList<RuleDefinition>> GetByTriggerAsync(
        string trigger,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет правило: создаёт новое либо обновляет существующее.
    /// </summary>
    /// <param name="rule">Сохраняемое правило.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат сохранения.</returns>
    Task<Result> SaveAsync(RuleDefinition rule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет правило.
    /// </summary>
    /// <param name="ruleId">Идентификатор правила.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><see langword="true"/>, если правило было найдено и удалено.</returns>
    Task<bool> DeleteAsync(Guid ruleId, CancellationToken cancellationToken = default);
}
