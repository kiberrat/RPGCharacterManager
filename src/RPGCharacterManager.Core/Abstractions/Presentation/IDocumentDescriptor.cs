namespace RPGCharacterManager.Core.Abstractions.Presentation;

/// <summary>
/// Описание типа документа рабочей области.
/// Регистрируется подсистемой в контейнере зависимостей и позволяет навигации
/// создавать документ по идентификатору, ничего не зная о его реализации.
/// </summary>
public interface IDocumentDescriptor
{
    /// <summary>Уникальный идентификатор типа документа.</summary>
    string Id { get; }

    /// <summary>Заголовок вкладки по умолчанию.</summary>
    string Title { get; }

    /// <summary>Ключ ресурса значка.</summary>
    string? IconKey { get; }

    /// <summary>
    /// Разрешено открывать несколько вкладок этого типа одновременно.
    /// Для документов-одиночек повторное открытие активирует существующую вкладку.
    /// </summary>
    bool AllowMultipleInstances { get; }

    /// <summary>
    /// Создаёт экземпляр документа.
    /// </summary>
    /// <param name="services">Поставщик служб для разрешения зависимостей документа.</param>
    /// <param name="parameter">
    /// Объект, который отображает документ. Передаётся конструктору документа
    /// дополнительным аргументом; остальные зависимости берутся из контейнера.
    /// </param>
    /// <returns>Созданный документ.</returns>
    IDocument Create(IServiceProvider services, object? parameter = null);
}
