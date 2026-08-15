namespace RPGCharacterManager.Core.Abstractions.Infrastructure;

/// <summary>
/// Переключение выполнения в поток пользовательского интерфейса.
///
/// Абстракция позволяет службам инфраструктуры безопасно изменять коллекции,
/// связанные с интерфейсом, не завися при этом от конкретной технологии интерфейса.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Вызов выполняется из потока пользовательского интерфейса.</summary>
    bool IsOnUiThread { get; }

    /// <summary>
    /// Выполняет действие в потоке пользовательского интерфейса без ожидания результата.
    /// </summary>
    /// <param name="action">Выполняемое действие.</param>
    void Post(Action action);

    /// <summary>
    /// Выполняет действие в потоке пользовательского интерфейса и дожидается завершения.
    /// </summary>
    /// <param name="action">Выполняемое действие.</param>
    /// <returns>Задача, завершающаяся после выполнения действия.</returns>
    Task InvokeAsync(Action action);
}
