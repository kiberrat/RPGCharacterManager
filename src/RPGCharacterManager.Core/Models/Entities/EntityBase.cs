namespace RPGCharacterManager.Core.Models.Entities;

/// <summary>
/// Базовый класс всех сущностей базы данных.
/// </summary>
public abstract class EntityBase
{
    /// <summary>Первичный ключ записи.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Момент создания записи.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Момент последнего изменения записи.</summary>
    public DateTimeOffset? ModifiedAt { get; set; }
}

/// <summary>
/// Базовый класс игровых объектов, редактируемых пользователем.
///
/// Документ 004_База_данных.md требует хранить пользовательские данные отдельно от
/// встроенных. Разделение обеспечивается признаком <see cref="IsSystem"/> и связями
/// с игровой системой и контент-паком: удаление пользовательского контента не
/// затрагивает системные записи.
/// </summary>
public abstract class ContentEntity : EntityBase
{
    /// <summary>Отображаемое название объекта.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Внутреннее имя объекта, используемое в формулах и правилах.
    /// Формируется из названия и уникально в пределах игровой системы.
    /// </summary>
    public string SystemName { get; set; } = string.Empty;

    /// <summary>Описание объекта.</summary>
    public string? Description { get; set; }

    /// <summary>Источник объекта: книга, контент-пак или имя автора.</summary>
    public string? Source { get; set; }

    /// <summary>Идентификатор игровой системы, которой принадлежит объект.</summary>
    public Guid? GameSystemId { get; set; }

    /// <summary>Игровая система, которой принадлежит объект.</summary>
    public GameSystem? GameSystem { get; set; }

    /// <summary>Идентификатор контент-пака, из которого получен объект.</summary>
    public Guid? ContentPackId { get; set; }

    /// <summary>Контент-пак, из которого получен объект.</summary>
    public ContentPack? ContentPack { get; set; }

    /// <summary>
    /// Объект является системным и доступен только для чтения.
    /// Изменение системного объекта выполняется созданием пользовательской копии.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>Ключ или путь изображения объекта.</summary>
    public string? Image { get; set; }

    /// <summary>Ключ значка объекта.</summary>
    public string? Icon { get; set; }
}
