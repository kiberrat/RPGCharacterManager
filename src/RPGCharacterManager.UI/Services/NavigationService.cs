using System.Collections.Frozen;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Logging;

namespace RPGCharacterManager.UI.Services;

/// <summary>
/// Навигация по документам рабочей области.
///
/// Служба не знает о конкретных типах документов: она работает только с описаниями
/// <see cref="IDocumentDescriptor"/>, зарегистрированными подсистемами в контейнере.
/// Благодаря этому добавление нового раздела приложения не требует изменения навигации.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly ObservableCollection<IDocument> _documents = [];

    /// <summary>
    /// Объекты, отображаемые открытыми документами.
    /// Позволяют не открывать вторую вкладку для уже показанного объекта.
    /// </summary>
    private readonly Dictionary<IDocument, object?> _parameters = [];

    private readonly FrozenDictionary<string, IDocumentDescriptor> _descriptors;
    private readonly IServiceProvider _services;
    private readonly ILogger<NavigationService> _logger;

    private IDocument? _activeDocument;

    /// <summary>
    /// Создаёт службу навигации.
    /// </summary>
    /// <param name="descriptors">Зарегистрированные описания документов.</param>
    /// <param name="services">Поставщик служб для создания документов.</param>
    /// <param name="logger">Журналировщик.</param>
    public NavigationService(
        IEnumerable<IDocumentDescriptor> descriptors,
        IServiceProvider services,
        ILogger<NavigationService> logger)
    {
        Guard.NotNull(descriptors);

        _services = Guard.NotNull(services);
        _logger = Guard.NotNull(logger);
        // FrozenDictionary оптимизирован для многократного чтения без изменений:
        // состав описаний документов фиксируется на этапе построения контейнера.
        _descriptors = descriptors.ToFrozenDictionary(descriptor => descriptor.Id, StringComparer.Ordinal);

        Documents = new ReadOnlyObservableCollection<IDocument>(_documents);
    }

    /// <inheritdoc />
    public event EventHandler<IDocument?>? ActiveDocumentChanged;

    /// <inheritdoc />
    public ReadOnlyObservableCollection<IDocument> Documents { get; }

    /// <inheritdoc />
    public IDocument? ActiveDocument
    {
        get => _activeDocument;
        private set
        {
            if (ReferenceEquals(_activeDocument, value))
            {
                return;
            }

            _activeDocument = value;
            ActiveDocumentChanged?.Invoke(this, value);
        }
    }

    /// <inheritdoc />
    public async Task<IDocument> OpenAsync(
        string documentId,
        object? parameter = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(documentId);

        if (!_descriptors.TryGetValue(documentId, out var descriptor))
        {
            throw new InvalidOperationException(
                $"Описание документа «{documentId}» не зарегистрировано в контейнере зависимостей.");
        }

        if (FindExisting(descriptor, parameter) is { } existing)
        {
            ActiveDocument = existing;
            return existing;
        }

        var created = descriptor.Create(_services, parameter);
        await created.InitializeAsync(cancellationToken).ConfigureAwait(true);

        _parameters[created] = parameter;
        _documents.Add(created);
        ActiveDocument = created;

        UiLog.DocumentOpened(_logger, documentId);
        return created;
    }

    /// <summary>
    /// Ищет уже открытый документ, который не следует открывать повторно.
    ///
    /// Документ-одиночка существует в единственном экземпляре, а документ,
    /// допускающий несколько вкладок, — в одном экземпляре на каждое значение
    /// параметра: лист одного и того же персонажа не открывается дважды.
    /// </summary>
    /// <param name="descriptor">Описание документа.</param>
    /// <param name="parameter">Отображаемый объект.</param>
    /// <returns>Открытый документ или <see langword="null"/>.</returns>
    private IDocument? FindExisting(IDocumentDescriptor descriptor, object? parameter) =>
        _documents.FirstOrDefault(document =>
            string.Equals(document.DocumentId, descriptor.Id, StringComparison.Ordinal)
            && (!descriptor.AllowMultipleInstances
                || Equals(_parameters.GetValueOrDefault(document), parameter)));

    /// <inheritdoc />
    public void Activate(IDocument document)
    {
        Guard.NotNull(document);

        if (_documents.Contains(document))
        {
            ActiveDocument = document;
        }
    }

    /// <inheritdoc />
    public void Move(IDocument document, int targetIndex)
    {
        Guard.NotNull(document);

        var currentIndex = _documents.IndexOf(document);
        if (currentIndex < 0)
        {
            return;
        }

        var boundedIndex = Math.Clamp(targetIndex, 0, _documents.Count - 1);
        if (boundedIndex == currentIndex)
        {
            return;
        }

        // Move сообщает списку именно о перемещении, поэтому вкладка сохраняет
        // выделение и не пересоздаётся как новый документ.
        _documents.Move(currentIndex, boundedIndex);
    }

    /// <inheritdoc />
    public async Task<bool> CloseAsync(IDocument document, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(document);

        if (!_documents.Contains(document))
        {
            return true;
        }

        if (!await document.CanCloseAsync(cancellationToken).ConfigureAwait(true))
        {
            return false;
        }

        Close(document);
        return true;
    }

    /// <inheritdoc />
    public void Close(IDocument document)
    {
        Guard.NotNull(document);

        var closedIndex = _documents.IndexOf(document);
        if (closedIndex < 0)
        {
            return;
        }

        _documents.RemoveAt(closedIndex);
        _parameters.Remove(document);

        if (ReferenceEquals(ActiveDocument, document))
        {
            // После закрытия активируется соседняя вкладка, как в современных редакторах.
            var nextIndex = Math.Min(closedIndex, _documents.Count - 1);
            ActiveDocument = nextIndex >= 0 ? _documents[nextIndex] : null;
        }

        (document as IDisposable)?.Dispose();

        UiLog.DocumentClosed(_logger, document.DocumentId);
    }

    /// <inheritdoc />
    public async Task<bool> CloseAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var document in _documents.ToList())
        {
            if (!await CloseAsync(document, cancellationToken).ConfigureAwait(true))
            {
                return false;
            }
        }

        return true;
    }
}
