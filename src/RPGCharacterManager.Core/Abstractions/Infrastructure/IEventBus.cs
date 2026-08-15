namespace RPGCharacterManager.Core.Abstractions.Infrastructure;

/// <summary>
/// Внутренняя шина событий приложения.
/// Обеспечивает слабую связанность подсистем: издатель события не знает о подписчиках.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Публикует событие и дожидается завершения всех обработчиков.
    /// </summary>
    /// <typeparam name="TEvent">Тип события.</typeparam>
    /// <param name="payload">Экземпляр события.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после обработки события всеми подписчиками.</returns>
    Task PublishAsync<TEvent>(TEvent payload, CancellationToken cancellationToken = default)
        where TEvent : notnull;

    /// <summary>
    /// Подписывается на событие указанного типа.
    /// </summary>
    /// <typeparam name="TEvent">Тип события.</typeparam>
    /// <param name="handler">Асинхронный обработчик события.</param>
    /// <returns>Объект, отмена подписки выполняется его освобождением.</returns>
    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : notnull;

    /// <summary>
    /// Подписывается на событие указанного типа синхронным обработчиком.
    /// </summary>
    /// <typeparam name="TEvent">Тип события.</typeparam>
    /// <param name="handler">Обработчик события.</param>
    /// <returns>Объект, отмена подписки выполняется его освобождением.</returns>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : notnull;
}
