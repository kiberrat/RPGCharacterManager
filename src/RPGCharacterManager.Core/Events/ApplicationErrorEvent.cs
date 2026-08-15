namespace RPGCharacterManager.Core.Events;

/// <summary>
/// Событие необработанной ошибки приложения.
/// Публикуется системой обработки ошибок, чтобы интерфейс мог уведомить пользователя,
/// не создавая прямой зависимости от слоя инфраструктуры.
/// </summary>
/// <param name="Source">Источник ошибки: имя подсистемы или операции.</param>
/// <param name="Exception">Возникшее исключение.</param>
/// <param name="IsFatal">Ошибка не позволяет продолжить работу приложения.</param>
public sealed record ApplicationErrorEvent(string Source, Exception Exception, bool IsFatal);
