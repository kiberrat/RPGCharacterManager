namespace RPGCharacterManager.Core.Abstractions.Presentation;

/// <summary>
/// Тип всплывающего уведомления.
/// </summary>
public enum NotificationKind
{
    /// <summary>Информационное сообщение.</summary>
    Information = 0,

    /// <summary>Успешное завершение операции.</summary>
    Success = 1,

    /// <summary>Предупреждение.</summary>
    Warning = 2,

    /// <summary>Ошибка.</summary>
    Error = 3,
}

/// <summary>
/// Небольшие всплывающие уведомления, не прерывающие работу пользователя.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Показывает всплывающее уведомление.
    /// </summary>
    /// <param name="message">Текст уведомления.</param>
    /// <param name="kind">Тип уведомления.</param>
    void Show(string message, NotificationKind kind = NotificationKind.Information);
}
