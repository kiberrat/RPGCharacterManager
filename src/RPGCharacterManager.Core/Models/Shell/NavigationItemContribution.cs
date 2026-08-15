namespace RPGCharacterManager.Core.Models.Shell;

/// <summary>
/// Описание раздела левой панели навигации.
/// Выбор раздела открывает документ с идентификатором <see cref="DocumentId"/> в рабочей области.
/// </summary>
public sealed class NavigationItemContribution
{
    /// <summary>
    /// Создаёт описание раздела навигации.
    /// </summary>
    /// <param name="id">Уникальный идентификатор раздела.</param>
    /// <param name="title">Отображаемое название раздела.</param>
    /// <param name="documentId">Идентификатор открываемого документа.</param>
    public NavigationItemContribution(string id, string title, string documentId)
    {
        Id = id;
        Title = title;
        DocumentId = documentId;
    }

    /// <summary>Уникальный идентификатор раздела.</summary>
    public string Id { get; }

    /// <summary>Отображаемое название раздела.</summary>
    public string Title { get; }

    /// <summary>Идентификатор документа, открываемого при выборе раздела.</summary>
    public string DocumentId { get; }

    /// <summary>Порядок сортировки. Меньшее значение отображается выше.</summary>
    public int Order { get; init; }

    /// <summary>Ключ ресурса значка.</summary>
    public string? IconKey { get; init; }
}
