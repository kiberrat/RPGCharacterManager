namespace RPGCharacterManager.Core.Abstractions.Presentation;

/// <summary>
/// Модальные диалоговые окна приложения.
/// Использование системных диалогов Windows запрещено документом 003_UI_UX.md,
/// поэтому все диалоги реализуются средствами приложения в едином стиле.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Показывает информационное сообщение.
    /// </summary>
    /// <param name="title">Заголовок окна.</param>
    /// <param name="message">Текст сообщения.</param>
    /// <returns>Задача, завершающаяся после закрытия окна.</returns>
    Task ShowInformationAsync(string title, string message);

    /// <summary>
    /// Показывает сообщение об ошибке.
    /// </summary>
    /// <param name="title">Заголовок окна.</param>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="details">Технические подробности, скрытые под раскрывающимся блоком.</param>
    /// <returns>Задача, завершающаяся после закрытия окна.</returns>
    Task ShowErrorAsync(string title, string message, string? details = null);

    /// <summary>
    /// Запрашивает подтверждение действия.
    /// </summary>
    /// <param name="title">Заголовок окна.</param>
    /// <param name="message">Текст вопроса.</param>
    /// <returns><see langword="true"/>, если пользователь подтвердил действие.</returns>
    Task<bool> ShowConfirmationAsync(string title, string message);
}
