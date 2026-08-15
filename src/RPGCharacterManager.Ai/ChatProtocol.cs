using System.Text.Json;
using System.Text.Json.Nodes;
using RPGCharacterManager.Core.Abstractions.Ai;

namespace RPGCharacterManager.Ai;

/// <summary>
/// Перевод запросов и ответов приложения в формат службы языковой модели и обратно.
///
/// Формат совместим с описанием OpenAI и используется большинством служб, поэтому
/// перевод выделен отдельно от передачи данных по сети: подключение другой службы
/// того же формата не требует изменения ни клиента, ни помощника.
/// </summary>
internal static class ChatProtocol
{
    private const string ListSeparator = "; ";

    /// <summary>
    /// Возвращает поле разобранного ответа, если оно есть.
    ///
    /// Обращение к полю значения, не являющегося объектом, — исключение, а не
    /// пустой результат. Ответы служб разнообразнее их описаний: сообщение об
    /// ошибке приходило то объектом, то массивом, и приложение закрывалось.
    /// Поэтому все обращения к полям идут через эту проверку.
    /// </summary>
    /// <param name="element">Разобранный ответ либо его часть.</param>
    /// <param name="name">Имя поля.</param>
    /// <returns>Значение поля либо <see langword="null"/>.</returns>
    private static JsonElement? Field(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value
            : null;

    /// <summary>
    /// Строит тело запроса к службе.
    /// </summary>
    /// <param name="model">Имя модели.</param>
    /// <param name="request">Запрос приложения.</param>
    /// <returns>Тело запроса.</returns>
    public static JsonObject BuildRequest(string model, AiRequest request)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["temperature"] = request.Temperature,
            ["messages"] = BuildMessages(request.Messages),
        };

        if (request.Tools.Count > 0)
        {
            body["tools"] = BuildTools(request.Tools);
            body["tool_choice"] = "auto";
        }

        return body;
    }

    /// <summary>
    /// Читает ответ модели.
    /// </summary>
    /// <param name="root">Корень ответа службы.</param>
    /// <returns>Ответ модели.</returns>
    public static AiReply ReadReply(JsonElement root)
    {
        var text = string.Empty;
        var calls = new List<AiToolCall>();

        if (Field(root, "choices") is { ValueKind: JsonValueKind.Array } choices
            && choices.GetArrayLength() > 0
            && Field(choices[0], "message") is { } message)
        {
            if (Field(message, "content") is { ValueKind: JsonValueKind.String } content)
            {
                text = content.GetString() ?? string.Empty;
            }

            if (Field(message, "tool_calls") is { ValueKind: JsonValueKind.Array } toolCalls)
            {
                calls.AddRange(toolCalls.EnumerateArray().Select(ReadToolCall).OfType<AiToolCall>());
            }
        }

        var cleaned = RemoveReasoning(text);

        // Модель, уместившая весь ответ в рассуждение, не должна оставлять
        // пустой экран: лучше показать её слова, чем ничего.
        if (cleaned.Length == 0 && text.Length > 0)
        {
            cleaned = text
                .Replace("<think>", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("</think>", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();
        }

        return new AiReply(cleaned, calls, ReadUsage(root));
    }

    /// <summary>
    /// Убирает из ответа рассуждения модели.
    ///
    /// Размышляющие модели пишут ход мысли прямо в тексте ответа, помечая его
    /// парой &lt;think&gt;. Показывать это пользователю нельзя: рассуждение занимает
    /// экран целиком и написано не для него, а сама модель к нему не возвращается.
    /// </summary>
    /// <param name="text">Текст ответа модели.</param>
    /// <returns>Текст без рассуждений.</returns>
    public static string RemoveReasoning(string text)
    {
        const string opening = "<think>";
        const string closing = "</think>";

        var result = text;

        while (true)
        {
            var start = result.IndexOf(opening, StringComparison.OrdinalIgnoreCase);

            if (start < 0)
            {
                break;
            }

            var end = result.IndexOf(closing, start, StringComparison.OrdinalIgnoreCase);

            // Незакрытая пара означает, что ответ оборван на рассуждении:
            // показывать нечего, и остаётся только всё, что было до него.
            result = end < 0
                ? result[..start]
                : result[..start] + result[(end + closing.Length)..];
        }

        return result.Trim();
    }

    /// <summary>
    /// Читает список доступных моделей, отбрасывая непригодные для помощника.
    /// </summary>
    /// <param name="root">Корень ответа службы.</param>
    /// <param name="freeOnly">Оставлять только бесплатные модели.</param>
    /// <returns>Имена моделей в алфавитном порядке.</returns>
    public static IReadOnlyList<string> ReadModels(JsonElement root, bool freeOnly = false)
    {
        if (Field(root, "data") is not { ValueKind: JsonValueKind.Array } data)
        {
            return [];
        }

        return data
            .EnumerateArray()
            .Where(item => !freeOnly || IsFree(item))
            .Where(SupportsTools)
            .Select(item => Field(item, "id") is { ValueKind: JsonValueKind.String } id ? id.GetString() : null)
            .OfType<string>()
            .Select(TrimPrefix)
            .Where(IsConversational)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Убирает приставку каталога из имени модели.
    ///
    /// Google AI Studio возвращает имена вида <c>models/gemini-2.5-flash</c>,
    /// а в запросе ждёт имя без приставки.
    /// </summary>
    /// <param name="name">Имя модели, полученное от службы.</param>
    /// <returns>Имя, пригодное для запроса.</returns>
    private static string TrimPrefix(string name)
    {
        const string prefix = "models/";

        return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? name[prefix.Length..]
            : name;
    }

    /// <summary>Признак бесплатной модели в имени.</summary>
    public const string FreeSuffix = ":free";

    /// <summary>
    /// Определяет, бесплатна ли модель.
    /// Служба помечает бесплатные модели окончанием имени и нулевой ценой.
    /// </summary>
    /// <param name="item">Описание модели.</param>
    /// <returns><see langword="true"/>, если модель бесплатна.</returns>
    private static bool IsFree(JsonElement item)
    {
        if (Field(item, "id") is { ValueKind: JsonValueKind.String } id
            && id.GetString() is { } name
            && name.EndsWith(FreeSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Field(item, "pricing") is not { } pricing)
        {
            return false;
        }

        return IsZero(pricing, "prompt") && IsZero(pricing, "completion");
    }

    private static bool IsZero(JsonElement pricing, string name) =>
        Field(pricing, name) is { } value
        && double.TryParse(
            value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var number)
        && number == 0;

    /// <summary>
    /// Определяет, умеет ли модель вызывать инструменты.
    ///
    /// Модель без вызова инструментов помощнику непригодна: она способна только
    /// рассуждать вслух и, что хуже, охотно описывает работу, которую не сделала.
    /// Служба, не сообщающая своих возможностей, считается пригодной.
    /// </summary>
    /// <param name="item">Описание модели.</param>
    /// <returns><see langword="true"/>, если модель пригодна для помощника.</returns>
    private static bool SupportsTools(JsonElement item)
    {
        if (Field(item, "supported_parameters") is not { ValueKind: JsonValueKind.Array } parameters)
        {
            return true;
        }

        return parameters
            .EnumerateArray()
            .Any(value => value.ValueKind == JsonValueKind.String
                && string.Equals(value.GetString(), "tools", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Читает сообщение об ошибке, возвращённое службой.
    /// </summary>
    /// <param name="body">Тело ответа.</param>
    /// <returns>Сообщение об ошибке или <see langword="null"/>.</returns>
    public static string? ReadError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            return ReadErrorMessage(document.RootElement);
        }
        catch (JsonException)
        {
            // Служба вернула не JSON: показывать пользователю нечего.
            return null;
        }
    }

    /// <summary>
    /// Достаёт сообщение об ошибке из ответа службы.
    ///
    /// Единого вида у сообщений об ошибках нет: одни службы отвечают объектом
    /// с полем error, Google AI Studio оборачивает такой объект в массив, а поле
    /// error иногда оказывается просто строкой. Читаются все три вида, а всё,
    /// что не разобрано, считается отсутствием сообщения — но не поводом
    /// прервать работу приложения.
    /// </summary>
    /// <param name="element">Разобранный ответ либо его часть.</param>
    /// <returns>Сообщение об ошибке или <see langword="null"/>.</returns>
    private static string? ReadErrorMessage(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element
                .EnumerateArray()
                .Select(ReadErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));
        }

        if (Field(element, "error") is not { } error)
        {
            return null;
        }

        return error.ValueKind switch
        {
            JsonValueKind.String => error.GetString(),
            JsonValueKind.Object => Field(error, "message") is { ValueKind: JsonValueKind.String } message
                ? message.GetString()
                : null,
            _ => null,
        };
    }

    /// <summary>
    /// Разбирает аргументы вызова инструмента.
    /// </summary>
    /// <param name="raw">Текст аргументов, полученный от модели.</param>
    /// <returns>Разобранные аргументы.</returns>
    public static AiToolArguments ParseArguments(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return AiToolArguments.Empty;
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var maps = new Dictionary<string, IReadOnlyDictionary<string, string?>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var document = JsonDocument.Parse(raw);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return AiToolArguments.Empty;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (TryReadMap(property.Value, out var map))
                {
                    maps[property.Name] = map;
                    continue;
                }

                values[property.Name] = ToText(property.Value);
            }
        }
        catch (JsonException)
        {
            // Модель вернула аргументы в неразборчивом виде: инструмент получит
            // пустой набор и сам сообщит, какого параметра не хватает.
            return AiToolArguments.Empty;
        }

        return new AiToolArguments(raw, values, maps);
    }

    private static JsonArray BuildMessages(IReadOnlyList<AiMessage> messages)
    {
        var array = new JsonArray();

        foreach (var message in messages)
        {
            array.Add(BuildMessage(message));
        }

        return array;
    }

    private static JsonObject BuildMessage(AiMessage message)
    {
        var item = new JsonObject { ["role"] = RoleOf(message.Role) };

        if (message.Role == AiRole.Tool)
        {
            item["tool_call_id"] = message.CallId;
            item["content"] = message.Text;

            return item;
        }

        // Сообщение с вызовами инструментов может не содержать текста: служба
        // ожидает его отсутствия, а не пустой строки.
        if (message.Calls.Count == 0 || message.Text.Length > 0)
        {
            item["content"] = message.Text;
        }

        if (message.Calls.Count > 0)
        {
            var calls = new JsonArray();

            foreach (var call in message.Calls)
            {
                calls.Add(new JsonObject
                {
                    ["id"] = call.Id,
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = call.Name,
                        ["arguments"] = call.Arguments.Raw,
                    },
                });
            }

            item["tool_calls"] = calls;
        }

        return item;
    }

    private static JsonArray BuildTools(IReadOnlyList<AiToolSpec> tools)
    {
        var array = new JsonArray();

        foreach (var tool in tools)
        {
            array.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = BuildSchema(tool.Parameters),
                },
            });
        }

        return array;
    }

    private static JsonObject BuildSchema(IReadOnlyList<AiToolParameter> parameters)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var parameter in parameters)
        {
            properties[parameter.Name] = BuildParameter(parameter);

            if (parameter.IsRequired)
            {
                required.Add(parameter.Name);
            }
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return schema;
    }

    private static JsonObject BuildParameter(AiToolParameter parameter) => parameter.Kind switch
    {
        AiParameterKind.Number => new JsonObject
        {
            ["type"] = "integer",
            ["description"] = parameter.Description,
        },

        AiParameterKind.Flag => new JsonObject
        {
            ["type"] = "boolean",
            ["description"] = parameter.Description,
        },

        AiParameterKind.Map => new JsonObject
        {
            ["type"] = "object",
            ["description"] = parameter.Description,
            ["additionalProperties"] = new JsonObject { ["type"] = "string" },
        },

        _ => new JsonObject
        {
            ["type"] = "string",
            ["description"] = parameter.Description,
        },
    };

    private static AiToolCall? ReadToolCall(JsonElement element)
    {
        if (Field(element, "function") is not { } function
            || Field(function, "name") is not { ValueKind: JsonValueKind.String } name
            || name.GetString() is not { Length: > 0 } toolName)
        {
            return null;
        }

        var id = Field(element, "id") is { ValueKind: JsonValueKind.String } identifier
            ? identifier.GetString() ?? string.Empty
            : string.Empty;

        var raw = Field(function, "arguments") is { ValueKind: JsonValueKind.String } arguments
            ? arguments.GetString()
            : null;

        return new AiToolCall(id, toolName, ParseArguments(raw));
    }

    private static AiUsage ReadUsage(JsonElement root)
    {
        if (Field(root, "usage") is not { } usage)
        {
            return default;
        }

        return new AiUsage(ReadCount(usage, "prompt_tokens"), ReadCount(usage, "completion_tokens"));
    }

    private static int ReadCount(JsonElement usage, string name) =>
        Field(usage, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var count)
            ? count
            : 0;

    private static bool TryReadMap(JsonElement value, out IReadOnlyDictionary<string, string?> map)
    {
        // Набор полей модель иногда возвращает не объектом, а строкой с записью
        // объекта внутри. Оба вида должны читаться одинаково.
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();

            if (text is not null && text.TrimStart().StartsWith('{'))
            {
                try
                {
                    using var document = JsonDocument.Parse(text);

                    return TryReadMap(document.RootElement, out map);
                }
                catch (JsonException)
                {
                    map = new Dictionary<string, string?>();
                    return false;
                }
            }
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            map = new Dictionary<string, string?>();
            return false;
        }

        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in value.EnumerateObject())
        {
            result[property.Name] = ToText(property.Value);
        }

        map = result;
        return true;
    }

    private static string? ToText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "да",
        JsonValueKind.False => "нет",
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.Array => string.Join(
            ListSeparator,
            value.EnumerateArray().Select(ToText).Where(item => !string.IsNullOrWhiteSpace(item))),
        _ => value.GetRawText(),
    };

    private static string RoleOf(AiRole role) => role switch
    {
        AiRole.System => "system",
        AiRole.User => "user",
        AiRole.Tool => "tool",
        _ => "assistant",
    };

    /// <summary>
    /// Определяет, ведёт ли модель переписку.
    /// Службы распознавания речи и синтеза звука отвечают тем же перечнем моделей,
    /// но для помощника непригодны.
    /// </summary>
    /// <param name="name">Имя модели.</param>
    /// <returns><see langword="true"/>, если модель пригодна для переписки.</returns>
    private static bool IsConversational(string name)
    {
        // Распознавание речи, синтез звука, рисование, видео и построение
        // векторов текста возвращаются тем же перечнем, но переписку не ведут.
        string[] excluded = ["whisper", "tts", "guard", "embed", "image", "imagen", "veo", "vision"];

        return !excluded.Any(part =>
            name.Contains(part, StringComparison.OrdinalIgnoreCase));
    }
}
