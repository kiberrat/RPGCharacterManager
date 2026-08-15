using RPGCharacterManager.Core.Abstractions.Import;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Import.Readers;

/// <summary>
/// Основа чтения документа: общая обработка отсутствующего и нечитаемого файла.
/// </summary>
internal abstract class DocumentReaderBase : IDocumentReader
{
    /// <inheritdoc />
    public abstract string Format { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<string> Extensions { get; }

    /// <inheritdoc />
    public async Task<Result<ImportedDocument>> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return Result.Failure<ImportedDocument>($"Файл «{path}» не найден.");
        }

        try
        {
            var notes = new List<string>();
            var text = await ExtractAsync(path, notes, cancellationToken).ConfigureAwait(false);

            return Result.Success(new ImportedDocument(
                Path.GetFileNameWithoutExtension(path),
                Format,
                text.Trim(),
                notes));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Чужой файл может быть повреждён, зашифрован или вовсе не тем,
            // чем притворяется расширение. Это ожидаемый исход импорта,
            // а не повод прервать работу приложения.
            return Result.Failure<ImportedDocument>(
                $"Не удалось прочитать {Format}-файл «{Path.GetFileName(path)}»: {exception.Message}");
        }
    }

    /// <summary>
    /// Извлекает текст документа.
    /// </summary>
    /// <param name="path">Полный путь к файлу.</param>
    /// <param name="notes">Сведения о содержимом, дополняемые чтением.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Текст документа.</returns>
    protected abstract Task<string> ExtractAsync(
        string path,
        List<string> notes,
        CancellationToken cancellationToken);
}

/// <summary>
/// Чтение простого текста и разметки Markdown.
///
/// Markdown читается как есть: его пометки — заголовки, списки, выделение —
/// помогают распознаванию, а не мешают ему.
/// </summary>
internal sealed class PlainTextReader : DocumentReaderBase
{
    /// <inheritdoc />
    public override string Format => "Текст";

    /// <inheritdoc />
    public override IReadOnlyList<string> Extensions { get; } = [".txt", ".md", ".markdown", ".text"];

    /// <inheritdoc />
    protected override async Task<string> ExtractAsync(
        string path,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var (text, encoding) = TextEncodings.Decode(bytes);

        notes.Add($"кодировка: {encoding}");
        notes.Add($"строк: {text.AsSpan().Count('\n') + 1}");

        return text;
    }
}
