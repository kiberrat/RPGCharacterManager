using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Characters;

/// <summary>
/// Имена переменных, доступных формулам восстановления при отдыхе.
/// </summary>
public static class RestVariables
{
    /// <summary>Максимальное значение восстанавливаемого ресурса.</summary>
    public const string Maximum = "максимум";

    /// <summary>Текущее значение восстанавливаемого ресурса до восстановления.</summary>
    public const string Current = "текущее";
}

/// <summary>
/// Что отдых сделает с одним ресурсом.
/// </summary>
/// <param name="ResourceName">Название ресурса.</param>
/// <param name="Description">Описание восстановления: «до максимума» или величина.</param>
/// <param name="Condition">Условие, при котором восстановление происходит.</param>
public sealed record RestRestorePreview(string ResourceName, string Description, string? Condition);

/// <summary>
/// Вид отдыха, доступный персонажу.
/// </summary>
/// <param name="Id">Идентификатор вида отдыха.</param>
/// <param name="Name">Название отдыха.</param>
/// <param name="Description">Описание отдыха.</param>
/// <param name="Duration">Длительность отдыха в виде текста.</param>
/// <param name="IsAvailable">Требования отдыха выполнены.</param>
/// <param name="UnavailableReason">Причина, по которой отдохнуть нельзя.</param>
/// <param name="Restores">Что отдых восстановит.</param>
public sealed record RestOption(
    Guid Id,
    string Name,
    string? Description,
    string? Duration,
    bool IsAvailable,
    string? UnavailableReason,
    IReadOnlyList<RestRestorePreview> Restores);

/// <summary>
/// Виды отдыха, доступные персонажу.
/// </summary>
/// <param name="Options">Виды отдыха в заданном пользователем порядке.</param>
public sealed record RestState(IReadOnlyList<RestOption> Options);

/// <summary>
/// Изменение ресурса при отдыхе.
/// </summary>
/// <param name="ResourceName">Название ресурса.</param>
/// <param name="Before">Значение до отдыха.</param>
/// <param name="After">Значение после отдыха.</param>
public sealed record RestResourceChange(string ResourceName, double Before, double After);

/// <summary>
/// Итог отдыха.
/// </summary>
/// <param name="RestName">Название отдыха.</param>
/// <param name="Changes">Изменённые ресурсы.</param>
/// <param name="Expired">Названия эффектов, срок которых истёк за время отдыха.</param>
/// <param name="AppliedRules">Названия применённых правил события отдыха.</param>
/// <param name="Issues">Замечания вычисления формул.</param>
public sealed record RestResult(
    string RestName,
    IReadOnlyList<RestResourceChange> Changes,
    IReadOnlyList<string> Expired,
    IReadOnlyList<string> AppliedRules,
    IReadOnlyList<string> Issues);

/// <summary>
/// Отдых персонажа: восстановление ресурсов и течение времени.
///
/// Служба не знает ни короткого, ни длительного отдыха: любой отдых — это запись
/// игрового контента со своим списком восстановлений. Поэтому система, где отдыхов
/// три или где их нет вовсе, работает без изменения приложения.
/// </summary>
public interface IRestService
{
    /// <summary>
    /// Возвращает виды отдыха, доступные персонажу, вместе с тем,
    /// что каждый из них восстановит.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Виды отдыха либо описание ошибки.</returns>
    Task<Result<RestState>> GetAsync(Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполняет отдых: восстанавливает ресурсы, продвигает таймеры эффектов
    /// на длительность отдыха и применяет правила события отдыха.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="restTypeId">Идентификатор вида отдыха.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итог отдыха либо описание ошибки.</returns>
    Task<Result<RestResult>> RestAsync(
        Guid characterId,
        Guid restTypeId,
        CancellationToken cancellationToken = default);
}
