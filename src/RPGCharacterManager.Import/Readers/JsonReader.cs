using System.Text;
using System.Text.Json;

namespace RPGCharacterManager.Import.Readers;

/// <summary>
/// Чтение наборов записей в формате JSON.
///
/// Записи не пересказываются исходным видом: скобки, запятые и кавычки занимают
/// место и мешают распознаванию. Вместо этого объект превращается в перечень
/// «поле: значение» — тот же вид, в котором объекты описаны в книге правил,
/// поэтому распознавание не отличает одно от другого.
/// </summary>
internal sealed class JsonReader : DocumentReaderBase
{
    /// <summary>Отступ одного уровня вложенности.</summary>
    private const string Indent = "  ";

    /// <summary>Глубина, дальше которой вложенность не разворачивается.</summary>
    private const int MaximumDepth = 8;

    /// <inheritdoc />
    public override string Format => "JSON";

    /// <inheritdoc />
    public override IReadOnlyList<string> Extensions { get; } = [".json"];

    /// <inheritdoc />
    protected override async Task<string> ExtractAsync(
        string path,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var (content, encoding) = TextEncodings.Decode(bytes);

        notes.Add($"кодировка: {encoding}");

        using var document = JsonDocument.Parse(content, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });

        var builder = new StringBuilder();
        var records = 0;

        Write(document.RootElement, builder, 0, ref records);

        notes.Add($"записей: {records}");

        return builder.ToString();
    }

    /// <summary>
    /// Записывает значение перечнем «поле: значение».
    /// </summary>
    /// <param name="element">Значение JSON.</param>
    /// <param name="builder">Собираемый текст.</param>
    /// <param name="depth">Текущая глубина вложенности.</param>
    /// <param name="records">Счётчик записей.</param>
    private static void Write(JsonElement element, StringBuilder builder, int depth, ref int records)
    {
        if (depth > MaximumDepth)
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                records++;

                foreach (var property in element.EnumerateObject())
                {
                    WriteNamed(property.Name, property.Value, builder, depth, ref records);
                }

                break;

            case JsonValueKind.Array:
                var index = 0;

                foreach (var item in element.EnumerateArray())
                {
                    index++;

                    if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        builder.AppendLine();
                        Write(item, builder, depth, ref records);
                    }
                    else
                    {
                        WriteNamed(index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            item, builder, depth, ref records);
                    }
                }

                break;

            default:
                builder.Append(Prefix(depth)).AppendLine(Scalar(element));
                break;
        }
    }

    /// <summary>
    /// Записывает поле с его значением.
    /// </summary>
    /// <param name="name">Имя поля.</param>
    /// <param name="value">Значение поля.</param>
    /// <param name="builder">Собираемый текст.</param>
    /// <param name="depth">Текущая глубина вложенности.</param>
    /// <param name="records">Счётчик записей.</param>
    private static void WriteNamed(
        string name,
        JsonElement value,
        StringBuilder builder,
        int depth,
        ref int records)
    {
        if (value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            builder.Append(Prefix(depth)).Append(name).AppendLine(":");
            Write(value, builder, depth + 1, ref records);

            return;
        }

        builder.Append(Prefix(depth)).Append(name).Append(": ").AppendLine(Scalar(value));
    }

    private static string Prefix(int depth) => string.Concat(Enumerable.Repeat(Indent, depth));

    private static string Scalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.True => "да",
        JsonValueKind.False => "нет",
        JsonValueKind.Null => "—",
        _ => element.GetRawText(),
    };
}
