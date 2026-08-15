using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Models.Ai;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Logging;
using RPGCharacterManager.UI.Shell;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Документ «AI»: беседа с помощником, разбор книг и список предложенных изменений.
///
/// Помощник ничего не меняет сам. Всё, что он предлагает, попадает в правый
/// столбец и применяется только нажатием пользователя — так требует документ
/// 024_AI_Помощник.md.
/// </summary>
public sealed partial class AiViewModel : DocumentViewModelBase
{
    private const int CharacterLimit = 200;

    private readonly IAiAssistant _assistant;
    private readonly IAiClient _client;
    private readonly IAiLibrary _library;
    private readonly ICharacterService _characters;
    private readonly INotificationService _notifications;
    private readonly IFilePicker _picker;
    private readonly ILogger<AiViewModel> _logger;
    private readonly AiConversation _conversation = new();

    [ObservableProperty]
    private string _question = string.Empty;

    [ObservableProperty]
    private AiCharacterOption? _selectedCharacter;

    [ObservableProperty]
    private AiBook? _selectedBook;

    [ObservableProperty]
    private string _connectionText = "Связь не проверена";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isConnectionFailed;

    [ObservableProperty]
    private string _progressText = string.Empty;

    /// <summary>
    /// Что найдено в выбранном документе.
    ///
    /// Разбор книги стоит обращений к службе и времени, поэтому прежде показывается,
    /// из чего эта книга состоит и во сколько частей обойдётся её разбор.
    /// </summary>
    [ObservableProperty]
    private string _documentSummary = string.Empty;

    /// <summary>
    /// Помощник занят: идёт обращение к службе.
    ///
    /// Признак хранится отдельно, а не выводится из состояния команды: состояние
    /// команды меняется уже после выхода из её тела, и уведомление об изменении
    /// вычисляемого признака приходило раньше времени — поле ввода оставалось
    /// заблокированным до переоткрытия раздела.
    /// </summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Создаёт модель представления раздела помощника.
    /// </summary>
    /// <param name="assistant">Помощник.</param>
    /// <param name="client">Клиент службы языковой модели.</param>
    /// <param name="library">Библиотека книг.</param>
    /// <param name="characters">Служба персонажей.</param>
    /// <param name="notifications">Служба всплывающих уведомлений.</param>
    /// <param name="picker">Обзор файлов.</param>
    /// <param name="logger">Журналировщик.</param>
    public AiViewModel(
        IAiAssistant assistant,
        IAiClient client,
        IAiLibrary library,
        ICharacterService characters,
        INotificationService notifications,
        IFilePicker picker,
        ILogger<AiViewModel> logger)
        : base(AiShellContributor.AssistantDocumentId, "AI")
    {
        _assistant = Guard.NotNull(assistant);
        _client = Guard.NotNull(client);
        _library = Guard.NotNull(library);
        _characters = Guard.NotNull(characters);
        _notifications = Guard.NotNull(notifications);
        _picker = Guard.NotNull(picker);
        _logger = Guard.NotNull(logger);
    }

    /// <summary>Сообщения беседы в порядке появления.</summary>
    public ObservableCollection<AiMessageViewModel> Messages { get; } = [];

    /// <summary>Предложения помощника, ожидающие решения пользователя.</summary>
    public ObservableCollection<AiProposalViewModel> Proposals { get; } = [];

    /// <summary>Персонажи, доступные для выбора.</summary>
    public ObservableCollection<AiCharacterOption> Characters { get; } = [];

    /// <summary>Книги, найденные в каталоге книг.</summary>
    public ObservableCollection<AiBook> Books { get; } = [];

    /// <summary>Беседа пуста.</summary>
    public bool IsEmpty => Messages.Count == 0;

    /// <summary>Предложений нет.</summary>
    public bool HasNoProposals => Proposals.Count == 0;

    /// <summary>Есть предложения, ожидающие решения.</summary>
    public bool HasPendingProposals => Proposals.Any(item => item.IsPending);

    /// <summary>Книг в каталоге не найдено.</summary>
    public bool HasNoBooks => Books.Count == 0;

    /// <summary>Каталог, в котором приложение ищет документы.</summary>
    public string BooksDirectory => _library.Directory;

    /// <summary>Расширения документов, доступных для разбора.</summary>
    public string SupportedBooks => string.Join(", ", _library.SupportedExtensions);

    /// <summary>Сведения о документе получены.</summary>
    public bool HasDocumentSummary => DocumentSummary.Length > 0;

    /// <inheritdoc />
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await ReloadCharactersAsync(cancellationToken).ConfigureAwait(true);

        RefreshBooks();

        await CheckConnectionAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Проверяет связь со службой языковой модели.
    ///
    /// Это и есть подтверждение работоспособности: проверка обращается к службе
    /// настоящим запросом и показывает выбранную модель, время ответа и то, что
    /// модель ответила по-русски.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после проверки.</returns>
    [RelayCommand]
    private async Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        if (!_client.IsConfigured)
        {
            SetConnection(false, true, "Ключ не задан — откройте «Настройки» → «Помощник»");
            return;
        }

        SetConnection(false, false, "Проверяем связь…");

        var result = await _client.CheckAsync(cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            SetConnection(false, true, result.Error!);
            return;
        }

        var connection = result.Value;

        SetConnection(
            true,
            false,
            $"Связь есть · {connection.Model} · {Seconds(connection.Latency)} с · " +
            $"моделей доступно: {connection.AvailableModels} · ответ модели: «{Trim(connection.Answer)}»");
    }

    /// <summary>
    /// Отправляет вопрос помощнику.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после получения ответа.</returns>
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SendAsync(CancellationToken cancellationToken)
    {
        var question = Question.Trim();

        if (question.Length == 0)
        {
            return;
        }

        Question = string.Empty;

        Add(new AiMessageViewModel(AiAuthor.User, question));

        var watch = Stopwatch.StartNew();

        IsBusy = true;

        try
        {
            var answer = await _assistant.AskAsync(_conversation, question, cancellationToken)
                .ConfigureAwait(true);

            watch.Stop();

            Complete(answer.IsSuccess ? answer.Value : null, answer.Error, watch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            Add(new AiMessageViewModel(AiAuthor.Application, "Запрос остановлен."));
        }
        catch (Exception exception)
        {
            // Ответ службы может оказаться каким угодно. Приложение обязано
            // сообщить о сбое строкой в беседе, а не закрыться вместе с ним.
            UiLog.AiSectionFailed(_logger, exception);

            Add(new AiMessageViewModel(AiAuthor.Application, $"Непредвиденный сбой: {exception.Message}"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Разбирает выбранную книгу и предлагает создать найденные объекты.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после разбора.</returns>
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task AnalyzeAsync(CancellationToken cancellationToken)
    {
        if (SelectedBook is not { } book)
        {
            return;
        }

        var source = await _library.ReadAsync(book, cancellationToken).ConfigureAwait(true);

        if (source.IsFailure)
        {
            Add(new AiMessageViewModel(AiAuthor.Application, source.Error!));
            return;
        }

        Add(new AiMessageViewModel(AiAuthor.User, $"Разбери книгу «{book.Name}»."));

        var progress = new Progress<AiProgress>(step =>
            ProgressText = $"{step.Title}…");

        var watch = Stopwatch.StartNew();

        IsBusy = true;

        try
        {
            var answer = await _assistant
                .AnalyzeAsync(_conversation, source.Value, progress, cancellationToken)
                .ConfigureAwait(true);

            watch.Stop();

            Complete(answer.IsSuccess ? answer.Value : null, answer.Error, watch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            Add(new AiMessageViewModel(AiAuthor.Application, "Разбор остановлен."));
        }
        finally
        {
            ProgressText = string.Empty;
            IsBusy = false;
        }
    }

    /// <summary>
    /// Подставляет в поле ввода готовый запрос выбранной возможности.
    /// </summary>
    /// <param name="capability">Возможность помощника.</param>
    [RelayCommand]
    private void UseCapability(AiCapability? capability)
    {
        if (capability is not null)
        {
            Question = capability.Example;
        }
    }

    /// <summary>
    /// Применяет предложение помощника к базе данных.
    /// </summary>
    /// <param name="row">Строка предложения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после применения.</returns>
    [RelayCommand]
    private async Task ApplyAsync(AiProposalViewModel? row, CancellationToken cancellationToken)
    {
        if (row is null || !row.IsPending)
        {
            return;
        }

        var result = await _assistant.ApplyAsync(row.Proposal, cancellationToken).ConfigureAwait(true);

        row.Refresh();
        NotifyProposals();

        if (result.IsFailure)
        {
            _notifications.Show(result.Error!, NotificationKind.Error);
            return;
        }

        _notifications.Show($"{row.Summary} — применено", NotificationKind.Success);
    }

    /// <summary>
    /// Отклоняет предложение помощника.
    /// </summary>
    /// <param name="row">Строка предложения.</param>
    [RelayCommand]
    private void Reject(AiProposalViewModel? row)
    {
        if (row is null || !row.IsPending)
        {
            return;
        }

        row.Proposal.State = AiProposalState.Rejected;
        row.Refresh();
        NotifyProposals();
    }

    /// <summary>
    /// Применяет все предложения, ожидающие решения.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после применения.</returns>
    [RelayCommand]
    private async Task ApplyAllAsync(CancellationToken cancellationToken)
    {
        var pending = Proposals.Where(item => item.IsPending).ToList();
        var applied = 0;

        foreach (var row in pending)
        {
            var result = await _assistant.ApplyAsync(row.Proposal, cancellationToken).ConfigureAwait(true);

            row.Refresh();

            if (result.IsSuccess)
            {
                applied++;
            }
        }

        NotifyProposals();

        _notifications.Show(
            applied == pending.Count
                ? $"Применено предложений: {applied}"
                : $"Применено {applied} из {pending.Count}; остальные отмечены ошибкой",
            applied == pending.Count ? NotificationKind.Success : NotificationKind.Warning);
    }

    /// <summary>
    /// Очищает беседу и список предложений.
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        _conversation.Clear();
        Messages.Clear();
        Proposals.Clear();

        OnPropertyChanged(nameof(IsEmpty));
        NotifyProposals();
    }

    /// <summary>
    /// Выбирает документ за пределами каталога приложения.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после выбора.</returns>
    [RelayCommand]
    private async Task PickFileAsync(CancellationToken cancellationToken)
    {
        var path = await _picker
            .PickAsync("Выберите документ для разбора", "Документы", _library.SupportedExtensions)
            .ConfigureAwait(true);

        if (path is null)
        {
            return;
        }

        var file = new FileInfo(path);

        // Выбранный файл встаёт в начало списка и становится выбранным: дальше
        // он ничем не отличается от документа, лежащего в каталоге приложения.
        var book = new AiBook(Path.GetFileNameWithoutExtension(file.Name), file.FullName, file.Length);

        Books.Insert(0, book);
        SelectedBook = book;

        OnPropertyChanged(nameof(HasNoBooks));

        await InspectAsync(book, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Читает выбранный документ и показывает, что в нём найдено.
    /// </summary>
    /// <param name="book">Документ.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после чтения.</returns>
    private async Task InspectAsync(AiBook book, CancellationToken cancellationToken)
    {
        SetSummary("Читаем документ…");

        try
        {
            var document = await _library.InspectAsync(book.Path, cancellationToken).ConfigureAwait(true);

            if (document.IsFailure)
            {
                SetSummary(document.Error!);
                return;
            }

            var parts = _assistant.CountParts(document.Value.Text);
            var details = new List<string> { document.Value.Format };

            details.AddRange(document.Value.Notes);
            details.Add($"знаков: {document.Value.Text.Length:N0}");
            details.Add($"частей разбора: {parts}");

            SetSummary(string.Join(" · ", details));
        }
        catch (OperationCanceledException)
        {
            SetSummary(string.Empty);
        }
        catch (Exception exception)
        {
            UiLog.AiSectionFailed(_logger, exception);
            SetSummary($"Прочитать документ не удалось: {exception.Message}");
        }
    }

    /// <summary>
    /// Перечитывает список документов в каталоге.
    /// </summary>
    [RelayCommand]
    private void RefreshBooks()
    {
        var selected = SelectedBook?.Path;

        Books.Clear();

        foreach (var book in _library.GetBooks())
        {
            Books.Add(book);
        }

        SelectedBook = Books.FirstOrDefault(book =>
            string.Equals(book.Path, selected, StringComparison.OrdinalIgnoreCase)) ?? Books.FirstOrDefault();

        OnPropertyChanged(nameof(HasNoBooks));
    }

    /// <summary>
    /// Открывает каталог книг в проводнике.
    /// </summary>
    [RelayCommand]
    private void OpenBooksFolder()
    {
        try
        {
            Directory.CreateDirectory(_library.Directory);
            Process.Start(new ProcessStartInfo(_library.Directory) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            UiLog.FolderOpenFailed(_logger, exception, _library.Directory);
            _notifications.Show("Не удалось открыть папку книг", NotificationKind.Warning);
        }
    }

    /// <summary>
    /// Завершает обращение к помощнику: показывает ответ и новые предложения.
    /// </summary>
    /// <param name="answer">Ответ помощника.</param>
    /// <param name="error">Описание ошибки.</param>
    /// <param name="elapsed">Сколько времени заняло обращение.</param>
    private void Complete(AiAnswer? answer, string? error, TimeSpan elapsed)
    {
        if (answer is null)
        {
            Add(new AiMessageViewModel(AiAuthor.Application, error ?? "Помощник не ответил."));

            return;
        }

        var text = answer.Text.Length > 0
            ? answer.Text
            : "Помощник не добавил пояснения: смотрите список действий справа.";

        Add(new AiMessageViewModel(AiAuthor.Assistant, text, Describe(answer, elapsed)));

        foreach (var proposal in answer.Proposals)
        {
            Proposals.Add(new AiProposalViewModel(proposal));
        }

        NotifyProposals();
    }

    /// <summary>
    /// Составляет перечень выполненных действий с итогом обращения.
    ///
    /// Пустой перечень тоже показывается, и это важнее всего остального: модель
    /// может расписать созданный объект, не вызвав ни одного инструмента, и тогда
    /// в базе не появилось ничего. Строка «инструменты не вызывались» сразу
    /// отличает сделанную работу от рассказа о ней.
    /// </summary>
    /// <param name="answer">Ответ помощника.</param>
    /// <param name="elapsed">Сколько времени заняло обращение.</param>
    /// <returns>Строки перечня.</returns>
    private static List<string> Describe(AiAnswer answer, TimeSpan elapsed)
    {
        const string bullet = "· ";

        var steps = answer.Steps.Count > 0
            ? answer.Steps.Select(step => bullet + step).ToList()
            : [bullet + "Инструменты не вызывались: помощник только ответил текстом, данные не менялись"];

        steps.Add($"{bullet}Готово за {Seconds(elapsed)} с · единиц текста: {answer.Usage.Total}");

        return steps;
    }

    private async Task ReloadCharactersAsync(CancellationToken cancellationToken)
    {
        var page = await _characters.SearchAsync(null, 0, CharacterLimit, cancellationToken)
            .ConfigureAwait(true);

        Characters.Clear();
        Characters.Add(new AiCharacterOption(null, "Без персонажа"));

        foreach (var character in page.Items)
        {
            Characters.Add(new AiCharacterOption(character.Id, character.Name));
        }

        SelectedCharacter = Characters[0];
    }

    private void Add(AiMessageViewModel message)
    {
        Messages.Add(message);

        OnPropertyChanged(nameof(IsEmpty));
    }

    private void SetSummary(string text)
    {
        DocumentSummary = text;

        OnPropertyChanged(nameof(HasDocumentSummary));
    }

    private void SetConnection(bool connected, bool failed, string text)
    {
        IsConnected = connected;
        IsConnectionFailed = failed;
        ConnectionText = text;
    }

    private void NotifyProposals()
    {
        OnPropertyChanged(nameof(HasNoProposals));
        OnPropertyChanged(nameof(HasPendingProposals));
    }

    private static string Seconds(TimeSpan value) =>
        value.TotalSeconds.ToString("0.0", CultureInfo.CurrentCulture);

    private static string Trim(string text)
    {
        const int limit = 40;

        var single = text.ReplaceLineEndings(" ").Trim();

        return single.Length <= limit ? single : string.Concat(single.AsSpan(0, limit), "…");
    }

    partial void OnSelectedBookChanged(AiBook? value)
    {
        SetSummary(string.Empty);

        if (value is not null)
        {
            _ = InspectAsync(value, CancellationToken.None);
        }
    }

    partial void OnSelectedCharacterChanged(AiCharacterOption? value) =>
        _conversation.Scope = new AiScope(value?.Id, value?.Id is null ? null : value.Name);
}
