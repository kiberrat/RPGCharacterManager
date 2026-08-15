namespace RPGCharacterManager.Core.Abstractions.Presentation;

/// <summary>
/// Документ рабочей области — содержимое одной вкладки главного окна.
/// Контракт не зависит от конкретной технологии интерфейса.
/// </summary>
public interface IDocument
{
    /// <summary>Идентификатор описания документа, из которого он был создан.</summary>
    string DocumentId { get; }

    /// <summary>Заголовок вкладки.</summary>
    string Title { get; }

    /// <summary>Ключ ресурса значка вкладки.</summary>
    string? IconKey { get; }

    /// <summary>
    /// Выполняет отложенную инициализацию документа.
    /// Вызывается навигацией после создания документа, до его отображения.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после инициализации.</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет возможность закрытия документа и при необходимости запрашивает подтверждение.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><see langword="true"/>, если документ можно закрыть.</returns>
    Task<bool> CanCloseAsync(CancellationToken cancellationToken = default);
}
