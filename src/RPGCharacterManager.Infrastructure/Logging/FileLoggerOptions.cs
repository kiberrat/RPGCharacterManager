using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.Infrastructure.Logging;

/// <summary>
/// Параметры записи журнала в файл.
/// </summary>
public sealed class FileLoggerOptions
{
    /// <summary>Имя секции конфигурации, из которой читаются параметры.</summary>
    public const string SectionName = "Logging:File";

    /// <summary>Размер файла журнала по умолчанию, после которого создаётся новый файл.</summary>
    public const long DefaultMaxFileSizeBytes = 10L * 1024 * 1024;

    /// <summary>Количество хранимых файлов журнала по умолчанию.</summary>
    public const int DefaultRetainedFileCount = 14;

    /// <summary>Каталог, в котором создаются файлы журнала.</summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>Префикс имени файла журнала.</summary>
    public string FileNamePrefix { get; set; } = "rpgmanager";

    /// <summary>Минимальный уровень записываемых сообщений.</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>Максимальный размер одного файла журнала в байтах.</summary>
    public long MaxFileSizeBytes { get; set; } = DefaultMaxFileSizeBytes;

    /// <summary>Количество хранимых файлов журнала. Более старые файлы удаляются.</summary>
    public int RetainedFileCount { get; set; } = DefaultRetainedFileCount;
}
