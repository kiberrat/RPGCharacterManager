using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Infrastructure.Logging;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Infrastructure.Events;

/// <summary>
/// Шина событий, работающая в пределах процесса приложения.
///
/// Сбой одного обработчика журналируется и не препятствует выполнению остальных:
/// подписчик не должен иметь возможности нарушить работу издателя.
/// </summary>
public sealed class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<Guid, Func<object, CancellationToken, Task>>> _subscriptions = new();
    private readonly ILogger<InMemoryEventBus> _logger;

    /// <summary>
    /// Создаёт шину событий.
    /// </summary>
    /// <param name="logger">Журналировщик.</param>
    public InMemoryEventBus(ILogger<InMemoryEventBus> logger) => _logger = Guard.NotNull(logger);

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent payload, CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        Guard.NotNull((object)payload);

        if (!_subscriptions.TryGetValue(typeof(TEvent), out var handlers))
        {
            return;
        }

        foreach (var handler in handlers.Values)
        {
            try
            {
                await handler(payload, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                InfrastructureLog.EventHandlerFailed(_logger, exception, typeof(TEvent).Name);
            }
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : notnull
    {
        Guard.NotNull(handler);

        var handlers = _subscriptions.GetOrAdd(typeof(TEvent), _ => new ConcurrentDictionary<Guid, Func<object, CancellationToken, Task>>());
        var token = Guid.NewGuid();

        handlers[token] = (payload, cancellationToken) => handler((TEvent)payload, cancellationToken);

        return new Subscription(() => handlers.TryRemove(token, out _));
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : notnull
    {
        Guard.NotNull(handler);

        return Subscribe<TEvent>((payload, _) =>
        {
            handler(payload);
            return Task.CompletedTask;
        });
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _unsubscribe;

        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose()
        {
            Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
        }
    }
}
