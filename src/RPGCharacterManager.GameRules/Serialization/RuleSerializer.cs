using System.Text.Json;
using System.Text.Json.Serialization;
using RPGCharacterManager.Core.Abstractions.Rules;

namespace RPGCharacterManager.GameRules.Serialization;

/// <summary>
/// Преобразование условий и действий правила в текст и обратно.
///
/// Документ 004_База_данных.md описывает поля <c>Condition</c> и <c>Action</c> таблицы
/// правил как текстовые, поэтому дерево условий и список действий сохраняются в JSON.
/// Формат человекочитаем: правило можно прочитать и перенести без приложения.
/// </summary>
public static class RuleSerializer
{
    private const string TypeProperty = "тип";
    private const string GroupTypeName = "группа";
    private const string ComparisonTypeName = "сравнение";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Converters = { new RuleConditionConverter(), new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Преобразует дерево условий в текст.
    /// </summary>
    /// <param name="condition">Дерево условий или <see langword="null"/>.</param>
    /// <returns>Текстовое представление или <see langword="null"/>, если условий нет.</returns>
    public static string? SerializeCondition(RuleCondition? condition) =>
        condition is null ? null : JsonSerializer.Serialize(condition, Options);

    /// <summary>
    /// Восстанавливает дерево условий из текста.
    /// Повреждённое значение не приводит к исключению: правило считается безусловным,
    /// а проверка правил сообщит о проблеме пользователю.
    /// </summary>
    /// <param name="value">Текстовое представление.</param>
    /// <returns>Дерево условий или <see langword="null"/>.</returns>
    public static RuleCondition? DeserializeCondition(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RuleCondition>(value, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Преобразует список действий в текст.
    /// </summary>
    /// <param name="actions">Список действий.</param>
    /// <returns>Текстовое представление.</returns>
    public static string SerializeActions(IEnumerable<RuleAction> actions) =>
        JsonSerializer.Serialize(actions, Options);

    /// <summary>
    /// Восстанавливает список действий из текста.
    /// </summary>
    /// <param name="value">Текстовое представление.</param>
    /// <returns>Список действий; пустой список при отсутствии или повреждении данных.</returns>
    public static List<RuleAction> DeserializeActions(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<RuleAction>>(value, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Преобразователь дерева условий с сохранением вида каждого узла.
    /// </summary>
    private sealed class RuleConditionConverter : JsonConverter<RuleCondition>
    {
        /// <inheritdoc />
        public override RuleCondition? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return ReadNode(document.RootElement);
        }

        /// <inheritdoc />
        public override void Write(
            Utf8JsonWriter writer,
            RuleCondition value,
            JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);

            switch (value)
            {
                case RuleConditionGroup group:
                    WriteGroup(writer, group);
                    break;

                case RuleComparison comparison:
                    WriteComparison(writer, comparison);
                    break;

                default:
                    throw new JsonException($"Неизвестный вид условия: {value?.GetType().Name}");
            }
        }

        private static void WriteGroup(Utf8JsonWriter writer, RuleConditionGroup group)
        {
            writer.WriteStartObject();
            writer.WriteString(TypeProperty, GroupTypeName);
            writer.WriteString("оператор", group.Operator.ToString());
            writer.WriteBoolean("отрицание", group.IsNegated);
            writer.WritePropertyName("условия");
            writer.WriteStartArray();

            foreach (var child in group.Children)
            {
                switch (child)
                {
                    case RuleConditionGroup nested:
                        WriteGroup(writer, nested);
                        break;

                    case RuleComparison comparison:
                        WriteComparison(writer, comparison);
                        break;

                    default:
                        throw new JsonException($"Неизвестный вид условия: {child?.GetType().Name}");
                }
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteComparison(Utf8JsonWriter writer, RuleComparison comparison)
        {
            writer.WriteStartObject();
            writer.WriteString(TypeProperty, ComparisonTypeName);
            writer.WriteString("слева", comparison.Left);
            writer.WriteString("оператор", comparison.Operator.ToString());
            writer.WriteString("справа", comparison.Right);
            writer.WriteEndObject();
        }

        private static RuleCondition? ReadNode(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var typeName = element.TryGetProperty(TypeProperty, out var typeElement)
                ? typeElement.GetString()
                : ComparisonTypeName;

            return string.Equals(typeName, GroupTypeName, StringComparison.Ordinal)
                ? ReadGroup(element)
                : ReadComparison(element);
        }

        private static RuleConditionGroup ReadGroup(JsonElement element)
        {
            var group = new RuleConditionGroup
            {
                Operator = ReadEnum(element, "оператор", RuleLogicalOperator.And),
                IsNegated = element.TryGetProperty("отрицание", out var negated)
                    && negated.ValueKind == JsonValueKind.True,
            };

            if (element.TryGetProperty("условия", out var children)
                && children.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in children.EnumerateArray())
                {
                    var node = ReadNode(child);

                    if (node is not null)
                    {
                        group.Children.Add(node);
                    }
                }
            }

            return group;
        }

        private static RuleComparison ReadComparison(JsonElement element) => new()
        {
            Left = ReadString(element, "слева"),
            Operator = ReadEnum(element, "оператор", RuleComparisonOperator.Equal),
            Right = ReadString(element, "справа"),
        };

        private static string ReadString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) ? value.GetString() ?? string.Empty : string.Empty;

        private static TEnum ReadEnum<TEnum>(JsonElement element, string propertyName, TEnum fallback)
            where TEnum : struct, Enum =>
            element.TryGetProperty(propertyName, out var value)
            && Enum.TryParse<TEnum>(value.GetString(), ignoreCase: true, out var parsed)
                ? parsed
                : fallback;
    }
}
