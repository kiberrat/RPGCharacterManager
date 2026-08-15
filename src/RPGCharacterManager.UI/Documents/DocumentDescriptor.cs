using Microsoft.Extensions.DependencyInjection;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.Documents;

/// <summary>
/// Универсальное описание документа рабочей области.
///
/// Позволяет любой подсистеме зарегистрировать свой тип документа одной строкой,
/// не создавая отдельный класс описания для каждого документа.
/// </summary>
/// <typeparam name="TDocument">Тип модели представления документа.</typeparam>
public sealed class DocumentDescriptor<TDocument> : IDocumentDescriptor
    where TDocument : class, IDocument
{
    /// <summary>
    /// Создаёт описание документа.
    /// </summary>
    /// <param name="id">Уникальный идентификатор типа документа.</param>
    /// <param name="title">Заголовок вкладки по умолчанию.</param>
    /// <param name="iconKey">Ключ ресурса значка.</param>
    /// <param name="allowMultipleInstances">Разрешено открывать несколько вкладок этого типа.</param>
    public DocumentDescriptor(
        string id,
        string title,
        string? iconKey = null,
        bool allowMultipleInstances = false)
    {
        Id = Guard.NotNullOrWhiteSpace(id);
        Title = Guard.NotNullOrWhiteSpace(title);
        IconKey = iconKey;
        AllowMultipleInstances = allowMultipleInstances;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Title { get; }

    /// <inheritdoc />
    public string? IconKey { get; }

    /// <inheritdoc />
    public bool AllowMultipleInstances { get; }

    /// <inheritdoc />
    public IDocument Create(IServiceProvider services, object? parameter = null)
    {
        Guard.NotNull(services);

        // ActivatorUtilities позволяет документу получать зависимости через конструктор,
        // не требуя отдельной регистрации каждого типа документа в контейнере.
        // Параметр передаётся дополнительным аргументом: так документ получает
        // отображаемый объект, а остальные зависимости — из контейнера.
        return parameter is null
            ? ActivatorUtilities.CreateInstance<TDocument>(services)
            : ActivatorUtilities.CreateInstance<TDocument>(services, parameter);
    }
}
