using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Macros;

/// <summary>
/// Строка списка макросов.
/// </summary>
/// <param name="Id">Идентификатор макроса.</param>
/// <param name="Name">Название макроса.</param>
/// <param name="Description">Описание макроса.</param>
/// <param name="Category">Категория макроса.</param>
/// <param name="Hotkey">Сочетание клавиш.</param>
/// <param name="ActionCount">Количество действий.</param>
/// <param name="HasCondition">У макроса задано условие.</param>
/// <param name="Enabled">Макрос доступен к запуску.</param>
public sealed record MacroListItem(
    Guid Id,
    string Name,
    string? Description,
    string? Category,
    string? Hotkey,
    int ActionCount,
    bool HasCondition,
    bool Enabled)
{
    /// <summary>У макроса назначено сочетание клавиш.</summary>
    public bool HasHotkey => !string.IsNullOrWhiteSpace(Hotkey);

    /// <summary>Подпись о составе макроса.</summary>
    public string Summary => HasCondition
        ? $"действий: {ActionCount} · с условием"
        : $"действий: {ActionCount}";

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// Макрос в виде, пригодном для правки и выполнения.
/// </summary>
public sealed class MacroDefinition
{
    /// <summary>Идентификатор макроса; пустое значение означает новый макрос.</summary>
    public Guid Id { get; set; }

    /// <summary>Название макроса.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Описание макроса.</summary>
    public string? Description { get; set; }

    /// <summary>Категория макроса.</summary>
    public string? Category { get; set; }

    /// <summary>Сочетание клавиш, запускающее макрос.</summary>
    public string? Hotkey { get; set; }

    /// <summary>Дерево условий. Отсутствие условий означает, что макрос выполняется всегда.</summary>
    public RuleCondition? Condition { get; set; }

    /// <summary>Выполняемые действия в порядке их применения.</summary>
    public IList<RuleAction> Actions { get; init; } = [];

    /// <summary>Макрос доступен к запуску.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Идентификатор персонажа, если макрос принадлежит только ему.</summary>
    public Guid? CharacterId { get; set; }

    /// <summary>Идентификатор игровой системы, которой принадлежит макрос.</summary>
    public Guid? GameSystemId { get; set; }
}

/// <summary>
/// Отчёт о выполнении макроса.
/// </summary>
/// <param name="MacroName">Название макроса.</param>
/// <param name="CharacterName">Имя персонажа, над которым выполнялся макрос.</param>
/// <param name="WasConditionMet">Условие макроса выполнено.</param>
/// <param name="Changes">Описания произошедших изменений.</param>
/// <param name="Issues">Замечания: действия, которые выполнить не удалось.</param>
public sealed record MacroRunReport(
    string MacroName,
    string CharacterName,
    bool WasConditionMet,
    IReadOnlyList<string> Changes,
    IReadOnlyList<string> Issues)
{
    /// <summary>Макрос что-то изменил.</summary>
    public bool HasChanges => Changes.Count > 0;

    /// <summary>При выполнении нашлись замечания.</summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>Краткий итог выполнения для уведомления.</summary>
    public string Summary => !WasConditionMet
        ? "Условие макроса не выполнено"
        : HasChanges
            ? $"Изменений: {Changes.Count}"
            : "Макрос выполнен, изменений нет";
}

/// <summary>
/// Макросы: последовательности действий, которые запускает человек.
///
/// Собственного движка у макросов нет: условия проверяет и действия выполняет
/// тот же движок правил, что и игровые правила. Поэтому обработчик действия,
/// добавленный на будущем этапе, становится доступен и правилам, и макросам
/// (решение Р-97).
/// </summary>
public interface IMacroService
{
    /// <summary>
    /// Возвращает все макросы.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Макросы в порядке отображения.</returns>
    Task<Result<IReadOnlyList<MacroListItem>>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Загружает макрос для правки.
    /// </summary>
    /// <param name="macroId">Идентификатор макроса.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Макрос с разобранными условиями и действиями.</returns>
    Task<Result<MacroDefinition>> GetAsync(Guid macroId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет макрос: создаёт новый либо обновляет существующий.
    /// </summary>
    /// <param name="macro">Сохраняемый макрос.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор сохранённого макроса.</returns>
    Task<Result<Guid>> SaveAsync(MacroDefinition macro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет макрос.
    /// </summary>
    /// <param name="macroId">Идентификатор макроса.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат удаления.</returns>
    Task<Result> DeleteAsync(Guid macroId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполняет макрос над персонажем.
    /// </summary>
    /// <param name="macroId">Идентификатор макроса.</param>
    /// <param name="characterId">Персонаж, над которым выполняется макрос.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Отчёт о выполнении либо описание ошибки.</returns>
    Task<Result<MacroRunReport>> RunAsync(
        Guid macroId,
        Guid characterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает макросы, которым назначено сочетание клавиш.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пары «сочетание клавиш — макрос».</returns>
    Task<IReadOnlyList<MacroListItem>> GetHotkeysAsync(CancellationToken cancellationToken = default);
}
