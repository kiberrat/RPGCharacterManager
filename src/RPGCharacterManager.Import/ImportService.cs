using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Import;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Import;

/// <summary>
/// Импорт внешних документов.
///
/// Служба ничего не читает сама: она выбирает подходящее чтение по расширению
/// файла. Поэтому новый формат подключается регистрацией ещё одного чтения,
/// а всё, что происходит после — приведение к тексту и распознавание объектов, —
/// остаётся неизменным.
/// </summary>
public sealed class ImportService : IImportService
{
    /// <summary>Наибольший размер импортируемого файла в байтах.</summary>
    public const long MaximumSize = 64L * 1024 * 1024;

    private readonly Dictionary<string, IDocumentReader> _readers;
    private readonly ILogger<ImportService> _logger;

    /// <summary>
    /// Создаёт службу импорта.
    /// </summary>
    /// <param name="readers">Зарегистрированные чтения документов.</param>
    /// <param name="logger">Журналировщик.</param>
    public ImportService(IEnumerable<IDocumentReader> readers, ILogger<ImportService> logger)
    {
        Guard.NotNull(readers);

        _logger = Guard.NotNull(logger);

        var ordered = readers.ToList();

        _readers = ordered
            .SelectMany(reader => reader.Extensions.Select(extension => (extension, reader)))
            .ToDictionary(pair => pair.extension, pair => pair.reader, StringComparer.OrdinalIgnoreCase);

        Formats = ordered
            .Select(reader => new ImportFormat(reader.Format, reader.Extensions))
            .OrderBy(format => format.Title, StringComparer.CurrentCulture)
            .ToList();

        SupportedExtensions = _readers.Keys.OrderBy(extension => extension, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ImportFormat> Formats { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions { get; }

    /// <inheritdoc />
    public bool CanRead(string path) => Find(path) is not null;

    /// <inheritdoc />
    public async Task<Result<ImportedDocument>> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result.Failure<ImportedDocument>("Файл не выбран.");
        }

        var reader = Find(path);

        if (reader is null)
        {
            return Result.Failure<ImportedDocument>(
                $"Формат «{Path.GetExtension(path)}» не поддерживается. Доступны: " +
                string.Join(", ", SupportedExtensions) + ".");
        }

        var file = new FileInfo(path);

        if (file.Exists && file.Length > MaximumSize)
        {
            return Result.Failure<ImportedDocument>(
                $"Файл больше {MaximumSize / (1024 * 1024)} МБ. Разделите его на части.");
        }

        var result = await reader.ReadAsync(path, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            ImportLog.ReadFailed(_logger, Path.GetFileName(path), result.Error!);

            return result;
        }

        if (result.Value.IsEmpty)
        {
            return Result.Failure<ImportedDocument>(
                $"В файле «{Path.GetFileName(path)}» не нашлось текста. " +
                "Если это книга из сканов, распознайте её текст заранее.");
        }

        ImportLog.DocumentRead(_logger, result.Value.Format, result.Value.Name, result.Value.Text.Length);

        return result;
    }

    private IDocumentReader? Find(string path)
    {
        var extension = Path.GetExtension(path);

        return extension.Length > 0 && _readers.TryGetValue(extension, out var reader) ? reader : null;
    }
}

/// <summary>
/// Сообщения журнала подсистемы импорта.
/// </summary>
internal static partial class ImportLog
{
    [LoggerMessage(
        EventId = 11001,
        Level = LogLevel.Information,
        Message = "Прочитан {Format}-документ «{Name}»: знаков {Length}.")]
    public static partial void DocumentRead(ILogger logger, string format, string name, int length);

    [LoggerMessage(
        EventId = 11002,
        Level = LogLevel.Warning,
        Message = "Не удалось прочитать файл «{Name}»: {Reason}")]
    public static partial void ReadFailed(ILogger logger, string name, string reason);
}
