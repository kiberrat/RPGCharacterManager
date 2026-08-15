using System.Text.Json;
using RPGCharacterManager.Ai;
using RPGCharacterManager.Core.Abstractions.Ai;

namespace RPGCharacterManager.Tests.Ai;

/// <summary>
/// Проверка перевода запросов и ответов в формат службы языковой модели.
///
/// Ошибка перевода не видна ни компилятору, ни тестам служб: она проявляется
/// только в отказе службы. Поэтому формат проверяется отдельно и целиком.
/// </summary>
public sealed class ChatProtocolTests
{
    private static JsonElement Build(AiRequest request)
    {
        var text = ChatProtocol.BuildRequest("модель", request).ToJsonString();

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    [Fact]
    public void Запрос_СИнструментом_ОписываетПараметрыСхемой()
    {
        var tool = new AiToolSpec(
            "create_object",
            "Создаёт объект.",
            [
                new("type", "Вид объектов.", AiParameterKind.Text, IsRequired: true),
                new("values", "Значения полей.", AiParameterKind.Map, IsRequired: true),
                new("limit", "Сколько записей.", AiParameterKind.Number),
            ]);

        var body = Build(new AiRequest([AiMessage.User("Создай")]) { Tools = [tool] });

        var function = body.GetProperty("tools")[0].GetProperty("function");
        var schema = function.GetProperty("parameters");
        var properties = schema.GetProperty("properties");

        Assert.Equal("create_object", function.GetProperty("name").GetString());
        Assert.Equal("string", properties.GetProperty("type").GetProperty("type").GetString());
        Assert.Equal("integer", properties.GetProperty("limit").GetProperty("type").GetString());

        // Набор полей передаётся объектом со строковыми значениями: именно так
        // модель может назвать любое поле любого вида контента.
        var values = properties.GetProperty("values");
        Assert.Equal("object", values.GetProperty("type").GetString());
        Assert.Equal("string", values.GetProperty("additionalProperties").GetProperty("type").GetString());

        var required = schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToList();
        Assert.Contains("type", required);
        Assert.Contains("values", required);
        Assert.DoesNotContain("limit", required);
    }

    [Fact]
    public void Запрос_БезИнструментов_НеСодержитРазделаИнструментов()
    {
        var body = Build(new AiRequest([AiMessage.User("Привет")]));

        Assert.False(body.TryGetProperty("tools", out _));
        Assert.Equal("user", body.GetProperty("messages")[0].GetProperty("role").GetString());
    }

    [Fact]
    public void Запрос_ОтветИнструмента_СвязанСВызовом()
    {
        var call = new AiToolCall("вызов-7", "find_objects", ChatProtocol.ParseArguments("""{"type":"spells"}"""));

        var body = Build(new AiRequest(
        [
            AiMessage.Assistant(string.Empty, [call]),
            AiMessage.Tool("вызов-7", "find_objects", "Найдено объектов: 0."),
        ]));

        var assistant = body.GetProperty("messages")[0];
        var tool = body.GetProperty("messages")[1];

        // Сообщение с вызовом не содержит пустого текста: служба его не принимает.
        Assert.False(assistant.TryGetProperty("content", out _));
        Assert.Equal("вызов-7", assistant.GetProperty("tool_calls")[0].GetProperty("id").GetString());

        Assert.Equal("tool", tool.GetProperty("role").GetString());
        Assert.Equal("вызов-7", tool.GetProperty("tool_call_id").GetString());
    }

    [Fact]
    public void Ответ_СВызовомИнструмента_Разбирается()
    {
        const string body = """
            {
              "choices": [{
                "message": {
                  "content": null,
                  "tool_calls": [{
                    "id": "вызов-3",
                    "function": { "name": "describe_type", "arguments": "{\"type\":\"spells\"}" }
                  }]
                }
              }],
              "usage": { "prompt_tokens": 120, "completion_tokens": 30 }
            }
            """;

        using var document = JsonDocument.Parse(body);
        var reply = ChatProtocol.ReadReply(document.RootElement);

        var call = Assert.Single(reply.Calls);

        Assert.Equal("describe_type", call.Name);
        Assert.Equal("spells", call.Arguments.Text("type"));
        Assert.Equal(150, reply.Usage.Total);
    }

    [Fact]
    public void Аргументы_НаборПолей_Читается()
    {
        var arguments = ChatProtocol.ParseArguments(
            """{"type":"spells","values":{"name":"Ледяная стрела","level":3,"ritual":true}}""");

        var values = arguments.Map("values");

        Assert.Equal("spells", arguments.Text("type"));
        Assert.Equal("Ледяная стрела", values["name"]);
        Assert.Equal("3", values["level"]);
        Assert.Equal("да", values["ritual"]);
    }

    [Fact]
    public void Аргументы_НаборПолейСтрокой_ЧитаетсяТакЖе()
    {
        // Модель нередко присылает объект строкой. Оба вида должны работать,
        // иначе создание объекта срывалось бы через раз без видимой причины.
        var arguments = ChatProtocol.ParseArguments(
            """{"type":"weapons","values":"{\"name\":\"Кинжал\"}"}""");

        Assert.Equal("Кинжал", arguments.Map("values")["name"]);
    }

    [Fact]
    public void Аргументы_МассивЗначений_СтановитсяПеречислением()
    {
        var arguments = ChatProtocol.ParseArguments("""{"values":{"components":["В","С","М"]}}""");

        Assert.Equal("В; С; М", arguments.Map("values")["components"]);
    }

    [Fact]
    public void Аргументы_НеразборчиваяЗапись_ДаётПустойНабор()
    {
        var arguments = ChatProtocol.ParseArguments("{это не json");

        Assert.Null(arguments.Text("type"));
        Assert.Empty(arguments.Map("values"));
    }

    [Fact]
    public void Ошибка_СообщениеСлужбы_Читается()
    {
        var message = ChatProtocol.ReadError("""{"error":{"message":"model not found"}}""");

        Assert.Equal("model not found", message);
        Assert.Null(ChatProtocol.ReadError("не json"));
    }

    [Fact]
    public void Ошибка_ОтветМассивом_ЧитаетсяАНеРоняетПриложение()
    {
        // Google AI Studio оборачивает сообщение об ошибке в массив. Обращение
        // к полю массива — исключение, из-за которого приложение закрывалось
        // при проверке связи.
        const string body = """
            [{ "error": { "code": 400, "message": "API key not valid", "status": "INVALID_ARGUMENT" } }]
            """;

        Assert.Equal("API key not valid", ChatProtocol.ReadError(body));
    }

    [Theory]
    [InlineData("""{"error":"просто строка"}""", "просто строка")]
    [InlineData("""{"error":{"code":400}}""", null)]
    [InlineData("""{"error":[1,2]}""", null)]
    [InlineData("[]", null)]
    [InlineData("[1,2,3]", null)]
    [InlineData("\"строка вместо ответа\"", null)]
    [InlineData("123", null)]
    public void Ошибка_НеожиданныйВидОтвета_НеБросаетИсключения(string body, string? expected) =>
        Assert.Equal(expected, ChatProtocol.ReadError(body));

    [Fact]
    public void Ответ_НеОбъект_ЧитаетсяКакПустой()
    {
        using var array = JsonDocument.Parse("[{\"choices\":[]}]");

        var reply = ChatProtocol.ReadReply(array.RootElement);

        Assert.Empty(reply.Text);
        Assert.Empty(reply.Calls);
        Assert.Empty(ChatProtocol.ReadModels(array.RootElement));
    }

    [Fact]
    public void Модели_СлужебныеМодели_НеПопадаютВСписок()
    {
        const string body = """
            { "data": [
                { "id": "llama-3.3-70b-versatile" },
                { "id": "whisper-large-v3" },
                { "id": "meta-llama/llama-guard-4-12b" }
            ] }
            """;

        using var document = JsonDocument.Parse(body);
        var models = ChatProtocol.ReadModels(document.RootElement);

        Assert.Equal(["llama-3.3-70b-versatile"], models);
    }

    [Fact]
    public void Модели_БезВызоваИнструментов_НеПопадаютВСписок()
    {
        // Модель, не умеющая вызывать инструменты, помощнику бесполезна и вредна:
        // она охотно описывает работу, которой не сделала.
        const string body = """
            { "data": [
                { "id": "хорошая", "supported_parameters": ["tools", "temperature"] },
                { "id": "болтливая", "supported_parameters": ["temperature"] },
                { "id": "неизвестная" }
            ] }
            """;

        using var document = JsonDocument.Parse(body);
        var models = ChatProtocol.ReadModels(document.RootElement);

        Assert.Equal(["неизвестная", "хорошая"], models);
    }

    [Fact]
    public void Модели_ТолькоБесплатные_ОтбираютсяПоИмениИЦене()
    {
        const string body = """
            { "data": [
                { "id": "автор/модель:free", "supported_parameters": ["tools"] },
                { "id": "автор/платная", "supported_parameters": ["tools"],
                  "pricing": { "prompt": "0.0000012", "completion": "0.0000024" } },
                { "id": "автор/нулевая", "supported_parameters": ["tools"],
                  "pricing": { "prompt": "0", "completion": "0" } }
            ] }
            """;

        using var document = JsonDocument.Parse(body);

        Assert.Equal(
            ["автор/модель:free", "автор/нулевая"],
            ChatProtocol.ReadModels(document.RootElement, freeOnly: true));

        Assert.Equal(3, ChatProtocol.ReadModels(document.RootElement).Count);
    }

    [Fact]
    public void Ответ_СРассуждениямиМодели_ПоказываетТолькоИтог()
    {
        const string body = """
            {
              "choices": [{ "message": {
                "content": "<think>Сначала подумаю, какой уровень выбрать.</think>Готово: заклинание первого уровня."
              }}]
            }
            """;

        using var document = JsonDocument.Parse(body);
        var reply = ChatProtocol.ReadReply(document.RootElement);

        Assert.Equal("Готово: заклинание первого уровня.", reply.Text);
    }

    [Fact]
    public void Ответ_ОборванныйНаРассуждении_НеПоказываетЕго()
    {
        Assert.Equal("Начало.", ChatProtocol.RemoveReasoning("Начало.<think>Незакрытая мысль"));
        Assert.Equal(string.Empty, ChatProtocol.RemoveReasoning("<think>Только мысль"));
        Assert.Equal("Ответ.", ChatProtocol.RemoveReasoning("Ответ."));
    }

    [Fact]
    public void Ответ_ЦеликомВнутриРассуждения_НеОставляетПустойЭкран()
    {
        const string body = """
            { "choices": [{ "message": { "content": "<think>Весь ответ оказался здесь.</think>" }}] }
            """;

        using var document = JsonDocument.Parse(body);
        var reply = ChatProtocol.ReadReply(document.RootElement);

        Assert.Equal("Весь ответ оказался здесь.", reply.Text);
    }

    [Fact]
    public void Модели_ИмяСПриставкойКаталога_Сокращается()
    {
        // Google AI Studio возвращает имена вида models/gemini-2.5-flash,
        // а в запросе ждёт имя без приставки.
        const string body = """
            { "data": [
                { "id": "models/gemini-2.5-flash" },
                { "id": "models/gemini-2.5-flash-image" },
                { "id": "models/veo-3.1-generate-preview" },
                { "id": "models/text-embedding-004" }
            ] }
            """;

        using var document = JsonDocument.Parse(body);

        Assert.Equal(["gemini-2.5-flash"], ChatProtocol.ReadModels(document.RootElement));
    }
}
