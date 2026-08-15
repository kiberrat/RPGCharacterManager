using System.Text;
using UglyToad.PdfPig;

namespace RPGCharacterManager.Import.Readers;

/// <summary>
/// Чтение книг в формате PDF — основной способ переноса правил в приложение.
///
/// Текст в PDF хранится кусками с собственными координатами, а не строками,
/// поэтому извлечение выполняет библиотека: она восстанавливает порядок слов
/// и раскрывает кодировки шрифтов.
/// </summary>
internal sealed class PdfReader : DocumentReaderBase
{
    /// <inheritdoc />
    public override string Format => "PDF";

    /// <inheritdoc />
    public override IReadOnlyList<string> Extensions { get; } = [".pdf"];

    /// <inheritdoc />
    protected override Task<string> ExtractAsync(
        string path,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(path);

        var builder = new StringBuilder();
        var pages = 0;
        var empty = 0;

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            pages++;

            var text = page.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                empty++;
                continue;
            }

            builder.AppendLine(text);
            builder.AppendLine();
        }

        notes.Add($"страниц: {pages}");

        if (empty > 0)
        {
            // Страница без текста — обычно скан: буквы на ней нарисованы,
            // а не записаны, и распознать их приложение не умеет.
            notes.Add($"страниц без текста: {empty} — возможно, это сканы");
        }

        return Task.FromResult(builder.ToString());
    }
}
