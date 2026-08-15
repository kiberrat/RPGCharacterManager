using CommunityToolkit.Mvvm.ComponentModel;
using RPGCharacterManager.Core.Abstractions.Presentation;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Базовая модель представления документа рабочей области.
/// </summary>
public abstract partial class DocumentViewModelBase : ViewModelBase, IDocument
{
    [ObservableProperty]
    private string _title;

    /// <summary>
    /// Создаёт модель представления документа.
    /// </summary>
    /// <param name="documentId">Идентификатор описания документа.</param>
    /// <param name="title">Заголовок вкладки.</param>
    /// <param name="iconKey">Ключ ресурса значка вкладки.</param>
    protected DocumentViewModelBase(string documentId, string title, string? iconKey = null)
    {
        DocumentId = documentId;
        _title = title;
        IconKey = iconKey;
    }

    /// <inheritdoc />
    public string DocumentId { get; }

    /// <inheritdoc />
    public string? IconKey { get; }

    /// <inheritdoc />
    public virtual Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task<bool> CanCloseAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}
