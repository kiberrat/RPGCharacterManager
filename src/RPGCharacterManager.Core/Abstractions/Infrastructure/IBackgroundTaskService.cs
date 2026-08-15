using System.Collections.ObjectModel;
using RPGCharacterManager.Core.Models.Diagnostics;

namespace RPGCharacterManager.Core.Abstractions.Infrastructure;

/// <summary>
/// Выполнение длительных операций вне потока пользовательского интерфейса.
/// Гарантирует, что интерфейс не блокируется, а сбой задачи не приводит к аварийному
/// завершению приложения: исключение журналируется и публикуется в шину событий.
/// </summary>
public interface IBackgroundTaskService
{
    /// <summary>Список выполняющихся в данный момент задач.</summary>
    ReadOnlyObservableCollection<BackgroundTaskInfo> RunningTasks { get; }

    /// <summary>
    /// Запускает фоновую операцию и отслеживает её выполнение.
    /// </summary>
    /// <param name="title">Название операции, отображаемое пользователю.</param>
    /// <param name="operation">Выполняемая операция.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после выполнения операции.</returns>
    Task RunAsync(string title, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Запускает фоновую операцию, возвращающую значение.
    /// </summary>
    /// <typeparam name="TResult">Тип результата операции.</typeparam>
    /// <param name="title">Название операции, отображаемое пользователю.</param>
    /// <param name="operation">Выполняемая операция.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат выполнения операции.</returns>
    Task<TResult> RunAsync<TResult>(
        string title,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
