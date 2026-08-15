namespace RPGCharacterManager.Core.Models.Entities;

/// <summary>
/// Сведения о резервной копии базы данных.
/// </summary>
public class BackupRecord : EntityBase
{
    /// <summary>Полный путь к файлу резервной копии.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Размер файла копии в байтах.</summary>
    public long SizeInBytes { get; set; }

    /// <summary>Копия создана автоматически, а не по команде пользователя.</summary>
    public bool IsAutomatic { get; set; }

    /// <summary>Примечание пользователя к копии.</summary>
    public string? Comment { get; set; }
}
