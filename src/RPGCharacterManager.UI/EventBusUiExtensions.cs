using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI;

/// <summary>
/// Подписка на события приложения с выполнением обработчика в потоке интерфейса.
///
/// Службы инфраструктуры используют <c>ConfigureAwait(false)</c> и публикуют события
/// из произвольного потока. Модели представления при этом изменяют свойства, связанные
/// с интерфейсом, поэтому их обработчики обязаны выполняться в потоке интерфейса.
/// Шина событий намеренно ничего не знает о слое представления — переключение потока
/// выполняется здесь, на стороне интерфейса.
/// </summary>
public static class EventBusUiExtensions
{
    /// <summary>
    /// Подписывается на событие и выполняет обработчик в потоке пользовательского интерфейса.
    /// </summary>
    /// <typeparam name="TEvent">Тип события.</typeparam>
    /// <param name="eventBus">Шина событий.</param>
    /// <param name="dispatcher">Диспетчер потока пользовательского интерфейса.</param>
    /// <param name="handler">Обработчик события.</param>
    /// <returns>Объект, отмена подписки выполняется его освобождением.</returns>
    public static IDisposable SubscribeOnUiThread<TEvent>(
        this IEventBus eventBus,
        IUiDispatcher dispatcher,
        Action<TEvent> handler)
        where TEvent : notnull
    {
        Guard.NotNull(eventBus);
        Guard.NotNull(dispatcher);
        Guard.NotNull(handler);

        return eventBus.Subscribe<TEvent>((payload, _) =>
            dispatcher.IsOnUiThread
                ? ExecuteInline(handler, payload)
                : dispatcher.InvokeAsync(() => handler(payload)));
    }

    private static Task ExecuteInline<TEvent>(Action<TEvent> handler, TEvent payload)
    {
        handler(payload);
        return Task.CompletedTask;
    }
}
