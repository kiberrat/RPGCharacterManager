using System.Text;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Ai.Tools;
using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Ai;

/// <summary>
/// Помощник приложения: отвечает на вопросы, разбирает источники и готовит
/// предложения изменить игровые данные.
///
/// Помощник не хранит состояния: беседа передаётся ему при каждом обращении.
/// Благодаря этому одна и та же реализация обслуживает и окно чата, и разбор
/// книги, каждая часть которой обрабатывается отдельной короткой беседой.
/// </summary>
public sealed class AiAssistant : IAiAssistant
{
    /// <summary>Сколько раз подряд помощник может обратиться к модели, отвечая на один вопрос.</summary>
    public const int MaximumRounds = 10;

    private readonly IAiClient _client;
    private readonly IContentService _content;
    private readonly ISettingsService _settings;
    private readonly AiContentWriter _writer;
    private readonly ILogger<AiAssistant> _logger;
    private readonly Dictionary<string, IAiTool> _tools;

    /// <summary>
    /// Создаёт помощника.
    /// </summary>
    /// <param name="client">Клиент службы языковой модели.</param>
    /// <param name="content">Служба контента.</param>
    /// <param name="settings">Служба пользовательских настроек.</param>
    /// <param name="tools">Действия, доступные помощнику.</param>
    /// <param name="logger">Журналировщик.</param>
    public AiAssistant(
        IAiClient client,
        IContentService content,
        ISettingsService settings,
        IEnumerable<IAiTool> tools,
        ILogger<AiAssistant> logger)
    {
        _client = Guard.NotNull(client);
        _content = Guard.NotNull(content);
        _settings = Guard.NotNull(settings);
        _logger = Guard.NotNull(logger);
        _writer = new AiContentWriter(content);

        Tools = Guard.NotNull(tools).OrderBy(tool => tool.Name, StringComparer.Ordinal).ToList();
        _tools = Tools.ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyList<IAiTool> Tools { get; }

    /// <inheritdoc />
    public int CountParts(string text) => AiTextSplitter.Split(text).Count;

    /// <inheritdoc />
    public Task<Result<AiAnswer>> AskAsync(
        AiConversation conversation,
        string question,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(conversation);

        if (string.IsNullOrWhiteSpace(question))
        {
            return Task.FromResult(Result.Failure<AiAnswer>("Вопрос пуст."));
        }

        conversation.Add(AiMessage.User(question.Trim()));

        return RunAsync(conversation, conversation, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<AiAnswer>> AnalyzeAsync(
        AiConversation conversation,
        AiSource source,
        IProgress<AiProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(conversation);
        Guard.NotNull(source);

        if (string.IsNullOrWhiteSpace(source.Text))
        {
            return Result.Failure<AiAnswer>("Разбирать нечего: источник пуст.");
        }

        var parts = AiTextSplitter.Split(source.Text);
        var proposals = new List<AiProposal>();
        var steps = new List<string>();
        var usage = default(AiUsage);
        var summary = new StringBuilder();

        conversation.Add(AiMessage.User(
            $"Разбери источник «{source.Name}» ({parts.Count} частей) и предложи создать найденные объекты."));

        for (var index = 0; index < parts.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var step = index + 1;

            AiLog.SourceChunkStarted(_logger, source.Name, step, parts.Count);
            progress?.Report(new AiProgress(step, parts.Count, $"Часть {step} из {parts.Count}"));

            // Каждая часть разбирается в отдельной короткой беседе: иначе к концу
            // книги запрос состоял бы из её начала и перестал бы помещаться в модель.
            var chunk = new AiConversation { Scope = conversation.Scope };

            chunk.Add(AiMessage.User(
                AiPrompt.BuildAnalysis(source.Name, step, parts.Count)
                + Environment.NewLine + Environment.NewLine
                + "Текст части:" + Environment.NewLine + parts[index]));

            var answer = await RunAsync(chunk, conversation, cancellationToken).ConfigureAwait(false);

            if (answer.IsFailure)
            {
                return summary.Length == 0
                    ? Result.Failure<AiAnswer>(answer.Error!)
                    : Result.Success(new AiAnswer(
                        summary.Append("Разбор прерван на части ").Append(step).Append(": ")
                            .AppendLine(answer.Error).ToString(),
                        proposals,
                        steps,
                        usage));
            }

            proposals.AddRange(answer.Value.Proposals);
            steps.AddRange(answer.Value.Steps);
            usage = usage.Add(answer.Value.Usage);

            if (answer.Value.Text.Length > 0)
            {
                summary.Append("Часть ").Append(step).Append(": ").AppendLine(answer.Value.Text);
            }
        }

        summary.Append("Разобрано частей: ").Append(parts.Count)
            .Append(". Подготовлено предложений: ").Append(proposals.Count).Append('.');

        var text = summary.ToString();

        conversation.Add(AiMessage.Assistant(text));

        return Result.Success(new AiAnswer(text, proposals, steps, usage));
    }

    /// <inheritdoc />
    public async Task<Result> ApplyAsync(AiProposal proposal, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(proposal);

        if (proposal.State == AiProposalState.Applied)
        {
            return Result.Failure("Это предложение уже применено.");
        }

        var descriptor = _content.FindType(proposal.TypeId);

        if (descriptor is null)
        {
            return Fail(proposal, $"Вид объектов «{proposal.TypeId}» больше не зарегистрирован.");
        }

        try
        {
            var result = await WriteAsync(descriptor, proposal, cancellationToken).ConfigureAwait(false);

            if (result.IsFailure)
            {
                return Fail(proposal, result.Error!);
            }

            proposal.State = AiProposalState.Applied;
            proposal.Error = null;

            AiLog.ProposalApplied(_logger, proposal.Summary);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AiLog.AiOperationFailed(_logger, exception);

            return Fail(proposal, $"Не удалось применить предложение: {exception.Message}");
        }
    }

    /// <summary>
    /// Записывает предложение в базу данных.
    /// </summary>
    /// <param name="descriptor">Описание вида контента.</param>
    /// <param name="proposal">Предложение.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат записи.</returns>
    private async Task<Result> WriteAsync(
        IContentTypeDescriptor descriptor,
        AiProposal proposal,
        CancellationToken cancellationToken)
    {
        var entity = await PrepareAsync(descriptor, proposal, cancellationToken).ConfigureAwait(false);

        if (entity.IsFailure)
        {
            return Result.Failure(entity.Error!);
        }

        if (proposal.ListName is { Length: > 0 })
        {
            var list = descriptor.Collections
                .FirstOrDefault(item => item.Name.Equals(proposal.ListName, StringComparison.OrdinalIgnoreCase));

            if (list is null)
            {
                return Result.Failure($"Списка «{proposal.ListName}» у этого вида объектов больше нет.");
            }

            var item = list.AddItem(entity.Value);

            var nested = await _writer
                .FillAsync(list.Fields, item, proposal.Values, trackOldValues: false, cancellationToken)
                .ConfigureAwait(false);

            if (nested.Changes.Count == 0 && nested.Problems.Count > 0)
            {
                return Result.Failure(string.Join(AiContentText.Separator, nested.Problems));
            }
        }
        else
        {
            var outcome = await _writer
                .FillAsync(descriptor.Fields, entity.Value, proposal.Values, trackOldValues: false, cancellationToken)
                .ConfigureAwait(false);

            if (outcome.Changes.Count == 0 && outcome.Problems.Count > 0)
            {
                return Result.Failure(string.Join(AiContentText.Separator, outcome.Problems));
            }
        }

        return await _content.SaveAsync(descriptor.Id, entity.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Готовит объект, к которому применяется предложение.
    /// </summary>
    /// <param name="descriptor">Описание вида контента.</param>
    /// <param name="proposal">Предложение.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Объект либо описание ошибки.</returns>
    private async Task<Result<Core.Models.Entities.EntityBase>> PrepareAsync(
        IContentTypeDescriptor descriptor,
        AiProposal proposal,
        CancellationToken cancellationToken)
    {
        if (proposal.Kind == AiProposalKind.Create && proposal.TargetId is null)
        {
            return Result.Success(descriptor.CreateInstance());
        }

        if (proposal.TargetId is not { } identifier)
        {
            return Result.Failure<Core.Models.Entities.EntityBase>(
                "В предложении не указан объект, к которому оно относится.");
        }

        if (proposal.Kind == AiProposalKind.Create)
        {
            var copy = await _content.DuplicateAsync(descriptor.Id, identifier, cancellationToken)
                .ConfigureAwait(false);

            return copy.IsFailure
                ? Result.Failure<Core.Models.Entities.EntityBase>(copy.Error!)
                : Result.Success(copy.Value);
        }

        var entity = await _content.GetAsync(descriptor.Id, identifier, cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? Result.Failure<Core.Models.Entities.EntityBase>("Объект больше не существует: возможно, он удалён.")
            : Result.Success(entity);
    }

    /// <summary>
    /// Ведёт переписку с моделью, пока она не перестанет вызывать инструменты.
    /// </summary>
    /// <param name="conversation">Беседа, отправляемая модели.</param>
    /// <param name="destination">Беседа, в которую попадают предложения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ помощника либо описание ошибки.</returns>
    private async Task<Result<AiAnswer>> RunAsync(
        AiConversation conversation,
        AiConversation destination,
        CancellationToken cancellationToken)
    {
        if (!_client.IsConfigured)
        {
            return Result.Failure<AiAnswer>(
                "Ключ доступа не задан. Откройте «Настройки» → «Помощник» и введите ключ Groq.");
        }

        var instructions = AiPrompt.Build(_content, conversation.Scope, _settings.Current.AiStyle);
        var specifications = Tools.Select(tool => tool.Describe()).ToList();
        var proposals = new List<AiProposal>();
        var steps = new List<string>();
        var usage = default(AiUsage);
        var reminded = false;

        for (var round = 1; round <= MaximumRounds; round++)
        {
            var messages = new List<AiMessage> { AiMessage.System(instructions) };

            messages.AddRange(conversation.Recall());

            var request = new AiRequest(messages) { Tools = specifications };
            var reply = await _client.CompleteAsync(request, cancellationToken).ConfigureAwait(false);

            if (reply.IsFailure)
            {
                return Result.Failure<AiAnswer>(reply.Error!);
            }

            usage = usage.Add(reply.Value.Usage);
            conversation.Add(AiMessage.Assistant(reply.Value.Text, reply.Value.Calls));

            if (reply.Value.Calls.Count == 0)
            {
                // Модель, замолчавшая без единого слова, работу не закончила:
                // она успела подготовиться и остановилась, не вызвав инструмент.
                // Одно напоминание дешевле, чем ответ, в котором ничего не сделано.
                if (reply.Value.Text.Length == 0 && !reminded)
                {
                    reminded = true;
                    conversation.Add(AiMessage.User(AiPrompt.Reminder));

                    continue;
                }

                AiLog.AnswerProduced(_logger, round, usage.Total);

                return Result.Success(new AiAnswer(reply.Value.Text, proposals, steps, usage));
            }

            foreach (var call in reply.Value.Calls)
            {
                var result = await InvokeAsync(call, cancellationToken).ConfigureAwait(false);

                steps.Add(Describe(call));

                foreach (var proposal in result.Proposals)
                {
                    destination.Add(proposal);
                    proposals.Add(proposal);
                }

                conversation.Add(AiMessage.Tool(call.Id, call.Name, result.Text));
            }
        }

        // Предел обращений достигнут. Ответ всё равно возвращается: подготовленные
        // предложения уже существуют, и терять их из-за многословности модели незачем.
        return Result.Success(new AiAnswer(
            $"Помощник сделал {MaximumRounds} шагов и не завершил работу. " +
            "Уточните просьбу или разбейте её на части.",
            proposals,
            steps,
            usage));
    }

    /// <summary>
    /// Выполняет вызов инструмента, запрошенный моделью.
    /// </summary>
    /// <param name="call">Вызов инструмента.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат работы инструмента.</returns>
    private async Task<AiToolResult> InvokeAsync(AiToolCall call, CancellationToken cancellationToken)
    {
        if (!_tools.TryGetValue(call.Name, out var tool))
        {
            return AiToolResult.Answer(
                $"Инструмента «{call.Name}» не существует. Доступны: " +
                string.Join(AiContentText.Separator, _tools.Keys) + ".");
        }

        AiLog.ToolInvoked(_logger, call.Name);

        try
        {
            return await tool.InvokeAsync(call.Arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AiLog.ToolFailed(_logger, exception, call.Name);

            // Сбой инструмента возвращается модели как обычный ответ: она сможет
            // исправить аргументы и повторить вызов, а не оборвёт работу.
            return AiToolResult.Answer($"Инструмент завершился ошибкой: {exception.Message}");
        }
    }

    /// <summary>
    /// Описывает вызов инструмента для показа пользователю.
    /// </summary>
    /// <param name="call">Вызов инструмента.</param>
    /// <returns>Строка вида «Поиск объектов: weapons».</returns>
    private string Describe(AiToolCall call)
    {
        var title = _tools.TryGetValue(call.Name, out var tool) ? tool.Title : call.Name;

        string[] interesting =
        [
            AiToolParameters.Type,
            AiToolParameters.Id,
            AiToolParameters.Query,
            AiToolParameters.List,
            AiToolParameters.Formula,
        ];

        var details = interesting
            .Select(call.Arguments.Text)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return details.Count == 0
            ? title
            : $"{title}: {string.Join(" · ", details)}";
    }

    /// <summary>
    /// Отмечает предложение как неприменённое и возвращает ошибку.
    /// </summary>
    /// <param name="proposal">Предложение.</param>
    /// <param name="error">Описание ошибки.</param>
    /// <returns>Неуспешный результат.</returns>
    private Result Fail(AiProposal proposal, string error)
    {
        proposal.State = AiProposalState.Failed;
        proposal.Error = error;

        AiLog.ProposalFailed(_logger, proposal.Summary);

        return Result.Failure(error);
    }
}
