using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.Diagnostics;
using RPGCharacterManager.Infrastructure.Logging;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Infrastructure.Diagnostics;

/// <summary>
/// Выполнение длительных операций вне потока пользовательского интерфейса
/// с отображением их количества в строке состояния.
/// </summary>
public sealed class BackgroundTaskService : IBackgroundTaskService
{
    private readonly ObservableCollection<BackgroundTaskInfo> _runningTasks = [];
    private readonly IUiDispatcher _dispatcher;
    private readonly IEventBus _eventBus;
    private readonly ILogger<BackgroundTaskService> _logger;

    /// <summary>
    /// Создаёт службу фоновых задач.
    /// </summary>
    /// <param name="dispatcher">Диспетчер потока пользовательского интерфейса.</param>
    /// <param name="eventBus">Шина событий для публикации сведений об ошибках.</param>
    /// <param name="logger">Журналировщик.</param>
    public BackgroundTaskService(
        IUiDispatcher dispatcher,
        IEventBus eventBus,
        ILogger<BackgroundTaskService> logger)
    {
        _dispatcher = Guard.NotNull(dispatcher);
        _eventBus = Guard.NotNull(eventBus);
        _logger = Guard.NotNull(logger);

        RunningTasks = new ReadOnlyObservableCollection<BackgroundTaskInfo>(_runningTasks);
    }

    /// <inheritdoc />
    public ReadOnlyObservableCollection<BackgroundTaskInfo> RunningTasks { get; }

    /// <inheritdoc />
    public async Task RunAsync(
        string title,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        await RunAsync<object?>(
            title,
            async token =>
            {
                await operation(token).ConfigureAwait(false);
                return null;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TResult> RunAsync<TResult>(
        string title,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(title);
        Guard.NotNull(operation);

        var info = new BackgroundTaskInfo(Guid.NewGuid(), title, DateTimeOffset.Now);
        await TrackAsync(info, isStarting: true).ConfigureAwait(false);

        InfrastructureLog.BackgroundTaskStarted(_logger, info.Title, info.Id);

        try
        {
            // Task.Run гарантирует, что операция не начнёт выполняться синхронно
            // в потоке пользовательского интерфейса.
            var result = await Task.Run(() => operation(cancellationToken), cancellationToken).ConfigureAwait(false);

            InfrastructureLog.BackgroundTaskCompleted(_logger, info.Title);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            InfrastructureLog.BackgroundTaskCancelled(_logger, info.Title);
            throw;
        }
        catch (Exception exception)
        {
            InfrastructureLog.BackgroundTaskFailed(_logger, exception, info.Title);
            await _eventBus
                .PublishAsync(new ApplicationErrorEvent(title, exception, IsFatal: false), CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
        finally
        {
            await TrackAsync(info, isStarting: false).ConfigureAwait(false);
        }
    }

    private Task TrackAsync(BackgroundTaskInfo info, bool isStarting)
    {
        void Update()
        {
            if (isStarting)
            {
                _runningTasks.Add(info);
            }
            else
            {
                _runningTasks.Remove(info);
            }
        }

        if (_dispatcher.IsOnUiThread)
        {
            Update();
            return Task.CompletedTask;
        }

        return _dispatcher.InvokeAsync(Update);
    }
}
