namespace RPGCharacterManager.Core.Models.Diagnostics;

/// <summary>
/// Сведения о выполняющейся фоновой задаче.
/// Отображаются в строке состояния главного окна.
/// </summary>
/// <param name="Id">Уникальный идентификатор задачи.</param>
/// <param name="Title">Название задачи для пользователя.</param>
/// <param name="StartedAt">Момент запуска задачи.</param>
public sealed record BackgroundTaskInfo(Guid Id, string Title, DateTimeOffset StartedAt);
