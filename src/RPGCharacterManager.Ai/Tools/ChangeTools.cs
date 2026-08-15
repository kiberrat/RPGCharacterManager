using System.Text;
using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Ai.Tools;

/// <summary>
/// Общее поведение инструментов, изменяющих игровые данные.
///
/// Ни один из них ничего не записывает: инструмент готовит предложение, показывает
/// в нём будущие значения полей и возвращает его пользователю на подтверждение.
/// Это прямое требование документа 024_AI_Помощник.md.
/// </summary>
internal abstract class ChangeToolBase : IAiTool
{
    /// <summary>Пояснение к параметру со значениями полей.</summary>
    protected const string ValuesHint =
        "Значения полей: имя поля и его значение текстом. " +
        "Имена полей берите из describe_type, придумывать их нельзя.";

    /// <summary>
    /// Создаёт основу инструмента изменения данных.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    protected ChangeToolBase(IContentService content)
    {
        Content = Guard.NotNull(content);
        Writer = new AiContentWriter(content);
    }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Title { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<AiToolParameter> Parameters { get; }

    /// <inheritdoc />
    public bool ChangesData => true;

    /// <summary>Служба контента.</summary>
    protected IContentService Content { get; }

    /// <summary>Запись значений полей.</summary>
    protected AiContentWriter Writer { get; }

    /// <inheritdoc />
    public abstract Task<AiToolResult> InvokeAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Составляет ответ модели о подготовленном предложении.
    /// </summary>
    /// <param name="proposal">Предложение.</param>
    /// <param name="outcome">Итог заполнения полей.</param>
    /// <returns>Текст для модели.</returns>
    protected static string Report(AiProposal proposal, AiWriteOutcome outcome)
    {
        var builder = new StringBuilder();

        builder.Append("Предложение подготовлено и ждёт подтверждения пользователя: ")
            .Append(proposal.Summary).AppendLine(".");

        foreach (var change in proposal.Changes)
        {
            builder.Append("- ").Append(change.Field).Append(": ")
                .AppendLine(change.OldValue is null
                    ? change.NewValue
                    : $"{change.OldValue} → {change.NewValue}");
        }

        if (outcome.Problems.Count > 0)
        {
            builder.Append("Не записано: ")
                .Append(string.Join(AiContentText.Separator, outcome.Problems))
                .AppendLine(".");
        }

        builder.AppendLine(
            "Данные ещё не изменены. Расскажи пользователю, что предложено, и не повторяй вызов.");

        return builder.ToString();
    }
}

/// <summary>
/// Создание нового игрового объекта.
/// </summary>
internal sealed class CreateObjectTool : ChangeToolBase
{
    /// <summary>Имя инструмента.</summary>
    public const string ToolName = "create_object";

    /// <summary>
    /// Создаёт инструмент создания объекта.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    public CreateObjectTool(IContentService content)
        : base(content)
    {
    }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Title => "Создание объекта";

    /// <inheritdoc />
    public override string Description =>
        "Готовит предложение создать новый игровой объект. Обязательно заполни поле name. " +
        "Для каждого объекта вызывай инструмент отдельно.";

    /// <inheritdoc />
    public override IReadOnlyList<AiToolParameter> Parameters =>
    [
        new(AiToolParameters.Type, AiToolParameters.TypeHint, AiParameterKind.Text, IsRequired: true),
        new(AiToolParameters.Values, ValuesHint, AiParameterKind.Map, IsRequired: true),
    ];

    /// <inheritdoc />
    public override async Task<AiToolResult> InvokeAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(arguments);

        var descriptor = AiContentText.Resolve(Content, arguments.Text(AiToolParameters.Type));

        if (descriptor is null)
        {
            return AiToolResult.Answer(AiToolFailures.UnknownType(Content));
        }

        var values = arguments.Map(AiToolParameters.Values);

        if (values.Count == 0)
        {
            return AiToolResult.Answer(
                "Значения полей не переданы. Заполните параметр values хотя бы полем name.");
        }

        var entity = descriptor.CreateInstance();

        var outcome = await Writer
            .FillAsync(descriptor.Fields, entity, values, trackOldValues: false, cancellationToken)
            .ConfigureAwait(false);

        var name = descriptor.GetName(entity);

        if (string.IsNullOrWhiteSpace(name))
        {
            return AiToolResult.Answer(
                $"Не задано название объекта. Повторите вызов, заполнив поле name. " +
                $"Поля вида «{descriptor.Id}» возвращает {DescribeTypeTool.ToolName}.");
        }

        var proposal = new AiProposal(
            AiProposalKind.Create,
            descriptor.Id,
            descriptor.SingularName,
            name,
            outcome.Changes,
            values);

        return AiToolResult.Propose(Report(proposal, outcome), proposal);
    }
}

/// <summary>
/// Создание изменённой копии существующего объекта.
///
/// Единственный способ изменить системный объект: сам он остаётся неизменным,
/// а пользователь получает собственную копию — так требует документ 002_Архитектура.md.
/// </summary>
internal sealed class CopyObjectTool : ChangeToolBase
{
    /// <summary>Имя инструмента.</summary>
    public const string ToolName = "copy_object";

    /// <summary>
    /// Создаёт инструмент копирования объекта.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    public CopyObjectTool(IContentService content)
        : base(content)
    {
    }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Title => "Копия объекта";

    /// <inheritdoc />
    public override string Description =>
        "Готовит предложение создать копию объекта с изменёнными полями. " +
        "Так изменяют системные объекты: сам объект остаётся нетронутым.";

    /// <inheritdoc />
    public override IReadOnlyList<AiToolParameter> Parameters =>
    [
        new(AiToolParameters.Type, AiToolParameters.TypeHint, AiParameterKind.Text, IsRequired: true),
        new(
            AiToolParameters.Id,
            "Идентификатор копируемого объекта либо его точное название.",
            AiParameterKind.Text,
            IsRequired: true),
        new(AiToolParameters.Values, "Поля, которые нужно изменить в копии. " + ValuesHint, AiParameterKind.Map),
    ];

    /// <inheritdoc />
    public override async Task<AiToolResult> InvokeAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(arguments);

        var descriptor = AiContentText.Resolve(Content, arguments.Text(AiToolParameters.Type));

        if (descriptor is null)
        {
            return AiToolResult.Answer(AiToolFailures.UnknownType(Content));
        }

        var source = await AiObjectLookup
            .FindAsync(Content, descriptor, arguments.Text(AiToolParameters.Id), cancellationToken)
            .ConfigureAwait(false);

        if (source is null)
        {
            return AiToolResult.Answer(AiToolFailures.ObjectNotFound(descriptor));
        }

        var copy = await Content.DuplicateAsync(descriptor.Id, source.Id, cancellationToken)
            .ConfigureAwait(false);

        if (copy.IsFailure)
        {
            return AiToolResult.Answer($"Скопировать объект не удалось: {copy.Error}");
        }

        var values = arguments.Map(AiToolParameters.Values);

        var outcome = await Writer
            .FillAsync(descriptor.Fields, copy.Value, values, trackOldValues: true, cancellationToken)
            .ConfigureAwait(false);

        var proposal = new AiProposal(
            AiProposalKind.Create,
            descriptor.Id,
            descriptor.SingularName,
            descriptor.GetName(copy.Value),
            outcome.Changes,
            values,
            source.Id);

        return AiToolResult.Propose(Report(proposal, outcome), proposal);
    }
}

/// <summary>
/// Изменение существующего игрового объекта.
/// </summary>
internal sealed class UpdateObjectTool : ChangeToolBase
{
    /// <summary>Имя инструмента.</summary>
    public const string ToolName = "update_object";

    /// <summary>
    /// Создаёт инструмент изменения объекта.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    public UpdateObjectTool(IContentService content)
        : base(content)
    {
    }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Title => "Изменение объекта";

    /// <inheritdoc />
    public override string Description =>
        "Готовит предложение изменить поля существующего объекта. " +
        "Передавай только те поля, которые действительно меняются.";

    /// <inheritdoc />
    public override IReadOnlyList<AiToolParameter> Parameters =>
    [
        new(AiToolParameters.Type, AiToolParameters.TypeHint, AiParameterKind.Text, IsRequired: true),
        new(
            AiToolParameters.Id,
            "Идентификатор изменяемого объекта либо его точное название.",
            AiParameterKind.Text,
            IsRequired: true),
        new(AiToolParameters.Values, ValuesHint, AiParameterKind.Map, IsRequired: true),
    ];

    /// <inheritdoc />
    public override async Task<AiToolResult> InvokeAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(arguments);

        var descriptor = AiContentText.Resolve(Content, arguments.Text(AiToolParameters.Type));

        if (descriptor is null)
        {
            return AiToolResult.Answer(AiToolFailures.UnknownType(Content));
        }

        var entity = await AiObjectLookup
            .FindAsync(Content, descriptor, arguments.Text(AiToolParameters.Id), cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return AiToolResult.Answer(AiToolFailures.ObjectNotFound(descriptor));
        }

        if (entity is ContentEntity { IsSystem: true })
        {
            return AiToolResult.Answer(
                $"Объект «{descriptor.GetName(entity)}» системный, изменить его нельзя. " +
                $"Вызовите {CopyObjectTool.ToolName} — он подготовит изменённую копию.");
        }

        var values = arguments.Map(AiToolParameters.Values);

        if (values.Count == 0)
        {
            return AiToolResult.Answer("Значения полей не переданы: изменять нечего.");
        }

        var outcome = await Writer
            .FillAsync(descriptor.Fields, entity, values, trackOldValues: true, cancellationToken)
            .ConfigureAwait(false);

        if (outcome.Changes.Count == 0)
        {
            return AiToolResult.Answer(outcome.Problems.Count > 0
                ? $"Ничего не изменилось. Причины: {string.Join(AiContentText.Separator, outcome.Problems)}."
                : "Указанные значения уже записаны в объекте: изменять нечего.");
        }

        var proposal = new AiProposal(
            AiProposalKind.Update,
            descriptor.Id,
            descriptor.SingularName,
            descriptor.GetName(entity),
            outcome.Changes,
            values,
            entity.Id);

        return AiToolResult.Propose(Report(proposal, outcome), proposal);
    }
}

/// <summary>
/// Добавление записи во вложенный список объекта: бонуса эффекта, уровня усиления,
/// восстановления при отдыхе.
/// </summary>
internal sealed class AddListItemTool : ChangeToolBase
{
    /// <summary>Имя инструмента.</summary>
    public const string ToolName = "add_list_item";

    /// <summary>
    /// Создаёт инструмент добавления записи списка.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    public AddListItemTool(IContentService content)
        : base(content)
    {
    }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Title => "Запись списка";

    /// <inheritdoc />
    public override string Description =>
        "Готовит предложение добавить запись во вложенный список объекта: " +
        "бонус эффекта, уровень усиления заклинания, восстановление при отдыхе. " +
        "Перечень списков и их полей возвращает describe_type.";

    /// <inheritdoc />
    public override IReadOnlyList<AiToolParameter> Parameters =>
    [
        new(AiToolParameters.Type, AiToolParameters.TypeHint, AiParameterKind.Text, IsRequired: true),
        new(
            AiToolParameters.Id,
            "Идентификатор объекта либо его точное название.",
            AiParameterKind.Text,
            IsRequired: true),
        new(
            AiToolParameters.List,
            "Внутреннее имя вложенного списка из describe_type.",
            AiParameterKind.Text,
            IsRequired: true),
        new(AiToolParameters.Values, "Значения полей записи. " + ValuesHint, AiParameterKind.Map, IsRequired: true),
    ];

    /// <inheritdoc />
    public override async Task<AiToolResult> InvokeAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(arguments);

        var descriptor = AiContentText.Resolve(Content, arguments.Text(AiToolParameters.Type));

        if (descriptor is null)
        {
            return AiToolResult.Answer(AiToolFailures.UnknownType(Content));
        }

        var listName = arguments.Text(AiToolParameters.List);
        var list = descriptor.Collections.FirstOrDefault(item =>
            item.Name.Equals(listName, StringComparison.OrdinalIgnoreCase)
            || item.DisplayName.Equals(listName, StringComparison.OrdinalIgnoreCase));

        if (list is null)
        {
            return AiToolResult.Answer(descriptor.Collections.Count == 0
                ? $"У вида «{descriptor.Id}» вложенных списков нет."
                : "Такого списка нет. Доступные списки: " +
                  string.Join(AiContentText.Separator, descriptor.Collections.Select(item => item.Name)) + ".");
        }

        var entity = await AiObjectLookup
            .FindAsync(Content, descriptor, arguments.Text(AiToolParameters.Id), cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return AiToolResult.Answer(AiToolFailures.ObjectNotFound(descriptor));
        }

        if (entity is ContentEntity { IsSystem: true })
        {
            return AiToolResult.Answer(
                $"Объект «{descriptor.GetName(entity)}» системный, дополнять его нельзя. " +
                $"Вызовите {CopyObjectTool.ToolName} и дополняйте копию.");
        }

        var values = arguments.Map(AiToolParameters.Values);
        var item = list.AddItem(entity);

        var outcome = await Writer
            .FillAsync(list.Fields, item, values, trackOldValues: false, cancellationToken)
            .ConfigureAwait(false);

        // Запись добавлялась только ради предпросмотра: объект не сохраняется,
        // но и оставлять его изменённым в памяти незачем.
        list.RemoveItem(entity, item);

        var proposal = new AiProposal(
            AiProposalKind.Update,
            descriptor.Id,
            $"{descriptor.SingularName} · {list.SingularName}",
            descriptor.GetName(entity),
            outcome.Changes,
            values,
            entity.Id,
            list.Name);

        return AiToolResult.Propose(Report(proposal, outcome), proposal);
    }
}
