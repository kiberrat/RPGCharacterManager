using System.Globalization;
using System.Text;
using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Ai.Tools;

/// <summary>
/// Проверка выражения движком вычислений.
///
/// Формулы придумывает помощник, а проверяет их тот же движок, что и вычисляет:
/// иначе в базу попадали бы выражения, красивые с виду и несчитаемые на деле.
/// </summary>
internal sealed class CheckFormulaTool : IAiTool
{
    /// <summary>Имя инструмента.</summary>
    public const string ToolName = "check_formula";

    private readonly IFormulaEngine _engine;

    /// <summary>
    /// Создаёт инструмент проверки формулы.
    /// </summary>
    /// <param name="engine">Движок вычислений.</param>
    public CheckFormulaTool(IFormulaEngine engine) => _engine = Guard.NotNull(engine);

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Title => "Проверка формулы";

    /// <inheritdoc />
    public string Description =>
        "Проверяет выражение движком вычислений приложения и возвращает используемые им переменные. " +
        "Вызывай перед тем, как записать формулу в поле объекта.";

    /// <inheritdoc />
    public IReadOnlyList<AiToolParameter> Parameters =>
    [
        new(
            AiToolParameters.Formula,
            "Выражение, например: 8к6 или 2к8 + Сила * 2.",
            AiParameterKind.Text,
            IsRequired: true),
    ];

    /// <inheritdoc />
    public Task<AiToolResult> InvokeAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(arguments);

        var expression = arguments.Text(AiToolParameters.Formula);

        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(AiToolResult.Answer("Выражение не передано."));
        }

        var validation = _engine.Validate(expression);

        if (validation.IsFailure)
        {
            return Task.FromResult(AiToolResult.Answer(
                $"Выражение «{expression}» неверно: {validation.Error}. " +
                $"Доступные функции: {string.Join(AiContentText.Separator, _engine.Functions.Select(item => item.Name))}."));
        }

        var builder = new StringBuilder($"Выражение «{expression}» верно.");
        var variables = _engine.GetReferencedVariables(expression);

        if (variables.IsSuccess && variables.Value.Count > 0)
        {
            builder.Append(" Использует переменные: ")
                .Append(string.Join(AiContentText.Separator, variables.Value)).Append('.');
        }

        var range = _engine.EvaluateRange(expression);

        if (range.IsSuccess)
        {
            builder.Append(range.Value.IsExact
                ? $" Значение: {Number(range.Value.Minimum)}."
                : $" Диапазон значений: от {Number(range.Value.Minimum)} до {Number(range.Value.Maximum)}.");
        }

        return Task.FromResult(AiToolResult.Answer(builder.ToString()));
    }

    private static string Number(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);
}

/// <summary>
/// Проверка игровой базы: незаполненные обязательные поля, несчитаемые формулы,
/// ссылки на исчезнувшие объекты и одинаковые названия.
///
/// Проверка построена на описаниях видов контента, поэтому знает ровно то, что
/// знает редактор, и не содержит правил какой-либо конкретной игры.
/// </summary>
internal sealed class CheckDatabaseTool : IAiTool
{
    /// <summary>Имя инструмента.</summary>
    public const string ToolName = "check_database";

    /// <summary>Сколько объектов одного вида проверяется за один вызов.</summary>
    public const int MaximumPerType = 300;

    /// <summary>Сколько объектов проверяется за один вызов всего.</summary>
    public const int MaximumTotal = 1500;

    /// <summary>Сколько замечаний попадает в ответ.</summary>
    public const int MaximumProblems = 40;

    private const int PageSize = 100;

    private readonly IContentService _content;
    private readonly IFormulaEngine _engine;

    /// <summary>
    /// Создаёт инструмент проверки базы.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    /// <param name="engine">Движок вычислений.</param>
    public CheckDatabaseTool(IContentService content, IFormulaEngine engine)
    {
        _content = Guard.NotNull(content);
        _engine = Guard.NotNull(engine);
    }

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Title => "Проверка базы";

    /// <inheritdoc />
    public string Description =>
        "Проверяет игровые объекты: незаполненные обязательные поля, несчитаемые формулы, " +
        "ссылки на несуществующие объекты и повторяющиеся названия. " +
        "Без указания вида проверяет всю базу по частям.";

    /// <inheritdoc />
    public IReadOnlyList<AiToolParameter> Parameters =>
    [
        new(
            AiToolParameters.Type,
            "Вид объектов для проверки. Без него проверяются все виды. " + AiToolParameters.TypeHint,
            AiParameterKind.Text),
    ];

    /// <inheritdoc />
    public async Task<AiToolResult> InvokeAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(arguments);

        var requested = arguments.Text(AiToolParameters.Type);
        var descriptor = AiContentText.Resolve(_content, requested);

        if (requested is { Length: > 0 } && descriptor is null)
        {
            return AiToolResult.Answer(AiToolFailures.UnknownType(_content));
        }

        var types = descriptor is null ? _content.Types : [descriptor];
        var report = await InspectAsync(types, cancellationToken).ConfigureAwait(false);

        return AiToolResult.Answer(report);
    }

    /// <summary>
    /// Проверяет объекты указанных видов и составляет отчёт.
    /// </summary>
    /// <param name="types">Проверяемые виды контента.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Текст отчёта.</returns>
    private async Task<string> InspectAsync(
        IReadOnlyList<IContentTypeDescriptor> types,
        CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        var references = new Dictionary<string, HashSet<Guid>>(StringComparer.Ordinal);

        var inspected = 0;
        var total = 0;

        foreach (var type in types)
        {
            var names = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
            var checkedHere = 0;

            for (var page = 0; ; page++)
            {
                var items = await _content
                    .SearchAsync(type.Id, null, page, PageSize, cancellationToken)
                    .ConfigureAwait(false);

                if (page == 0)
                {
                    total += items.TotalCount;
                }

                if (items.Items.Count == 0)
                {
                    break;
                }

                foreach (var item in items.Items)
                {
                    if (checkedHere >= MaximumPerType || inspected >= MaximumTotal)
                    {
                        break;
                    }

                    checkedHere++;
                    inspected++;

                    if (names.TryGetValue(item.Name, out var existing))
                    {
                        problems.Add(
                            $"{type.Id}: название «{item.Name}» повторяется у объектов {existing} и {item.Id}");
                    }
                    else
                    {
                        names[item.Name] = item.Id.ToString();
                    }

                    var entity = await _content.GetAsync(type.Id, item.Id, cancellationToken)
                        .ConfigureAwait(false);

                    if (entity is null)
                    {
                        continue;
                    }

                    await InspectEntityAsync(type, entity, problems, references, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (checkedHere >= MaximumPerType
                    || inspected >= MaximumTotal
                    || items.Items.Count < PageSize)
                {
                    break;
                }
            }
        }

        return Describe(problems, inspected, total);
    }

    /// <summary>
    /// Проверяет поля одного объекта.
    /// </summary>
    /// <param name="type">Описание вида контента.</param>
    /// <param name="entity">Игровой объект.</param>
    /// <param name="problems">Накопленные замечания.</param>
    /// <param name="references">Известные идентификаторы объектов по видам.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после проверки.</returns>
    private async Task InspectEntityAsync(
        IContentTypeDescriptor type,
        EntityBase entity,
        List<string> problems,
        Dictionary<string, HashSet<Guid>> references,
        CancellationToken cancellationToken)
    {
        var name = type.GetName(entity);

        foreach (var field in type.Fields)
        {
            switch (field.Kind)
            {
                case ContentFieldKind.Formula:
                    var expression = field.GetText(entity);

                    if (!string.IsNullOrWhiteSpace(expression))
                    {
                        var validation = _engine.Validate(expression);

                        if (validation.IsFailure)
                        {
                            problems.Add(
                                $"{type.Id}: «{name}» ({entity.Id}), поле «{field.DisplayName}» — " +
                                $"выражение «{expression}» не считается: {validation.Error}");
                        }
                    }
                    else if (field.IsRequired)
                    {
                        problems.Add(
                            $"{type.Id}: «{name}» ({entity.Id}) — не заполнено обязательное поле «{field.DisplayName}»");
                    }

                    break;

                case ContentFieldKind.Reference:
                    if (field.GetReference(entity) is { } identifier)
                    {
                        var known = await GetKnownAsync(field.ReferenceTypeId, references, cancellationToken)
                            .ConfigureAwait(false);

                        // Пустой перечень — не повод пропустить проверку: если
                        // объектов этого вида не осталось вовсе, ссылка тем более
                        // потеряна. Пропускается лишь незарегистрированный вид.
                        if (known is not null && !known.Contains(identifier))
                        {
                            problems.Add(
                                $"{type.Id}: «{name}» ({entity.Id}), поле «{field.DisplayName}» — " +
                                $"ссылка на несуществующий объект {identifier}");
                        }
                    }

                    break;

                default:
                    if (field.IsRequired && string.IsNullOrWhiteSpace(field.GetText(entity)))
                    {
                        problems.Add(
                            $"{type.Id}: «{name}» ({entity.Id}) — не заполнено обязательное поле «{field.DisplayName}»");
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Возвращает идентификаторы объектов вида, на который ссылается поле.
    /// </summary>
    /// <param name="typeId">Идентификатор вида контента.</param>
    /// <param name="cache">Уже прочитанные перечни.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификаторы либо <see langword="null"/>, если вид не зарегистрирован.</returns>
    private async Task<HashSet<Guid>?> GetKnownAsync(
        string? typeId,
        Dictionary<string, HashSet<Guid>> cache,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(typeId) || _content.FindType(typeId) is null)
        {
            return null;
        }

        if (cache.TryGetValue(typeId, out var found))
        {
            return found;
        }

        var known = (await _content.GetReferencesAsync(typeId, cancellationToken).ConfigureAwait(false))
            .Select(item => item.Id)
            .ToHashSet();

        cache[typeId] = known;

        return known;
    }

    /// <summary>
    /// Составляет текст отчёта о проверке.
    /// </summary>
    /// <param name="problems">Найденные замечания.</param>
    /// <param name="inspected">Сколько объектов проверено.</param>
    /// <param name="total">Сколько объектов всего.</param>
    /// <returns>Текст отчёта.</returns>
    private static string Describe(IReadOnlyList<string> problems, int inspected, int total)
    {
        var builder = new StringBuilder();

        builder.Append("Проверено объектов: ").Append(inspected).Append(" из ").Append(total).AppendLine(".");

        if (problems.Count == 0)
        {
            builder.AppendLine("Замечаний не найдено.");

            return builder.ToString();
        }

        builder.Append("Найдено замечаний: ").Append(problems.Count).AppendLine(".");

        foreach (var problem in problems.Take(MaximumProblems))
        {
            builder.Append("- ").AppendLine(problem);
        }

        if (problems.Count > MaximumProblems)
        {
            builder.Append("Показаны первые ").Append(MaximumProblems)
                .AppendLine(" замечаний; остальные останутся до следующей проверки.");
        }

        builder.AppendLine(
            "Ничего не исправлено. Перечисли замечания пользователю и предложи правки " +
            $"инструментом {UpdateObjectTool.ToolName}.");

        return builder.ToString();
    }
}
