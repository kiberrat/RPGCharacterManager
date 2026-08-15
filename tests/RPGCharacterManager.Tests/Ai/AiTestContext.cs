using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Ai;
using RPGCharacterManager.Ai.Tools;
using RPGCharacterManager.Content;
using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Core.Models.Settings;
using RPGCharacterManager.Shared.Results;
using RPGCharacterManager.Tests.Characters;
using RPGCharacterManager.Tests.Dice;

namespace RPGCharacterManager.Tests.Ai;

/// <summary>
/// Клиент службы языковой модели, отвечающий заранее заданной последовательностью.
///
/// Помощник проверяется целиком, вплоть до записи в базу, но без обращения к сети:
/// поведение модели задаётся тестом, а всё остальное — настоящее.
/// </summary>
internal sealed class ScriptedAiClient : IAiClient
{
    private readonly Queue<AiReply> _replies;

    public ScriptedAiClient(params AiReply[] replies) => _replies = new Queue<AiReply>(replies);

    /// <summary>Запросы, отправленные помощником.</summary>
    public List<AiRequest> Requests { get; } = [];

    public bool IsConfigured { get; set; } = true;

    public string Model => "проверочная модель";

    public AiServiceInfo Service { get; } = new("Проверка", "example.test", false, "Служба для тестов.");

    public IReadOnlyList<string> RecommendedModels { get; } = ["проверочная модель"];

    public Task<Result<AiReply>> CompleteAsync(
        AiRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);

        var reply = _replies.Count > 0
            ? _replies.Dequeue()
            : new AiReply("Больше сказать нечего.", [], default);

        return Task.FromResult(Result.Success(reply));
    }

    public Task<Result<AiConnection>> CheckAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new AiConnection(Model, 1, TimeSpan.Zero, "Работает")));

    public Task<Result<IReadOnlyList<string>>> GetModelsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(RecommendedModels));
}

/// <summary>
/// Подсистема помощника, собранная поверх временной базы данных.
/// Служба контента, движок формул и службы персонажей — настоящие.
/// </summary>
internal sealed class AiTestContext : IAsyncDisposable
{
    private readonly CharacterTestContext _characters;

    private AiTestContext(
        CharacterTestContext characters,
        ContentService content,
        ScriptedAiClient client,
        AiAssistant assistant)
    {
        _characters = characters;
        Content = content;
        Client = client;
        Assistant = assistant;
    }

    /// <summary>Служба контента, разделяющая базу данных с помощником.</summary>
    public ContentService Content { get; }

    /// <summary>Клиент службы языковой модели.</summary>
    public ScriptedAiClient Client { get; }

    /// <summary>Помощник.</summary>
    public AiAssistant Assistant { get; }

    /// <summary>Подсистема персонажей.</summary>
    public CharacterTestContext Characters => _characters;

    /// <summary>
    /// Создаёт подсистему помощника с заданными ответами модели.
    /// </summary>
    /// <param name="replies">Ответы, которые выдаст модель по очереди.</param>
    /// <returns>Готовое окружение теста.</returns>
    public static async Task<AiTestContext> CreateAsync(params AiReply[] replies)
    {
        var characters = await CharacterTestContext.CreateAsync();

        var content = new ContentService(
            StandardContentTypes.Create(),
            characters.ContextFactory,
            NullLogger<ContentService>.Instance);

        var client = new ScriptedAiClient(replies);
        var settings = new FixedSettingsService(new AppSettings());

        var assistant = new AiAssistant(
            client,
            content,
            settings,
            CreateTools(content, characters),
            NullLogger<AiAssistant>.Instance);

        return new AiTestContext(characters, content, client, assistant);
    }

    /// <summary>
    /// Возвращает инструмент помощника по имени.
    /// </summary>
    /// <param name="name">Имя инструмента.</param>
    /// <returns>Инструмент.</returns>
    public IAiTool Tool(string name) =>
        Assistant.Tools.Single(tool => tool.Name == name);

    /// <summary>
    /// Вызывает инструмент с аргументами, записанными так же, как их присылает модель.
    /// </summary>
    /// <param name="name">Имя инструмента.</param>
    /// <param name="arguments">Аргументы в виде записи объекта.</param>
    /// <returns>Результат работы инструмента.</returns>
    public Task<AiToolResult> InvokeAsync(string name, string arguments) =>
        Tool(name).InvokeAsync(ChatProtocol.ParseArguments(arguments));

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _characters.DisposeAsync();

    private static IReadOnlyList<IAiTool> CreateTools(
        ContentService content,
        CharacterTestContext characters) =>
    [
        new ListTypesTool(content),
        new DescribeTypeTool(content),
        new FindObjectsTool(content),
        new ReadObjectTool(content),
        new CreateObjectTool(content),
        new CopyObjectTool(content),
        new UpdateObjectTool(content),
        new AddListItemTool(content),
        new ListCharactersTool(characters.Characters),
        new ReadCharacterTool(characters.Characters, characters.Sheets),
        new CheckFormulaTool(characters.Formulas),
        new CheckDatabaseTool(content, characters.Formulas),
    ];
}

/// <summary>
/// Создание ответов модели для тестов.
/// </summary>
internal static class AiReplies
{
    /// <summary>
    /// Создаёт ответ с текстом и без вызовов инструментов.
    /// </summary>
    /// <param name="text">Текст ответа.</param>
    /// <returns>Ответ модели.</returns>
    public static AiReply Text(string text) => new(text, [], default);

    /// <summary>
    /// Создаёт ответ с вызовом инструмента.
    /// </summary>
    /// <param name="name">Имя инструмента.</param>
    /// <param name="arguments">Аргументы в виде записи объекта.</param>
    /// <param name="text">Текст, сопровождающий вызов.</param>
    /// <returns>Ответ модели.</returns>
    public static AiReply Call(string name, string arguments, string text = "") =>
        new(text, [new AiToolCall("вызов-1", name, ChatProtocol.ParseArguments(arguments))], default);
}
