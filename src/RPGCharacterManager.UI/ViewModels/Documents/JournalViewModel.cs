using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.History;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Строка журнала событий.
/// </summary>
public sealed class JournalRowViewModel : ViewModelBase
{
    private const string DayFormat = "dd.MM.yyyy";
    private const string TimeFormat = "HH:mm:ss";

    /// <summary>
    /// Создаёт строку журнала.
    /// </summary>
    /// <param name="record">Запись журнала.</param>
    public JournalRowViewModel(HistoryRecord record) => Record = Guard.NotNull(record);

    /// <summary>Запись журнала.</summary>
    public HistoryRecord Record { get; }

    /// <summary>Дата события.</summary>
    public string Day => Record.Timestamp.ToLocalTime().ToString(DayFormat, CultureInfo.CurrentCulture);

    /// <summary>Время события.</summary>
    public string Time => Record.Timestamp.ToLocalTime().ToString(TimeFormat, CultureInfo.CurrentCulture);

    /// <summary>Название события.</summary>
    public string Title => Record.Title;

    /// <summary>Описание события.</summary>
    public string Description => Record.Description ?? string.Empty;

    /// <summary>Имя персонажа.</summary>
    public string Character => Record.CharacterName ?? "без персонажа";

    /// <summary>Изменение значения в виде «было → стало».</summary>
    public string Change => (Record.OldValue, Record.NewValue) switch
    {
        ({ } old, { } created) => $"{old} → {created}",
        (null, { } created) => created,
        ({ } old, null) => old,
        _ => string.Empty,
    };

    /// <summary>Событие содержит изменение значения.</summary>
    public bool HasChange => Change.Length > 0;

    /// <summary>Вид события — бросок.</summary>
    public bool IsRoll => Record.Kind == HistoryKind.Roll;

    /// <summary>Вид события — изменение ресурса.</summary>
    public bool IsResource => Record.Kind == HistoryKind.Resource;

    /// <summary>Вид события — экипировка.</summary>
    public bool IsEquipment => Record.Kind == HistoryKind.Equipment;
}

/// <summary>
/// Вид события в списке отбора.
/// </summary>
/// <param name="Kind">Вид события.</param>
/// <param name="Title">Название вида.</param>
public sealed record JournalKindOption(HistoryKind Kind, string Title);

/// <summary>
/// Документ «Журнал»: что происходило с персонажами и когда.
///
/// Записи создают сами подсистемы, поэтому документ не знает, откуда взялось
/// событие: он показывает и бросок, и расход ресурса, и смену экипировки одним
/// списком в порядке времени.
/// </summary>
public sealed partial class JournalViewModel : DocumentViewModelBase, IDisposable
{
    /// <summary>Сколько записей добавляет одно нажатие «Показать больше».</summary>
    public const int PageSize = HistoryQuery.DefaultLimit;

    private readonly IHistoryService _history;
    private readonly IDialogService _dialogs;
    private readonly IDisposable _subscription;

    [ObservableProperty]
    private CharacterFilterOption? _selectedCharacter;

    [ObservableProperty]
    private JournalKindOption? _selectedKind;

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private int _limit = PageSize;

    [ObservableProperty]
    private int _total;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Создаёт модель представления журнала.
    /// </summary>
    /// <param name="history">Служба журнала событий.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    /// <param name="eventBus">Шина событий приложения.</param>
    /// <param name="dispatcher">Диспетчер потока пользовательского интерфейса.</param>
    public JournalViewModel(
        IHistoryService history,
        IDialogService dialogs,
        IEventBus eventBus,
        IUiDispatcher dispatcher)
        : base(CoreShellContributor.JournalDocumentId, "Журнал")
    {
        Guard.NotNull(eventBus);
        Guard.NotNull(dispatcher);

        _history = Guard.NotNull(history);
        _dialogs = Guard.NotNull(dialogs);

        Kinds =
        [
            new JournalKindOption(HistoryKind.Any, HistoryActions.Title(HistoryKind.Any)),
            new JournalKindOption(HistoryKind.Roll, HistoryActions.Title(HistoryKind.Roll)),
            new JournalKindOption(HistoryKind.Resource, HistoryActions.Title(HistoryKind.Resource)),
            new JournalKindOption(HistoryKind.Spell, HistoryActions.Title(HistoryKind.Spell)),
            new JournalKindOption(HistoryKind.Equipment, HistoryActions.Title(HistoryKind.Equipment)),
            new JournalKindOption(HistoryKind.Item, HistoryActions.Title(HistoryKind.Item)),
            new JournalKindOption(HistoryKind.Character, HistoryActions.Title(HistoryKind.Character)),
        ];

        _selectedKind = Kinds[0];

        // Журнал пополняется другими разделами приложения: бросок из панели
        // кубиков и надетый на листе предмет должны появляться здесь сразу.
        _subscription = eventBus.SubscribeOnUiThread<CharacterChangedEvent>(dispatcher, _ => Reload());
    }

    /// <summary>Записи журнала от новых к старым.</summary>
    public ObservableCollection<JournalRowViewModel> Records { get; } = [];

    /// <summary>Виды событий, доступные для отбора.</summary>
    public IReadOnlyList<JournalKindOption> Kinds { get; }

    /// <summary>Персонажи, доступные для отбора.</summary>
    public ObservableCollection<CharacterFilterOption> Characters { get; } = [];

    /// <summary>Журнал пуст при выбранном отборе.</summary>
    public bool IsEmpty => Records.Count == 0;

    /// <summary>Показаны не все записи.</summary>
    public bool HasMore => Records.Count < Total;

    /// <summary>Сколько записей показано из общего числа.</summary>
    public string Counter => Records.Count == Total
        ? $"Записей: {Format(Total)}"
        : $"Показано {Format(Records.Count)} из {Format(Total)}";

    /// <inheritdoc />
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await ReloadCharactersAsync(cancellationToken).ConfigureAwait(true);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public void Dispose() => _subscription.Dispose();

    /// <summary>
    /// Перечитывает журнал с учётом отбора.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после чтения.</returns>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        try
        {
            var query = HistoryQuery.ForCharacter(
                SelectedCharacter?.Id,
                SelectedKind?.Kind ?? HistoryKind.Any,
                Search,
                Limit);

            var page = await _history.GetAsync(query, cancellationToken).ConfigureAwait(true);

            if (page.IsFailure)
            {
                await _dialogs.ShowErrorAsync("Журнал", page.Error!, null).ConfigureAwait(true);
                return;
            }

            Records.Clear();

            foreach (var record in page.Value.Records)
            {
                Records.Add(new JournalRowViewModel(record));
            }

            Total = page.Value.Total;
        }
        finally
        {
            IsBusy = false;
            NotifyCounters();
        }
    }

    /// <summary>
    /// Перечитывает журнал по требованию пользователя.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после чтения.</returns>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await ReloadCharactersAsync(cancellationToken).ConfigureAwait(true);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Показывает следующую часть журнала.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после чтения.</returns>
    [RelayCommand]
    private async Task ShowMoreAsync(CancellationToken cancellationToken)
    {
        Limit += PageSize;

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Очищает журнал с подтверждением.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после очистки.</returns>
    [RelayCommand]
    private async Task ClearAsync(CancellationToken cancellationToken)
    {
        var scope = SelectedCharacter?.Id is null
            ? "весь журнал"
            : $"журнал персонажа «{SelectedCharacter.Name}»";

        var confirmed = await _dialogs
            .ShowConfirmationAsync(
                "Очистить журнал",
                $"Удалить {scope}? Любимые броски останутся. Действие нельзя отменить.")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var result = await _history
            .ClearAsync(SelectedCharacter?.Id, cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Журнал", result.Error!, null).ConfigureAwait(true);
            return;
        }

        Limit = PageSize;

        await ReloadCharactersAsync(cancellationToken).ConfigureAwait(true);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task ReloadCharactersAsync(CancellationToken cancellationToken)
    {
        var result = await _history.GetCharactersAsync(cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            return;
        }

        var selected = SelectedCharacter?.Id;

        Characters.Clear();
        Characters.Add(CharacterFilterOption.All);

        foreach (var character in result.Value)
        {
            Characters.Add(new CharacterFilterOption(character.Id, character.Name));
        }

        SelectedCharacter = Characters.FirstOrDefault(option => option.Id == selected) ?? Characters[0];
    }

    /// <summary>
    /// Перечитывает журнал, не дожидаясь завершения: вызывается из обработчиков
    /// изменения отбора и из подписки на шину событий.
    /// </summary>
    private void Reload() => _ = ReloadAsync(CancellationToken.None);

    private void NotifyCounters()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasMore));
        OnPropertyChanged(nameof(Counter));
    }

    private static string Format(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    partial void OnSelectedCharacterChanged(CharacterFilterOption? value) => ResetAndReload();

    partial void OnSelectedKindChanged(JournalKindOption? value) => ResetAndReload();

    partial void OnSearchChanged(string value) => ResetAndReload();

    /// <summary>
    /// Возвращает показ к первой странице и перечитывает журнал.
    /// Иначе после сужения отбора остался бы прежний, уже неверный, предел показа.
    /// </summary>
    private void ResetAndReload()
    {
        Limit = PageSize;

        Reload();
    }
}
