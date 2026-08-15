namespace RPGCharacterManager.Core.Models.Entities;

/// <summary>
/// Дополнительное имя игрового объекта. Псевдоним хранится отдельно от самого
/// объекта, поэтому пакет перевода может дополнять содержимое любого другого
/// подключенного пакета, не копируя и не перезаписывая его.
/// </summary>
public sealed class ContentAlias : EntityBase
{
    /// <summary>Идентификатор вида контента, например spells или traits.</summary>
    public string ContentTypeId { get; set; } = string.Empty;

    /// <summary>Внутреннее имя объекта, к которому относится псевдоним.</summary>
    public string TargetSystemName { get; set; } = string.Empty;

    /// <summary>Дополнительное имя, по которому объект можно найти.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>Игровая система целевого объекта. Null означает любую систему.</summary>
    public Guid? GameSystemId { get; set; }

    /// <summary>Игровая система целевого объекта.</summary>
    public GameSystem? GameSystem { get; set; }

    /// <summary>Пакет, предоставивший псевдоним.</summary>
    public Guid ContentPackId { get; set; }

    /// <summary>Пакет, предоставивший псевдоним.</summary>
    public ContentPack? ContentPack { get; set; }
}
