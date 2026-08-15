using System.Collections.ObjectModel;

namespace RPGCharacterManager.Core.Abstractions.Presentation;

/// <summary>
/// Навигация по документам рабочей области.
/// </summary>
public interface INavigationService
{
    /// <summary>Открытые документы в порядке их вкладок.</summary>
    ReadOnlyObservableCollection<IDocument> Documents { get; }

    /// <summary>Активный документ или <see langword="null"/>, если рабочая область пуста.</summary>
    IDocument? ActiveDocument { get; }

    /// <summary>Возникает при смене активного документа.</summary>
    event EventHandler<IDocument?>? ActiveDocumentChanged;

    /// <summary>
    /// Открывает документ по идентификатору его описания.
    ///
    /// Если документ-одиночка уже открыт, активируется существующая вкладка.
    /// Документ, допускающий несколько вкладок, повторно открывается только для
    /// нового значения параметра: лист одного персонажа не открывается дважды.
    /// </summary>
    /// <param name="documentId">Идентификатор описания документа.</param>
    /// <param name="parameter">
    /// Объект, который отображает документ, например идентификатор персонажа.
    /// Передаётся конструктору документа дополнительным аргументом.
    /// </param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Открытый документ.</returns>
    /// <exception cref="InvalidOperationException">Описание документа не зарегистрировано.</exception>
    Task<IDocument> OpenAsync(
        string documentId,
        object? parameter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Делает документ активным.
    /// </summary>
    /// <param name="document">Документ, который следует активировать.</param>
    void Activate(IDocument document);

    /// <summary>
    /// Перемещает документ на другую позицию в полосе вкладок.
    ///
    /// Порядок вкладок принадлежит пользователю: он выстраивает рабочее место так,
    /// как ему удобно. Перемещение не меняет ни состав документов, ни активный документ.
    /// </summary>
    /// <param name="document">Перемещаемый документ.</param>
    /// <param name="targetIndex">
    /// Позиция, в которую следует поместить документ. Значения за пределами
    /// списка приводятся к ближайшей допустимой позиции.
    /// </param>
    void Move(IDocument document, int targetIndex);

    /// <summary>
    /// Закрывает документ, если он это разрешает.
    /// </summary>
    /// <param name="document">Закрываемый документ.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><see langword="true"/>, если документ был закрыт.</returns>
    Task<bool> CloseAsync(IDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Убирает документ из рабочей области, не спрашивая его о готовности закрыться.
    ///
    /// Метод предназначен для случая, когда готовность уже подтверждена вызовом
    /// <see cref="IDocument.CanCloseAsync"/>: оболочка сначала спрашивает документ,
    /// затем показывает исчезновение вкладки и лишь после этого убирает документ.
    /// Повторный вопрос показал бы пользователю то же подтверждение дважды.
    ///
    /// Во всех остальных случаях следует вызывать <see cref="CloseAsync"/>.
    /// </summary>
    /// <param name="document">Убираемый документ.</param>
    void Close(IDocument document);

    /// <summary>
    /// Закрывает все открытые документы.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><see langword="true"/>, если все документы были закрыты.</returns>
    Task<bool> CloseAllAsync(CancellationToken cancellationToken = default);
}
