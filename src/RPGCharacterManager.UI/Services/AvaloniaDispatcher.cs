using Avalonia.Threading;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.Services;

/// <summary>
/// Реализация диспетчера потока пользовательского интерфейса поверх Avalonia UI.
/// </summary>
public sealed class AvaloniaDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public bool IsOnUiThread => Dispatcher.UIThread.CheckAccess();

    /// <inheritdoc />
    public void Post(Action action)
    {
        Guard.NotNull(action);
        Dispatcher.UIThread.Post(action);
    }

    /// <inheritdoc />
    public Task InvokeAsync(Action action)
    {
        Guard.NotNull(action);

        return IsOnUiThread
            ? ExecuteInline(action)
            : Dispatcher.UIThread.InvokeAsync(action).GetTask();
    }

    private static Task ExecuteInline(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
