using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Ai;

/// <summary>
/// Что помощник знает о происходящем в приложении.
///
/// Документ 024_AI_Помощник.md требует, чтобы помощник понимал текущего персонажа
/// и учитывал его в советах. Область передаётся беседой, поэтому помощник получает
/// её от интерфейса и не обращается к нему сам.
/// </summary>
/// <param name="CharacterId">Идентификатор выбранного персонажа.</param>
/// <param name="CharacterName">Имя выбранного персонажа.</param>
public sealed record AiScope(Guid? CharacterId, string? CharacterName)
{
    /// <summary>Персонаж не выбран.</summary>
    public static AiScope None { get; } = new(null, null);
}

/// <summary>
/// Источник текста для разбора: книга, правило, фрагмент, вставленный пользователем.
/// </summary>
/// <param name="Name">Название источника.</param>
/// <param name="Text">Текст источника.</param>
public sealed record AiSource(string Name, string Text);

/// <summary>
/// Ход разбора длинного источника.
/// </summary>
/// <param name="Step">Номер обрабатываемой части, начиная с единицы.</param>
/// <param name="Total">Общее количество частей.</param>
/// <param name="Title">Что делается сейчас.</param>
public sealed record AiProgress(int Step, int Total, string Title);

/// <summary>
/// Ответ помощника на запрос пользователя.
/// </summary>
/// <param name="Text">Текст ответа.</param>
/// <param name="Proposals">Новые предложения изменить данные.</param>
/// <param name="Steps">Что помощник сделал по пути: вызванные инструменты и их итоги.</param>
/// <param name="Usage">Израсходованные единицы обработки текста.</param>
public sealed record AiAnswer(
    string Text,
    IReadOnlyList<AiProposal> Proposals,
    IReadOnlyList<string> Steps,
    AiUsage Usage);

/// <summary>
/// Беседа с помощником: переписка и накопленные предложения.
///
/// Беседа принадлежит интерфейсу и передаётся помощнику при каждом запросе,
/// поэтому сам помощник не хранит состояния и одинаково пригоден и для окна чата,
/// и для разбора книги, и для проверки в тестах.
/// </summary>
public sealed class AiConversation
{
    /// <summary>Сколько последних сообщений переписки передаётся модели.</summary>
    public const int DefaultMemoryLimit = 40;

    private readonly List<AiMessage> _messages = [];
    private readonly List<AiProposal> _proposals = [];

    /// <summary>Переписка в порядке появления сообщений.</summary>
    public IReadOnlyList<AiMessage> Messages => _messages;

    /// <summary>Предложения помощника в порядке появления.</summary>
    public IReadOnlyList<AiProposal> Proposals => _proposals;

    /// <summary>Что помощник знает о происходящем в приложении.</summary>
    public AiScope Scope { get; set; } = AiScope.None;

    /// <summary>
    /// Добавляет сообщение в переписку.
    /// </summary>
    /// <param name="message">Сообщение.</param>
    public void Add(AiMessage message) => _messages.Add(message);

    /// <summary>
    /// Добавляет предложение помощника.
    /// </summary>
    /// <param name="proposal">Предложение.</param>
    public void Add(AiProposal proposal) => _proposals.Add(proposal);

    /// <summary>
    /// Очищает переписку и предложения.
    /// </summary>
    public void Clear()
    {
        _messages.Clear();
        _proposals.Clear();
    }

    /// <summary>
    /// Возвращает последние сообщения переписки.
    ///
    /// Длинная беседа не должна отправляться модели целиком: разбор книги
    /// добавляет десятки сообщений, и без ограничения запрос перестал бы
    /// помещаться в допустимый размер.
    /// </summary>
    /// <param name="limit">Сколько сообщений оставить.</param>
    /// <returns>Сообщения, пригодные для передачи модели.</returns>
    public IReadOnlyList<AiMessage> Recall(int limit = DefaultMemoryLimit)
    {
        if (_messages.Count <= limit)
        {
            return _messages;
        }

        // Начало отрезка не должно попасть на ответ инструмента: без сообщения
        // модели с вызовом такой ответ становится бессмысленным и отвергается службой.
        var start = _messages.Count - limit;

        while (start < _messages.Count && _messages[start].Role == AiRole.Tool)
        {
            start++;
        }

        return _messages.GetRange(start, _messages.Count - start);
    }
}

/// <summary>
/// Помощник: отвечает на вопросы, разбирает источники и предлагает изменения.
/// </summary>
public interface IAiAssistant
{
    /// <summary>Действия, доступные помощнику.</summary>
    IReadOnlyList<IAiTool> Tools { get; }

    /// <summary>
    /// Возвращает, на сколько частей разобьётся источник.
    ///
    /// Каждая часть — отдельное обращение к службе, поэтому число частей
    /// показывается пользователю до начала разбора: книга на пятьсот страниц
    /// обойдётся дороже и дольше, чем страница правил, и знать об этом
    /// он должен заранее.
    /// </summary>
    /// <param name="text">Текст источника.</param>
    /// <returns>Количество частей разбора.</returns>
    int CountParts(string text);

    /// <summary>
    /// Отвечает на вопрос пользователя, при необходимости обращаясь к данным
    /// приложения через инструменты.
    /// </summary>
    /// <param name="conversation">Беседа, к которой относится вопрос.</param>
    /// <param name="question">Вопрос пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ помощника либо описание ошибки.</returns>
    Task<Result<AiAnswer>> AskAsync(
        AiConversation conversation,
        string question,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Разбирает источник и предлагает создать найденные в нём игровые объекты.
    ///
    /// Длинный текст обрабатывается частями: книга не помещается в один запрос.
    /// </summary>
    /// <param name="conversation">Беседа, к которой относится разбор.</param>
    /// <param name="source">Разбираемый источник.</param>
    /// <param name="progress">Получатель сведений о ходе разбора.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итог разбора либо описание ошибки.</returns>
    Task<Result<AiAnswer>> AnalyzeAsync(
        AiConversation conversation,
        AiSource source,
        IProgress<AiProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Применяет предложение помощника к базе данных.
    /// </summary>
    /// <param name="proposal">Предложение, подтверждённое пользователем.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат применения.</returns>
    Task<Result> ApplyAsync(AiProposal proposal, CancellationToken cancellationToken = default);
}
