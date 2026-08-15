using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Dice;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;
using RPGCharacterManager.UI.Controls;

namespace RPGCharacterManager.UI.ViewModels.Dice;

/// <summary>
/// Панель бросков кубиков.
///
/// Панель живёт рядом с рабочей областью, а не внутри документа: бросок нужен и на
/// листе персонажа, и в редакторе контента, и при разборе правил. Если показан лист
/// персонажа, его значения доступны формуле броска, поэтому «1d20 + Ловкость»
/// работает без выбора персонажа вручную.
/// </summary>
public sealed partial class DicePanelViewModel : ViewModelBase, IDisposable
{
    /// <summary>Количество записей журнала, показываемых в панели.</summary>
    public const int HistorySize = 60;

    /// <summary>Наибольшее количество кубиков в одном броске кнопкой.</summary>
    public const int MaximumCount = 50;

    private readonly IDiceService _dice;
    private readonly ICharacterSheetService _sheets;
    private readonly ISettingsService _settings;
    private readonly IDisposable _settingsSubscription;
    private readonly IDisposable _characterSubscription;

    private Guid? _characterId;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private int _count = 1;

    [ObservableProperty]
    private string _expression = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private RollMode _mode;

    [ObservableProperty]
    private bool _isRolling;

    [ObservableProperty]
    private IReadOnlyList<DieCast>? _casts;

    [ObservableProperty]
    private Color? _dieColor;

    [ObservableProperty]
    private bool _isAnimated = true;

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private string? _characterName;

    [ObservableProperty]
    private RollRowViewModel? _lastRoll;

    [ObservableProperty]
    private bool _showFavorites;

    [ObservableProperty]
    private bool _isLoadingCharacterRolls;

    [ObservableProperty]
    private CharacterRollOptionViewModel? _selectedSavingThrow;

    [ObservableProperty]
    private CharacterRollOptionViewModel? _selectedSkillCheck;

    /// <summary>
    /// Нажатие на кубик добавляет его к выражению, а не бросает сразу.
    ///
    /// Смешанный бросок вроде «2d10 + 4d4 + 15d8» иначе пришлось бы набирать
    /// вручную: кнопка кубика заменяла выражение целиком.
    /// </summary>
    [ObservableProperty]
    private bool _isBuilding;

    /// <summary>
    /// Создаёт модель представления панели бросков.
    /// </summary>
    /// <param name="dice">Служба бросков.</param>
    /// <param name="sheets">Служба листов персонажей: источник рассчитанных бонусов проверок.</param>
    /// <param name="formulas">Движок формул для встроенного калькулятора.</param>
    /// <param name="settings">Служба настроек.</param>
    /// <param name="eventBus">Шина событий приложения.</param>
    /// <param name="dispatcher">Диспетчер потока пользовательского интерфейса.</param>
    public DicePanelViewModel(
        IDiceService dice,
        ICharacterSheetService sheets,
        IFormulaEngine formulas,
        ISettingsService settings,
        IEventBus eventBus,
        IUiDispatcher dispatcher)
    {
        Guard.NotNull(eventBus);
        Guard.NotNull(dispatcher);

        _dice = Guard.NotNull(dice);
        _sheets = Guard.NotNull(sheets);
        _settings = Guard.NotNull(settings);
        Calculator = new CalculatorViewModel(Guard.NotNull(formulas));

        IsAnimated = _settings.Current.DiceAnimationEnabled;

        // Полёт кубика включается и выключается в настройках, поэтому панель
        // следит за ними и не требует перезапуска приложения.
        _settingsSubscription = eventBus.SubscribeOnUiThread<SettingsChangedEvent>(dispatcher, OnSettingsChanged);
        _characterSubscription = eventBus.SubscribeOnUiThread<CharacterChangedEvent>(dispatcher, OnCharacterChanged);
    }

    /// <summary>Кубики, доступные для броска.</summary>
    public ObservableCollection<DieButtonViewModel> Dice { get; } = [];

    /// <summary>Последние броски от новых к старым.</summary>
    public ObservableCollection<RollRowViewModel> History { get; } = [];

    /// <summary>Любимые броски.</summary>
    public ObservableCollection<RollRowViewModel> Favorites { get; } = [];

    /// <summary>Спасброски активного персонажа с уже рассчитанными бонусами.</summary>
    public ObservableCollection<CharacterRollOptionViewModel> SavingThrows { get; } = [];

    /// <summary>Проверки навыков активного персонажа с уже рассчитанными бонусами.</summary>
    public ObservableCollection<CharacterRollOptionViewModel> SkillChecks { get; } = [];

    /// <summary>Обычный арифметический калькулятор под кубиками.</summary>
    public CalculatorViewModel Calculator { get; }

    /// <summary>Обычный бросок.</summary>
    public bool IsNormalMode => Mode == RollMode.Normal;

    /// <summary>Бросок с преимуществом.</summary>
    public bool IsAdvantageMode => Mode == RollMode.Advantage;

    /// <summary>Бросок с помехой.</summary>
    public bool IsDisadvantageMode => Mode == RollMode.Disadvantage;

    /// <summary>Показан лист персонажа, значения которого доступны формуле.</summary>
    public bool HasCharacter => _characterId is not null;

    /// <summary>У активного персонажа настроены спасброски.</summary>
    public bool HasSavingThrows => SavingThrows.Count > 0;

    /// <summary>У активного персонажа настроены проверки навыков.</summary>
    public bool HasSkillChecks => SkillChecks.Count > 0;

    /// <summary>У активного персонажа пока нет ни навыков, ни спасбросков.</summary>
    public bool HasNoCharacterRolls =>
        HasCharacter && !IsLoadingCharacterRolls && !HasSavingThrows && !HasSkillChecks;

    /// <summary>Сообщение об ошибке показано.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    /// <summary>Итог последнего броска показан.</summary>
    public bool HasResult => LastRoll is not null && !IsRolling;

    /// <summary>В этом сеансе ещё не бросали.</summary>
    public bool HasNotRolled => LastRoll is null && !IsRolling;

    /// <summary>В журнале нет записей.</summary>
    public bool IsHistoryEmpty => History.Count == 0;

    /// <summary>Любимых бросков нет.</summary>
    public bool IsFavoritesEmpty => Favorites.Count == 0;

    /// <summary>Показан список любимых бросков, а не журнал.</summary>
    public bool ShowHistory => !ShowFavorites;

    /// <summary>
    /// Загружает кубики, журнал и любимые броски.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var dice = await _dice.GetDiceAsync(cancellationToken).ConfigureAwait(true);

        if (dice.IsSuccess)
        {
            Dice.Clear();

            foreach (var die in dice.Value)
            {
                Dice.Add(new DieButtonViewModel(die));
            }
        }

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
        await ReloadCharacterRollsAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Привязывает панель к персонажу, лист которого показан.
    ///
    /// Журнал бросков при этом не сужается до персонажа: игрок бросает и «за столом»,
    /// без персонажа, и такие броски должны остаться на виду.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа или <see langword="null"/>.</param>
    /// <param name="characterName">Имя персонажа.</param>
    public void SetCharacter(Guid? characterId, string? characterName)
    {
        var changed = _characterId != characterId;
        _characterId = characterId;
        CharacterName = characterName;

        OnPropertyChanged(nameof(HasCharacter));
        OnPropertyChanged(nameof(HasNoCharacterRolls));

        if (!changed)
        {
            return;
        }

        ClearCharacterRolls();

        if (IsOpen && characterId is not null)
        {
            _ = ReloadCharacterRollsAsync(CancellationToken.None);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _settingsSubscription.Dispose();
        _characterSubscription.Dispose();
    }

    /// <summary>
    /// Показывает или скрывает панель бросков.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки содержимого панели.</returns>
    [RelayCommand]
    private async Task ToggleAsync(CancellationToken cancellationToken)
    {
        IsOpen = !IsOpen;

        if (IsOpen)
        {
            // Пользовательские кубики мог измениться в разделе контента, пока
            // панель была закрыта.
            await InitializeAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Бросает выбранное количество указанных кубиков.
    /// </summary>
    /// <param name="die">Кубик.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после броска.</returns>
    [RelayCommand]
    private async Task RollDieAsync(DieButtonViewModel? die, CancellationToken cancellationToken)
    {
        if (die is null)
        {
            return;
        }

        var count = Math.Clamp(Count, 1, MaximumCount);

        if (IsBuilding)
        {
            Add(die, count);
            return;
        }

        DieColor = die.Color;
        Expression = DiceNotation.Throw(count, die.Die.Sides);

        await RollAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Добавляет кубики к собираемому броску.
    /// </summary>
    /// <param name="die">Кубик.</param>
    /// <param name="count">Количество кубиков.</param>
    private void Add(DieButtonViewModel die, int count)
    {
        // Набор из разных кубиков красится акцентным цветом: выбрать цвет одного
        // из них значило бы соврать про остальные, а раскрасить каждый по-своему
        // поднос не умеет — он готовит оттенки один раз на весь бросок.
        DieColor = Expression.Length == 0 || DieColor == die.Color ? die.Color : null;

        Expression = DiceNotation.Add(Expression, count, die.Die.Sides);
    }

    /// <summary>
    /// Очищает собранное выражение броска.
    /// </summary>
    [RelayCommand]
    private void ClearExpression()
    {
        Expression = string.Empty;
        DieColor = null;
    }

    /// <summary>
    /// Выполняет бросок по введённому выражению.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после броска.</returns>
    [RelayCommand]
    private async Task RollAsync(CancellationToken cancellationToken)
    {
        await PerformAsync(
            new RollRequest(Expression, Mode, Title, _characterId),
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Повторяет бросок из журнала или список любимых.
    /// </summary>
    /// <param name="row">Запись броска.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после броска.</returns>
    [RelayCommand]
    private async Task RepeatAsync(RollRowViewModel? row, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return;
        }

        // Повтор восстанавливает и способ броска, и название: любимый бросок
        // сохраняли целиком, а не одну лишь формулу.
        Expression = row.Outcome.Expression;
        Mode = row.Outcome.Mode;
        Title = row.Outcome.Title ?? string.Empty;
        DieColor = null;

        await PerformAsync(
            new RollRequest(row.Outcome.Expression, row.Outcome.Mode, row.Outcome.Title, _characterId),
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Добавляет бросок в любимые или убирает из них.
    /// </summary>
    /// <param name="row">Запись броска.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после изменения.</returns>
    [RelayCommand]
    private async Task ToggleFavoriteAsync(RollRowViewModel? row, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return;
        }

        var result = await _dice
            .SetFavoriteAsync(row.Id, !row.IsFavorite, null, cancellationToken)
            .ConfigureAwait(true);

        if (!Report(result))
        {
            return;
        }

        // Показанный итог обновляется вместе с записью, иначе кнопка предлагала бы
        // добавить в любимые бросок, который там уже есть.
        if (LastRoll?.Id == row.Id)
        {
            LastRoll = new RollRowViewModel(result.Value);
        }

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Удаляет бросок из журнала.
    /// </summary>
    /// <param name="row">Запись броска.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task DeleteAsync(RollRowViewModel? row, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return;
        }

        var result = await _dice.DeleteAsync(row.Id, cancellationToken).ConfigureAwait(true);

        if (Report(result))
        {
            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Очищает журнал бросков, сохраняя любимые.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после очистки.</returns>
    [RelayCommand]
    private async Task ClearHistoryAsync(CancellationToken cancellationToken)
    {
        var result = await _dice.ClearHistoryAsync(null, cancellationToken).ConfigureAwait(true);

        if (Report(result))
        {
            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Переключает панель на обычный бросок.</summary>
    [RelayCommand]
    private void SetNormal() => Mode = RollMode.Normal;

    /// <summary>Переключает панель на бросок с преимуществом.</summary>
    [RelayCommand]
    private void SetAdvantage() => Mode = RollMode.Advantage;

    /// <summary>Переключает панель на бросок с помехой.</summary>
    [RelayCommand]
    private void SetDisadvantage() => Mode = RollMode.Disadvantage;

    /// <summary>
    /// Выполняет выбранную проверку навыка или спасбросок с итоговым бонусом персонажа.
    /// </summary>
    /// <param name="option">Проверка с рассчитанным бонусом.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после броска.</returns>
    [RelayCommand]
    private async Task RollCharacterAsync(
        CharacterRollOptionViewModel? option,
        CancellationToken cancellationToken)
    {
        if (option is null || _characterId is null)
        {
            return;
        }

        Expression = option.Expression;
        Title = option.Title;
        DieColor = null;

        await PerformAsync(
            new RollRequest(option.Expression, Mode, option.Title, _characterId),
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Показывает журнал бросков.</summary>
    [RelayCommand]
    private void ShowHistoryList() => ShowFavorites = false;

    /// <summary>Показывает любимые броски.</summary>
    [RelayCommand]
    private void ShowFavoritesList() => ShowFavorites = true;

    /// <summary>
    /// Выполняет бросок и показывает его результат после полёта кубиков.
    /// </summary>
    /// <param name="request">Запрос броска.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после показа результата.</returns>
    private async Task PerformAsync(RollRequest request, CancellationToken cancellationToken)
    {
        if (IsRolling)
        {
            return;
        }

        Error = null;
        OnPropertyChanged(nameof(HasError));

        var result = await _dice.RollAsync(request, cancellationToken).ConfigureAwait(true);

        if (!Report(result))
        {
            return;
        }

        var outcome = result.Value;

        IsRolling = true;
        NotifyResultChanged();

        // Кубики отправляются в полёт, а итог объявляется, когда они улягутся:
        // иначе число появлялось бы раньше, чем кубик его показал.
        Casts = outcome.Dice;

        if (IsAnimated && outcome.Dice.Count > 0)
        {
            await Task.Delay(DiceTray.DurationOf(outcome.Dice.Count), cancellationToken).ConfigureAwait(true);
        }

        LastRoll = new RollRowViewModel(outcome);
        IsRolling = false;

        NotifyResultChanged();

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Сообщает представлению, что состояние показа итога изменилось.
    /// </summary>
    private void NotifyResultChanged()
    {
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(HasNotRolled));
    }

    /// <summary>
    /// Перечитывает журнал и любимые броски.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после чтения.</returns>
    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        var history = await _dice.GetHistoryAsync(null, HistorySize, cancellationToken).ConfigureAwait(true);

        if (history.IsSuccess)
        {
            Fill(History, history.Value);
            OnPropertyChanged(nameof(IsHistoryEmpty));
        }

        var favorites = await _dice.GetFavoritesAsync(null, cancellationToken).ConfigureAwait(true);

        if (favorites.IsSuccess)
        {
            Fill(Favorites, favorites.Value);
            OnPropertyChanged(nameof(IsFavoritesEmpty));
        }
    }

    /// <summary>Перечитывает рассчитанные проверки активного персонажа.</summary>
    private async Task ReloadCharacterRollsAsync(CancellationToken cancellationToken)
    {
        if (_characterId is not { } characterId)
        {
            ClearCharacterRolls();
            return;
        }

        IsLoadingCharacterRolls = true;
        OnPropertyChanged(nameof(HasNoCharacterRolls));

        var result = await _sheets.LoadAsync(characterId, cancellationToken).ConfigureAwait(true);

        // Пока лист загружался, пользователь мог открыть другого персонажа.
        if (_characterId != characterId)
        {
            return;
        }

        IsLoadingCharacterRolls = false;

        if (!result.IsSuccess)
        {
            ClearCharacterRolls();
            Error = result.Error;
            OnPropertyChanged(nameof(HasError));
            return;
        }

        SavingThrows.Clear();
        SkillChecks.Clear();

        foreach (var skill in result.Value.Skills.OrderBy(skill => skill.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var isSavingThrow = string.Equals(
                skill.Category,
                SheetCategories.SavingThrows,
                StringComparison.OrdinalIgnoreCase);
            var option = new CharacterRollOptionViewModel(skill, isSavingThrow);

            if (isSavingThrow)
            {
                SavingThrows.Add(option);
            }
            else
            {
                SkillChecks.Add(option);
            }
        }

        SelectedSavingThrow = SavingThrows.FirstOrDefault();
        SelectedSkillCheck = SkillChecks.FirstOrDefault();
        NotifyCharacterRollsChanged();
    }

    /// <summary>Очищает проверки, когда лист персонажа закрыт или заменён.</summary>
    private void ClearCharacterRolls()
    {
        IsLoadingCharacterRolls = false;
        SavingThrows.Clear();
        SkillChecks.Clear();
        SelectedSavingThrow = null;
        SelectedSkillCheck = null;
        NotifyCharacterRollsChanged();
    }

    private void NotifyCharacterRollsChanged()
    {
        OnPropertyChanged(nameof(HasSavingThrows));
        OnPropertyChanged(nameof(HasSkillChecks));
        OnPropertyChanged(nameof(HasNoCharacterRolls));
    }

    private static void Fill(ObservableCollection<RollRowViewModel> target, IReadOnlyList<RollOutcome> source)
    {
        target.Clear();

        foreach (var outcome in source)
        {
            target.Add(new RollRowViewModel(outcome));
        }
    }

    /// <summary>
    /// Показывает ошибку прямо в панели.
    ///
    /// Опечатка в формуле — обычное дело, и останавливать работу окном с ошибкой
    /// ради неё незачем: сообщение появляется под полем ввода.
    /// </summary>
    /// <param name="result">Результат операции.</param>
    /// <returns><see langword="true"/>, если операция удалась.</returns>
    private bool Report(Result result)
    {
        Guard.NotNull(result);

        Error = result.IsSuccess ? null : result.Error;
        OnPropertyChanged(nameof(HasError));

        return result.IsSuccess;
    }

    private void OnSettingsChanged(SettingsChangedEvent notification) =>
        IsAnimated = notification.Settings.DiceAnimationEnabled;

    private void OnCharacterChanged(CharacterChangedEvent notification)
    {
        if (notification.CharacterId == _characterId && IsOpen)
        {
            _ = ReloadCharacterRollsAsync(CancellationToken.None);
        }
    }

    partial void OnModeChanged(RollMode value)
    {
        OnPropertyChanged(nameof(IsNormalMode));
        OnPropertyChanged(nameof(IsAdvantageMode));
        OnPropertyChanged(nameof(IsDisadvantageMode));
    }

    partial void OnShowFavoritesChanged(bool value) => OnPropertyChanged(nameof(ShowHistory));

    partial void OnCountChanged(int value)
    {
        var limited = Math.Clamp(value, 1, MaximumCount);

        if (limited != value)
        {
            Count = limited;
        }
    }
}
