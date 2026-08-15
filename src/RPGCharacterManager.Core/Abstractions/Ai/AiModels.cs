namespace RPGCharacterManager.Core.Abstractions.Ai;

/// <summary>
/// Роль участника переписки с языковой моделью.
/// </summary>
public enum AiRole
{
    /// <summary>Указания модели: что она собой представляет и что ей доступно.</summary>
    System = 0,

    /// <summary>Сообщение пользователя.</summary>
    User = 1,

    /// <summary>Ответ модели.</summary>
    Assistant = 2,

    /// <summary>Результат работы инструмента, возвращённый модели.</summary>
    Tool = 3,
}

/// <summary>
/// Способ передачи значения в параметре инструмента.
///
/// Набор намеренно мал: приложение описывает объекты полями, значения которых
/// вводятся текстом, поэтому сложные структуры инструментам не требуются.
/// </summary>
public enum AiParameterKind
{
    /// <summary>Строка.</summary>
    Text = 0,

    /// <summary>Целое число.</summary>
    Number = 1,

    /// <summary>Логическое значение.</summary>
    Flag = 2,

    /// <summary>Набор пар «имя поля — значение».</summary>
    Map = 3,
}

/// <summary>
/// Разобранные аргументы вызова инструмента.
///
/// Разбор выполняет клиент модели, поэтому сами инструменты не зависят от формата
/// передачи данных и остаются пригодными для проверки тестами.
/// </summary>
public sealed class AiToolArguments
{
    private static readonly Dictionary<string, string?> NoValues = [];

    private readonly IReadOnlyDictionary<string, string?> _values;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> _maps;

    /// <summary>
    /// Создаёт аргументы вызова инструмента.
    /// </summary>
    /// <param name="raw">Исходный текст аргументов, полученный от модели.</param>
    /// <param name="values">Простые значения аргументов.</param>
    /// <param name="maps">Аргументы-наборы пар «имя — значение».</param>
    public AiToolArguments(
        string raw,
        IReadOnlyDictionary<string, string?> values,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> maps)
    {
        Raw = raw;
        _values = values;
        _maps = maps;
    }

    /// <summary>Пустые аргументы.</summary>
    public static AiToolArguments Empty { get; } = new(
        "{}",
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, IReadOnlyDictionary<string, string?>>(StringComparer.OrdinalIgnoreCase));

    /// <summary>Исходный текст аргументов: показывается пользователю в списке действий.</summary>
    public string Raw { get; }

    /// <summary>
    /// Возвращает строковое значение аргумента.
    /// </summary>
    /// <param name="name">Имя аргумента.</param>
    /// <returns>Значение либо <see langword="null"/>, если аргумент не задан.</returns>
    public string? Text(string name)
    {
        var value = _values.TryGetValue(name, out var found) ? found : null;

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Возвращает значение аргумента как идентификатор объекта.
    /// </summary>
    /// <param name="name">Имя аргумента.</param>
    /// <returns>Идентификатор либо <see langword="null"/>, если значение не является идентификатором.</returns>
    public Guid? Identifier(string name) =>
        Guid.TryParse(Text(name), out var value) ? value : null;

    /// <summary>
    /// Возвращает числовое значение аргумента.
    /// </summary>
    /// <param name="name">Имя аргумента.</param>
    /// <param name="fallback">Значение, применяемое при отсутствии аргумента.</param>
    /// <returns>Число.</returns>
    public int Number(string name, int fallback) =>
        int.TryParse(Text(name), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    /// <summary>
    /// Возвращает набор пар «имя поля — значение».
    /// </summary>
    /// <param name="name">Имя аргумента.</param>
    /// <returns>Набор значений; пустой, если аргумент не задан.</returns>
    public IReadOnlyDictionary<string, string?> Map(string name) =>
        _maps.TryGetValue(name, out var found) ? found : NoValues;
}

/// <summary>
/// Вызов инструмента, запрошенный моделью.
/// </summary>
/// <param name="Id">Идентификатор вызова: с ним модель сопоставит результат.</param>
/// <param name="Name">Имя инструмента.</param>
/// <param name="Arguments">Разобранные аргументы.</param>
public sealed record AiToolCall(string Id, string Name, AiToolArguments Arguments);

/// <summary>
/// Сообщение переписки с моделью.
/// </summary>
public sealed record AiMessage
{
    /// <summary>Роль автора сообщения.</summary>
    public required AiRole Role { get; init; }

    /// <summary>Текст сообщения.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Вызовы инструментов, запрошенные моделью.</summary>
    public IReadOnlyList<AiToolCall> Calls { get; init; } = [];

    /// <summary>Идентификатор вызова, на который отвечает сообщение роли <see cref="AiRole.Tool"/>.</summary>
    public string? CallId { get; init; }

    /// <summary>Имя инструмента, результат которого содержит сообщение.</summary>
    public string? CallName { get; init; }

    /// <summary>
    /// Создаёт указания модели.
    /// </summary>
    /// <param name="text">Текст указаний.</param>
    /// <returns>Сообщение.</returns>
    public static AiMessage System(string text) => new() { Role = AiRole.System, Text = text };

    /// <summary>
    /// Создаёт сообщение пользователя.
    /// </summary>
    /// <param name="text">Текст сообщения.</param>
    /// <returns>Сообщение.</returns>
    public static AiMessage User(string text) => new() { Role = AiRole.User, Text = text };

    /// <summary>
    /// Создаёт ответ модели.
    /// </summary>
    /// <param name="text">Текст ответа.</param>
    /// <param name="calls">Запрошенные вызовы инструментов.</param>
    /// <returns>Сообщение.</returns>
    public static AiMessage Assistant(string text, IReadOnlyList<AiToolCall>? calls = null) => new()
    {
        Role = AiRole.Assistant,
        Text = text,
        Calls = calls ?? [],
    };

    /// <summary>
    /// Создаёт результат работы инструмента.
    /// </summary>
    /// <param name="callId">Идентификатор вызова.</param>
    /// <param name="name">Имя инструмента.</param>
    /// <param name="text">Текст результата.</param>
    /// <returns>Сообщение.</returns>
    public static AiMessage Tool(string callId, string name, string text) => new()
    {
        Role = AiRole.Tool,
        Text = text,
        CallId = callId,
        CallName = name,
    };
}

/// <summary>
/// Описание параметра инструмента.
/// </summary>
/// <param name="Name">Имя параметра.</param>
/// <param name="Description">Пояснение для модели.</param>
/// <param name="Kind">Способ передачи значения.</param>
/// <param name="IsRequired">Параметр обязателен.</param>
public sealed record AiToolParameter(
    string Name,
    string Description,
    AiParameterKind Kind,
    bool IsRequired = false);

/// <summary>
/// Описание инструмента, передаваемое модели.
/// </summary>
/// <param name="Name">Имя инструмента.</param>
/// <param name="Description">Пояснение: что инструмент делает и когда его вызывать.</param>
/// <param name="Parameters">Параметры инструмента.</param>
public sealed record AiToolSpec(
    string Name,
    string Description,
    IReadOnlyList<AiToolParameter> Parameters);

/// <summary>
/// Израсходованные единицы обработки текста.
/// </summary>
/// <param name="PromptTokens">Единицы запроса.</param>
/// <param name="CompletionTokens">Единицы ответа.</param>
public readonly record struct AiUsage(int PromptTokens, int CompletionTokens)
{
    /// <summary>Общее количество израсходованных единиц.</summary>
    public int Total => PromptTokens + CompletionTokens;

    /// <summary>
    /// Складывает расходы двух обращений к модели.
    /// </summary>
    /// <param name="other">Второй расход.</param>
    /// <returns>Суммарный расход.</returns>
    public AiUsage Add(AiUsage other) =>
        new(PromptTokens + other.PromptTokens, CompletionTokens + other.CompletionTokens);
}

/// <summary>
/// Запрос к языковой модели.
/// </summary>
/// <param name="Messages">Переписка целиком, включая указания модели.</param>
public sealed record AiRequest(IReadOnlyList<AiMessage> Messages)
{
    /// <summary>Температура по умолчанию: ответы должны быть предсказуемыми, а не разнообразными.</summary>
    public const double DefaultTemperature = 0.2;

    /// <summary>Доступные модели инструменты.</summary>
    public IReadOnlyList<AiToolSpec> Tools { get; init; } = [];

    /// <summary>Разброс ответов: чем меньше, тем строже модель следует указаниям.</summary>
    public double Temperature { get; init; } = DefaultTemperature;
}

/// <summary>
/// Ответ языковой модели.
/// </summary>
/// <param name="Text">Текст ответа.</param>
/// <param name="Calls">Вызовы инструментов, запрошенные моделью.</param>
/// <param name="Usage">Израсходованные единицы обработки текста.</param>
public sealed record AiReply(string Text, IReadOnlyList<AiToolCall> Calls, AiUsage Usage);

/// <summary>
/// Сведения об установленной связи со службой языковой модели.
/// Показываются пользователю как подтверждение того, что ключ и модель работают.
/// </summary>
/// <param name="Model">Выбранная модель.</param>
/// <param name="AvailableModels">Количество моделей, доступных по ключу.</param>
/// <param name="Latency">Время ответа службы.</param>
/// <param name="Answer">Короткий ответ модели на проверочный вопрос.</param>
public sealed record AiConnection(string Model, int AvailableModels, TimeSpan Latency, string Answer);
