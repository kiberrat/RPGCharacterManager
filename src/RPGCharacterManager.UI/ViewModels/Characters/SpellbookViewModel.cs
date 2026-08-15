using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Книга заклинаний персонажа на его листе.
///
/// Раздел вынесен в отдельную модель представления: у него собственные поиск,
/// пределы и набор действий, не связанные с остальными разделами листа.
/// Игровых правил здесь нет — всё считает служба книги заклинаний.
/// </summary>
public sealed partial class SpellbookViewModel : ViewModelBase
{
    private readonly ISpellbookService _spellbook;
    private readonly IDialogService _dialogs;

    private Guid _characterId;

    /// <summary>
    /// Заклинание, для которого выбран уровень применения.
    /// Перечитывание книги создаёт строки заново, поэтому равенство ссылок
    /// не годится: выбранный пользователем уровень нужно сохранить.
    /// </summary>
    private Guid _castLevelOwner;

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private SpellbookEntryViewModel? _selectedSpell;

    [ObservableProperty]
    private bool _isPickerOpen;

    [ObservableProperty]
    private string _pickerSearch = string.Empty;

    [ObservableProperty]
    private CharacterOptionViewModel? _selectedAvailableSpell;

    [ObservableProperty]
    private int _castLevel;

    [ObservableProperty]
    private int _minimumCastLevel;

    [ObservableProperty]
    private string _knownText = string.Empty;

    [ObservableProperty]
    private string _preparedText = string.Empty;

    [ObservableProperty]
    private bool _usesPreparation;

    [ObservableProperty]
    private string? _concentratingOn;

    [ObservableProperty]
    private string? _lastReport;

    /// <summary>
    /// Создаёт модель представления книги заклинаний.
    /// </summary>
    /// <param name="spellbook">Служба книги заклинаний.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    public SpellbookViewModel(ISpellbookService spellbook, IDialogService dialogs)
    {
        _spellbook = Guard.NotNull(spellbook);
        _dialogs = Guard.NotNull(dialogs);
    }

    /// <summary>Уровни книги заклинаний в порядке возрастания.</summary>
    public ObservableCollection<SpellbookLevelViewModel> Levels { get; } = [];

    /// <summary>Последние применения заклинаний, новые сверху.</summary>
    public ObservableCollection<SpellCastRecordViewModel> History { get; } = [];

    /// <summary>Заклинания, доступные для изучения.</summary>
    public ObservableCollection<CharacterOptionViewModel> AvailableSpells { get; } = [];

    /// <summary>Книга заклинаний пуста.</summary>
    public bool IsEmpty => Levels.Count == 0;

    /// <summary>Поиск скрыл все заклинания.</summary>
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search);

    /// <summary>Персонаж концентрируется на заклинании.</summary>
    public bool IsConcentrating => !string.IsNullOrWhiteSpace(ConcentratingOn);

    /// <summary>Заклинание выбрано.</summary>
    public bool HasSelection => SelectedSpell is not null;

    /// <summary>Отчёт о последнем действии показан.</summary>
    public bool HasReport => !string.IsNullOrWhiteSpace(LastReport);

    /// <summary>История применения не пуста.</summary>
    public bool HasHistory => History.Count > 0;

    /// <summary>
    /// Привязывает книгу заклинаний к персонажу и загружает её.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    public async Task InitializeAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        _characterId = characterId;

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Перечитывает книгу заклинаний персонажа.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (_characterId == Guid.Empty)
        {
            return;
        }

        var result = await _spellbook.GetAsync(_characterId, Search, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await ReportAsync(result).ConfigureAwait(true);
            return;
        }

        Fill(result.Value);
    }

    /// <summary>
    /// Показывает или скрывает список заклинаний, доступных для изучения.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки списка.</returns>
    [RelayCommand]
    private async Task TogglePickerAsync(CancellationToken cancellationToken)
    {
        IsPickerOpen = !IsPickerOpen;

        if (IsPickerOpen)
        {
            await ReloadAvailableSpellsAsync(cancellationToken).ConfigureAwait(true);
        }
        else
        {
            AvailableSpells.Clear();
            SelectedAvailableSpell = null;
        }
    }

    /// <summary>
    /// Изучает выбранное заклинание.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после изучения.</returns>
    [RelayCommand]
    private async Task LearnAsync(CancellationToken cancellationToken)
    {
        if (SelectedAvailableSpell is not { IsAvailable: true } option)
        {
            return;
        }

        var result = await _spellbook.LearnAsync(_characterId, option.Id, cancellationToken)
            .ConfigureAwait(true);

        if (!await ReportAsync(result).ConfigureAwait(true))
        {
            return;
        }

        LastReport = $"Выучено: {option.Name}.";
        OnPropertyChanged(nameof(HasReport));

        await ReloadAvailableSpellsAsync(cancellationToken).ConfigureAwait(true);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Забывает выбранное заклинание.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        if (SelectedSpell is not { } spell)
        {
            return;
        }

        var confirmed = await _dialogs
            .ShowConfirmationAsync(
                "Забыть заклинание",
                $"Убрать «{spell.Name}» из книги заклинаний персонажа?")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var result = await _spellbook
            .ForgetAsync(_characterId, spell.CharacterSpellId, cancellationToken)
            .ConfigureAwait(true);

        if (await ReportAsync(result).ConfigureAwait(true))
        {
            LastReport = $"Забыто: {spell.Name}.";
            OnPropertyChanged(nameof(HasReport));

            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Подготавливает выбранное заклинание или снимает подготовку.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после изменения.</returns>
    [RelayCommand]
    private async Task TogglePreparedAsync(CancellationToken cancellationToken)
    {
        if (SelectedSpell is not { } spell)
        {
            return;
        }

        var result = await _spellbook
            .SetPreparedAsync(_characterId, spell.CharacterSpellId, !spell.IsPrepared, cancellationToken)
            .ConfigureAwait(true);

        if (await ReportAsync(result).ConfigureAwait(true))
        {
            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Применяет выбранное заклинание на выбранном уровне.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после применения.</returns>
    [RelayCommand]
    private async Task CastAsync(CancellationToken cancellationToken)
    {
        if (SelectedSpell is not { } spell)
        {
            return;
        }

        var result = await _spellbook
            .CastAsync(_characterId, spell.CharacterSpellId, CastLevel, cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs
                .ShowErrorAsync("Заклинание не применено", result.Error!, null)
                .ConfigureAwait(true);

            return;
        }

        LastReport = Describe(result.Value);
        OnPropertyChanged(nameof(HasReport));

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Прерывает концентрацию персонажа.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после прерывания.</returns>
    [RelayCommand]
    private async Task StopConcentrationAsync(CancellationToken cancellationToken)
    {
        var result = await _spellbook.StopConcentrationAsync(_characterId, cancellationToken)
            .ConfigureAwait(true);

        if (await ReportAsync(result).ConfigureAwait(true))
        {
            LastReport = "Концентрация прервана.";
            OnPropertyChanged(nameof(HasReport));

            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    partial void OnSearchChanged(string value) => _ = ReloadAsync(CancellationToken.None);

    partial void OnPickerSearchChanged(string value) =>
        _ = ReloadAvailableSpellsAsync(CancellationToken.None);

    partial void OnSelectedSpellChanged(SpellbookEntryViewModel? value)
    {
        MinimumCastLevel = value?.Level ?? 0;

        // Уровень применения возвращается к базовому только при выборе другого
        // заклинания. Применение перечитывает книгу, и выбранный пользователем
        // повышенный уровень не должен из-за этого сбрасываться.
        var owner = value?.CharacterSpellId ?? Guid.Empty;

        if (owner != _castLevelOwner)
        {
            _castLevelOwner = owner;
            CastLevel = MinimumCastLevel;

            OnPropertyChanged(nameof(HasSelection));
            return;
        }

        // Уровень применения не может быть ниже уровня заклинания.
        if (CastLevel < MinimumCastLevel)
        {
            CastLevel = MinimumCastLevel;
        }

        OnPropertyChanged(nameof(HasSelection));
    }

    /// <summary>
    /// Переносит книгу заклинаний в списки представления.
    /// </summary>
    /// <param name="state">Книга заклинаний.</param>
    private void Fill(SpellbookState state)
    {
        // Выбор сохраняется по идентификатору: перечитывание книги не должно
        // сбрасывать выделенное заклинание, иначе действия над ним прерывались бы.
        var selectedId = SelectedSpell?.CharacterSpellId;

        Levels.Clear();

        foreach (var level in state.Levels)
        {
            Levels.Add(new SpellbookLevelViewModel(level));
        }

        History.Clear();

        foreach (var record in state.History)
        {
            History.Add(new SpellCastRecordViewModel(record));
        }

        SelectedSpell = Levels
            .SelectMany(level => level.Spells)
            .FirstOrDefault(spell => spell.CharacterSpellId == selectedId);

        KnownText = FormatLimit("Известно", state.Known);
        PreparedText = FormatLimit("Подготовлено", state.Prepared);
        UsesPreparation = state.UsesPreparation;
        ConcentratingOn = state.ConcentratingOn;

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsFiltered));
        OnPropertyChanged(nameof(IsConcentrating));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasHistory));
    }

    private async Task ReloadAvailableSpellsAsync(CancellationToken cancellationToken)
    {
        if (!IsPickerOpen || _characterId == Guid.Empty)
        {
            return;
        }

        var page = await _spellbook
            .GetAvailableSpellsAsync(_characterId, PickerSearch, cancellationToken)
            .ConfigureAwait(true);

        AvailableSpells.Clear();

        foreach (var option in page.Options)
        {
            AvailableSpells.Add(new CharacterOptionViewModel(option));
        }

        SelectedAvailableSpell = AvailableSpells.FirstOrDefault();
    }

    /// <summary>
    /// Показывает ошибку операции, если она произошла.
    /// </summary>
    /// <param name="result">Результат операции.</param>
    /// <returns><see langword="true"/>, если операция удалась.</returns>
    private async Task<bool> ReportAsync(Result result)
    {
        Guard.NotNull(result);

        if (result.IsSuccess)
        {
            return true;
        }

        await _dialogs.ShowErrorAsync("Книга заклинаний", result.Error!, null).ConfigureAwait(true);

        return false;
    }

    /// <summary>
    /// Описывает итог применения заклинания одной строкой.
    /// </summary>
    /// <param name="result">Итог применения.</param>
    /// <returns>Текст отчёта.</returns>
    private static string Describe(SpellCastResult result)
    {
        var level = result.CastLevel == 0
            ? "кантрип"
            : $"уровень {result.CastLevel.ToString(CultureInfo.CurrentCulture)}";

        var parts = new List<string> { $"Применено: {result.SpellName} ({level})." };

        if (result.Result is { } value)
        {
            parts.Add($"Результат: {SheetNumber.Format(value)}.");
        }

        if (result.ResourceName is not null && result.ResourceSpent > 0)
        {
            parts.Add(
                $"{result.ResourceName}: −{SheetNumber.Format(result.ResourceSpent)}, "
                + $"осталось {SheetNumber.Format(result.ResourceRemaining ?? 0)}.");
        }

        if (result.BrokeConcentration is { } broken)
        {
            parts.Add($"Концентрация на «{broken}» прервана.");
        }

        if (result.IsConcentrating)
        {
            parts.Add("Персонаж концентрируется на этом заклинании.");
        }

        parts.AddRange(result.Issues);

        return string.Join(" ", parts);
    }

    private static string FormatLimit(string title, SpellbookLimit limit) =>
        limit.Limit is { } allowed
            ? $"{title}: {limit.Count.ToString(CultureInfo.CurrentCulture)} из {allowed.ToString(CultureInfo.CurrentCulture)}"
            : $"{title}: {limit.Count.ToString(CultureInfo.CurrentCulture)}";
}
