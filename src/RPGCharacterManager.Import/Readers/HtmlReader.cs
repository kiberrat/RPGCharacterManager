using System.Net;
using System.Text;
using RPGCharacterManager.Core.Abstractions.Import;

namespace RPGCharacterManager.Import.Readers;

/// <summary>
/// Чтение веб-страниц.
///
/// Из разметки берётся только видимый текст: содержимое скриптов и стилей
/// не относится к игре, а перечисление имён свойств оформления сбивает
/// распознавание не хуже случайного шума.
/// </summary>
internal sealed class HtmlReader : DocumentReaderBase
{
    /// <summary>Разделы, содержимое которых пользователю не показывается.</summary>
    private static readonly string[] HiddenSections = ["script", "style", "head", "svg"];

    /// <summary>Разделы, после которых текст продолжается с новой строки.</summary>
    private static readonly string[] BlockTags =
    [
        "p", "div", "br", "li", "tr", "h1", "h2", "h3", "h4", "h5", "h6",
        "table", "section", "article", "blockquote", "pre",
    ];

    /// <inheritdoc />
    public override string Format => "HTML";

    /// <inheritdoc />
    public override IReadOnlyList<string> Extensions { get; } = [".html", ".htm", ".xhtml"];

    /// <inheritdoc />
    protected override async Task<string> ExtractAsync(
        string path,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var (markup, encoding) = TextEncodings.Decode(bytes);

        notes.Add($"кодировка: {encoding}");

        return Strip(markup, notes);
    }

    /// <summary>
    /// Убирает разметку, оставляя видимый текст.
    /// </summary>
    /// <param name="markup">Исходная разметка.</param>
    /// <param name="notes">Сведения о содержимом.</param>
    /// <returns>Видимый текст страницы.</returns>
    private static string Strip(string markup, List<string> notes)
    {
        var builder = new StringBuilder(markup.Length);
        var position = 0;
        var tags = 0;

        while (position < markup.Length)
        {
            var open = markup.IndexOf('<', position);

            if (open < 0)
            {
                builder.Append(markup, position, markup.Length - position);
                break;
            }

            builder.Append(markup, position, open - position);

            var close = markup.IndexOf('>', open);

            if (close < 0)
            {
                // Незакрытая скобка: дальше разметки нет, остаток — текст.
                builder.Append(markup, open, markup.Length - open);
                break;
            }

            var tag = markup[(open + 1)..close];
            var name = TagName(tag);

            tags++;

            if (HiddenSections.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                position = SkipSection(markup, close + 1, name);
                continue;
            }

            if (BlockTags.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine();
            }

            position = close + 1;
        }

        notes.Add($"разобрано тегов: {tags}");

        return Collapse(WebUtility.HtmlDecode(builder.ToString()));
    }

    /// <summary>
    /// Возвращает имя тега без косой черты и свойств.
    /// </summary>
    /// <param name="tag">Содержимое угловых скобок.</param>
    /// <returns>Имя тега.</returns>
    private static string TagName(string tag)
    {
        var start = 0;

        while (start < tag.Length && (tag[start] == '/' || tag[start] == '!'))
        {
            start++;
        }

        var end = start;

        while (end < tag.Length && !char.IsWhiteSpace(tag[end]) && tag[end] != '/')
        {
            end++;
        }

        return tag[start..end];
    }

    /// <summary>
    /// Пропускает раздел вместе с его содержимым.
    /// </summary>
    /// <param name="markup">Исходная разметка.</param>
    /// <param name="position">Позиция сразу после открывающего тега.</param>
    /// <param name="name">Имя раздела.</param>
    /// <returns>Позиция сразу после закрывающего тега.</returns>
    private static int SkipSection(string markup, int position, string name)
    {
        var closing = markup.IndexOf($"</{name}", position, StringComparison.OrdinalIgnoreCase);

        if (closing < 0)
        {
            return markup.Length;
        }

        var end = markup.IndexOf('>', closing);

        return end < 0 ? markup.Length : end + 1;
    }

    /// <summary>
    /// Убирает лишние пробелы и пустые строки, оставшиеся от разметки.
    /// </summary>
    /// <param name="text">Текст с остатками отступов разметки.</param>
    /// <returns>Читаемый текст.</returns>
    private static string Collapse(string text)
    {
        var builder = new StringBuilder(text.Length);
        var blankLines = 0;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                blankLines++;

                // Разметка оставляет после себя десятки пустых строк подряд;
                // одна пустая строка разделяет абзацы, остальные — мусор.
                if (blankLines > 1)
                {
                    continue;
                }
            }
            else
            {
                blankLines = 0;
            }

            builder.AppendLine(trimmed);
        }

        return builder.ToString();
    }
}
