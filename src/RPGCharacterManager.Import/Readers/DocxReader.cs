using System.IO.Compression;
using System.Text;
using System.Xml;

namespace RPGCharacterManager.Import.Readers;

/// <summary>
/// Чтение документов Word.
///
/// Файл DOCX — это ZIP-архив с разметкой внутри, поэтому средств платформы
/// достаточно и сторонняя библиотека не нужна. Из разметки берутся куски текста
/// и границы абзацев; оформление, стили и разметка правок отбрасываются —
/// распознаванию объектов они не помогают.
/// </summary>
internal sealed class DocxReader : DocumentReaderBase
{
    /// <summary>Пространство имён текстовой части документа.</summary>
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>Имя части архива, содержащей текст документа.</summary>
    private const string DocumentPart = "word/document.xml";

    /// <inheritdoc />
    public override string Format => "DOCX";

    /// <inheritdoc />
    public override IReadOnlyList<string> Extensions { get; } = [".docx"];

    /// <inheritdoc />
    protected override async Task<string> ExtractAsync(
        string path,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        await using var file = File.OpenRead(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);

        var part = archive.GetEntry(DocumentPart)
            ?? throw new InvalidDataException(
                "внутри нет части «word/document.xml» — возможно, это файл старого формата DOC.");

        await using var content = part.Open();

        return Read(content, notes);
    }

    /// <summary>
    /// Читает текст из разметки документа.
    /// </summary>
    /// <param name="content">Поток с разметкой.</param>
    /// <param name="notes">Сведения о содержимом.</param>
    /// <returns>Текст документа.</returns>
    private static string Read(Stream content, List<string> notes)
    {
        var builder = new StringBuilder();
        var paragraphs = 0;
        var tables = 0;

        using var reader = XmlReader.Create(content, new XmlReaderSettings { IgnoreWhitespace = false });

        while (reader.Read())
        {
            if (reader.NamespaceURI != WordNamespace)
            {
                continue;
            }

            switch (reader.NodeType, reader.LocalName)
            {
                case (XmlNodeType.Element, "t"):
                    builder.Append(reader.ReadElementContentAsString());
                    break;

                // Разрыв строки внутри абзаца и позиция табуляции разделяют
                // сведения не хуже конца абзаца: «Урон<tab>2d6» без них слиплось бы.
                case (XmlNodeType.Element, "br"):
                case (XmlNodeType.Element, "tab"):
                    builder.Append(' ');
                    break;

                case (XmlNodeType.EndElement, "p"):
                    builder.AppendLine();
                    paragraphs++;
                    break;

                case (XmlNodeType.EndElement, "tr"):
                    builder.AppendLine();
                    break;

                case (XmlNodeType.Element, "tbl"):
                    tables++;
                    break;

                default:
                    break;
            }
        }

        notes.Add($"абзацев: {paragraphs}");

        if (tables > 0)
        {
            notes.Add($"таблиц: {tables}");
        }

        return builder.ToString();
    }
}
