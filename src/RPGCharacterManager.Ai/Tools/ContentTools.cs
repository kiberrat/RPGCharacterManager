using System.Text;
using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Ai.Tools;

/// <summary>
/// Имена параметров, общие для инструментов помощника.
/// </summary>
internal static class AiToolParameters
{
    /// <summary>Вид игрового контента.</summary>
    public const string Type = "type";

    /// <summary>Идентификатор объекта.</summary>
    public const string Id = "id";

    /// <summary>Строка поиска.</summary>
    public const string Query = "query";

    /// <summary>Наибольшее количество записей в ответе.</summary>
    public const string Limit = "limit";

    /// <summary>Значения полей объекта.</summary>
    public const string Values = "values";

    /// <summary>Внутреннее имя вложенного списка.</summary>
    public const string List = "list";

    /// <summary>Выражение движка вычислений.</summary>
    public const string Formula = "formula";

    /// <summary>Пояснение к параметру вида контента.</summary>
    public const string TypeHint =
        "Внутреннее имя вида игровых объектов, например spells или weapons. " +
        "Полный перечень возвращает list_types.";
}

/// <summary>
/// Перечень видов игровых объектов, доступных в приложении.
///
/// Это первое, что помощник узнаёт о базе: приложение не знает заранее ни одной
/// игровой системы, поэтому состав видов объектов определяется только тем, что
/// зарегистрировано в приложении.
/// </summary>
internal sealed class ListTypesTool : IAiTool
{
    /// <summary>Имя инструмента.</summary>
    public const string ToolName = "list_types";

    private readonly IContentService _content;

    /// <summary>
    /// Создаёт инструмент перечня видов контента.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    public ListTypesTool(IContentService content) => _content = Guard.NotNull(content);

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Title => "Виды объектов";

    /// <inheritdoc />
    public string Description =>
        "Возвращает все виды игровых объектов этого приложения и количество объектов каждого вида. " +
        "Вызывай первым, когда не знаешь, к какому виду отнести объект.";

    /// <inheritdoc />
    public IReadOnlyList<AiToolParameter> Parameters => [];

    /// <inheritdoc />
    public async Task<AiToolResult> InvokeAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder("Виды игровых объектов:").AppendLine();

        foreach (var type in _content.Types)
        {
            var page = await _content.SearchAsync(type.Id, null, 0, 1, cancellationToken)
                .ConfigureAwait(false);

            builder.Append("- ").Append(type.Id).Append(" — ").Append(type.DisplayName)
                .Append(" (объектов: ").Append(page.TotalCount).Append("). ")
                .AppendLine(type.Description);
        }

        return AiToolResult.Answer(builder.ToString());
    }
}

/// <summary>
/// Описание полей одного вида игровых объектов.
/// </summary>
internal sealed class DescribeTypeTool : IAiTool
{
    /// <summary>Имя инструмента.</summary>
    public const string ToolName = "describe_type";

    private readonly IContentService _content;

    /// <summary>
    /// Создаёт инструмент описания вида контента.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    public DescribeTypeTool(IContentService content) => _content = Guard.NotNull(content);

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Title => "Поля вида";

    /// <inheritdoc />
    public string Description =>
        "Возвращает поля вида объектов: внутреннее имя, название, вид значения и обязательность. " +
        "Вызывай перед созданием и изменением объекта: имена полей нельзя придумывать.";

    /// <inheritdoc />
    public IReadOnlyList<AiToolParameter> Parameters =>
    [
        new(AiToolParameters.Type, AiToolParameters.TypeHint, AiParameterKind.Text, IsRequired: true),
    ];

    /// <inheritdoc />
    public Task<AiToolResult> InvokeAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(arguments);

        var descriptor = AiContentText.Resolve(_content, arguments.Text(AiToolParameters.Type));

        return Task.FromResult(descriptor is null
            ? AiToolResult.Answer(AiToolFailures.UnknownType(_content))
            : AiToolResult.Answer(AiContentText.DescribeType(descriptor)));
    }
}

/// <summary>
/// Поиск игровых объектов по названию.
/// </summary>
internal sealed class FindObjectsTool : IAiTool
{
    /// <summary>Имя инструмента.</summary>
    public const string ToolName = "find_objects";

    /// <summary>Сколько объектов возвращается, если модель не указала иного.</summary>
    public const int DefaultLimit = 20;

    /// <summary>Наибольшее допустимое количество объектов в ответе.</summary>
    public const int MaximumLimit = 50;

    private readonly IContentService _content;

    /// <summary>
    /// Создаёт инструмент поиска объектов.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    public FindObjectsTool(IContentService content) => _content = Guard.NotNull(content);

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Title => "Поиск объектов";

    /// <inheritdoc />
    public string Description =>
        "Ищет игровые объекты заданного вида по названию и возвращает их идентификаторы. " +
        "Без строки поиска возвращает начало списка.";

    /// <inheritdoc />
    public IReadOnlyList<AiToolParameter> Parameters =>
    [
        new(AiToolParameters.Type, AiToolParameters.TypeHint, AiParameterKind.Text, IsRequired: true),
        new(AiToolParameters.Query, "Часть названия объекта.", AiParameterKind.Text),
        new(
            AiToolParameters.Limit,
            $"Сколько объектов вернуть, не больше {MaximumLimit}.",
            AiParameterKind.Number),
    ];

    /// <inheritdoc />
    public async Task<AiToolResult> InvokeAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(arguments);

        var descriptor = AiContentText.Resolve(_content, arguments.Text(AiToolParameters.Type));

        if (descriptor is null)
        {
            return AiToolResult.Answer(AiToolFailures.UnknownType(_content));
        }

        var limit = Math.Clamp(arguments.Number(AiToolParameters.Limit, DefaultLimit), 1, MaximumLimit);
        var search = arguments.Text(AiToolParameters.Query);

        var page = await _content
            .SearchAsync(descriptor.Id, search, 0, limit, cancellationToken)
            .ConfigureAwait(false);

        if (page.Items.Count == 0)
        {
            return AiToolResult.Answer(
                $"Объектов вида «{descriptor.DisplayName}» по запросу «{search}» не найдено.");
        }

        var builder = new StringBuilder();

        builder.Append("Найдено объектов: ").Append(page.Items.Count)
            .Append(" из ").Append(page.TotalCount).AppendLine(".");

        foreach (var item in page.Items)
        {
            builder.Append("- ").Append(item.Id).Append(" — ").Append(item.Name);

            if (item.IsSystem)
            {
                builder.Append(" (системный, изменять нельзя)");
            }

            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                builder.Append(". ").Append(Shorten(item.Description));
            }

            builder.AppendLine();
        }

        return AiToolResult.Answer(builder.ToString());
    }

    /// <summary>Сколько знаков описания попадает в строку списка.</summary>
    private const int DescriptionLimit = 160;

    private static string Shorten(string text) => text.Length <= DescriptionLimit
        ? text
        : string.Concat(text.AsSpan(0, DescriptionLimit), "…");
}

/// <summary>
/// Чтение игрового объекта целиком.
/// </summary>
internal sealed class ReadObjectTool : IAiTool
{
    /// <summary>Имя инструмента.</summary>
    public const string ToolName = "read_object";

    private readonly IContentService _content;

    /// <summary>
    /// Создаёт инструмент чтения объекта.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    public ReadObjectTool(IContentService content) => _content = Guard.NotNull(content);

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Title => "Чтение объекта";

    /// <inheritdoc />
    public string Description =>
        "Возвращает все поля одного игрового объекта. " +
        "Вызывай перед тем, как объяснять объект пользователю или предлагать его изменение.";

    /// <inheritdoc />
    public IReadOnlyList<AiToolParameter> Parameters =>
    [
        new(AiToolParameters.Type, AiToolParameters.TypeHint, AiParameterKind.Text, IsRequired: true),
        new(
            AiToolParameters.Id,
            "Идентификатор объекта либо его точное название.",
            AiParameterKind.Text,
            IsRequired: true),
    ];

    /// <inheritdoc />
    public async Task<AiToolResult> InvokeAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(arguments);

        var descriptor = AiContentText.Resolve(_content, arguments.Text(AiToolParameters.Type));

        if (descriptor is null)
        {
            return AiToolResult.Answer(AiToolFailures.UnknownType(_content));
        }

        var entity = await AiObjectLookup
            .FindAsync(_content, descriptor, arguments.Text(AiToolParameters.Id), cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? AiToolResult.Answer(AiToolFailures.ObjectNotFound(descriptor))
            : AiToolResult.Answer(AiContentText.DescribeEntity(descriptor, entity));
    }
}

/// <summary>
/// Поиск объекта по идентификатору либо по названию.
/// Модель одинаково охотно называет и то и другое, поэтому оба вида принимаются.
/// </summary>
internal static class AiObjectLookup
{
    /// <summary>
    /// Находит объект по идентификатору либо по точному названию.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    /// <param name="descriptor">Описание вида контента.</param>
    /// <param name="text">Идентификатор или название.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Объект либо <see langword="null"/>.</returns>
    public static async Task<EntityBase?> FindAsync(
        IContentService content,
        IContentTypeDescriptor descriptor,
        string? text,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (Guid.TryParse(text, out var identifier))
        {
            return await content.GetAsync(descriptor.Id, identifier, cancellationToken)
                .ConfigureAwait(false);
        }

        var page = await content
            .SearchAsync(descriptor.Id, text, 0, FindObjectsTool.MaximumLimit, cancellationToken)
            .ConfigureAwait(false);

        var found = page.Items.FirstOrDefault(item =>
            item.Name.Equals(text, StringComparison.OrdinalIgnoreCase));

        return found is null
            ? null
            : await content.GetAsync(descriptor.Id, found.Id, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Сообщения инструментов о невыполнимых запросах.
/// Текст возвращается модели, поэтому он объясняет, что сделать вместо этого.
/// </summary>
internal static class AiToolFailures
{
    /// <summary>
    /// Сообщает, что вид контента не найден, и перечисляет доступные.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    /// <returns>Текст сообщения.</returns>
    public static string UnknownType(IContentService content) =>
        "Такого вида объектов нет. Доступные виды: " +
        string.Join(AiContentText.Separator, content.Types.Select(type => type.Id)) + ".";

    /// <summary>
    /// Сообщает, что объект не найден.
    /// </summary>
    /// <param name="descriptor">Описание вида контента.</param>
    /// <returns>Текст сообщения.</returns>
    public static string ObjectNotFound(IContentTypeDescriptor descriptor) =>
        $"Объект вида «{descriptor.DisplayName}» не найден. " +
        $"Найдите его через {FindObjectsTool.ToolName} и используйте полученный идентификатор.";
}
