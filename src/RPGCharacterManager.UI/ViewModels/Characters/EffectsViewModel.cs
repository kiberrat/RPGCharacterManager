using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Эффекты персонажа на его листе.
///
/// Раздел вынесен в отдельную модель представления: у него собственный набор
/// действий — наложение, снятие и продвижение времени, — не связанный с остальным
/// листом. Игровых правил здесь нет: всё считает служба эффектов.
/// </summary>
public sealed partial class EffectsViewModel : ViewModelBase
{
    private readonly IEffectService _effects;
    private readonly IDialogService _dialogs;

    private Guid _characterId;

    [ObservableProperty]
    private ActiveEffectViewModel? _selectedEffect;

    [ObservableProperty]
    private bool _isPickerOpen;

    [ObservableProperty]
    private string _pickerSearch = string.Empty;

    [ObservableProperty]
    private CharacterOptionViewModel? _selectedAvailableEffect;

    [ObservableProperty]
    private string _source = string.Empty;

    [ObservableProperty]
    private string? _lastReport;

    /// <summary>
    /// Создаёт модель представления эффектов.
    /// </summary>
    /// <param name="effects">Служба эффектов персонажа.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    public EffectsViewModel(IEffectService effects, IDialogService dialogs)
    {
        _effects = Guard.NotNull(effects);
        _dialogs = Guard.NotNull(dialogs);
    }

    /// <summary>Действующие эффекты от большего приоритета к меньшему.</summary>
    public ObservableCollection<ActiveEffectViewModel> Effects { get; } = [];

    /// <summary>Единицы длительности, по которым можно продвинуть время.</summary>
    public ObservableCollection<EffectTimerUnitViewModel> Units { get; } = [];

    /// <summary>Эффекты, доступные для наложения.</summary>
    public ObservableCollection<CharacterOptionViewModel> AvailableEffects { get; } = [];

    /// <summary>На персонажа ничего не наложено.</summary>
    public bool IsEmpty => Effects.Count == 0;

    /// <summary>Есть эффекты с таймером.</summary>
    public bool HasTimers => Units.Count > 0;

    /// <summary>Эффект выбран.</summary>
    public bool HasSelection => SelectedEffect is not null;

    /// <summary>Отчёт о последнем действии показан.</summary>
    public bool HasReport => !string.IsNullOrWhiteSpace(LastReport);

    /// <summary>
    /// Привязывает эффекты к персонажу и загружает их.
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
    /// Перечитывает эффекты персонажа.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (_characterId == Guid.Empty)
        {
            return;
        }

        var result = await _effects.GetAsync(_characterId, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await ReportAsync(result).ConfigureAwait(true);
            return;
        }

        Fill(result.Value);
    }

    /// <summary>
    /// Показывает или скрывает список эффектов, доступных для наложения.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки списка.</returns>
    [RelayCommand]
    private async Task TogglePickerAsync(CancellationToken cancellationToken)
    {
        IsPickerOpen = !IsPickerOpen;

        if (IsPickerOpen)
        {
            await ReloadAvailableEffectsAsync(cancellationToken).ConfigureAwait(true);
        }
        else
        {
            AvailableEffects.Clear();
            SelectedAvailableEffect = null;
        }
    }

    /// <summary>
    /// Накладывает выбранный эффект на персонажа.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после наложения.</returns>
    [RelayCommand]
    private async Task ApplyAsync(CancellationToken cancellationToken)
    {
        if (SelectedAvailableEffect is not { } option)
        {
            return;
        }

        var result = await _effects
            .ApplyAsync(_characterId, option.Id, Source, cancellationToken)
            .ConfigureAwait(true);

        if (!await ReportAsync(result).ConfigureAwait(true))
        {
            return;
        }

        LastReport = $"Наложено: {option.Name}.";
        OnPropertyChanged(nameof(HasReport));

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Снимает выбранный эффект целиком.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после снятия.</returns>
    [RelayCommand]
    private async Task RemoveAsync(CancellationToken cancellationToken)
    {
        if (SelectedEffect is not { } effect)
        {
            return;
        }

        var result = await _effects
            .RemoveAsync(_characterId, effect.CharacterEffectId, cancellationToken)
            .ConfigureAwait(true);

        if (await ReportAsync(result).ConfigureAwait(true))
        {
            LastReport = $"Снято: {effect.Name}.";
            OnPropertyChanged(nameof(HasReport));

            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Убирает одно наложение выбранного эффекта.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после изменения.</returns>
    [RelayCommand]
    private async Task RemoveStackAsync(CancellationToken cancellationToken)
    {
        if (SelectedEffect is not { } effect)
        {
            return;
        }

        var result = await _effects
            .RemoveStackAsync(_characterId, effect.CharacterEffectId, cancellationToken)
            .ConfigureAwait(true);

        if (await ReportAsync(result).ConfigureAwait(true))
        {
            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Продвигает время на одну единицу выбранного вида.
    /// </summary>
    /// <param name="unit">Единица длительности.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после продвижения.</returns>
    [RelayCommand]
    private async Task AdvanceAsync(EffectTimerUnitViewModel? unit, CancellationToken cancellationToken)
    {
        if (unit is null)
        {
            return;
        }

        var result = await _effects
            .AdvanceAsync(_characterId, unit.Name, 1, cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Эффекты", result.Error!, null).ConfigureAwait(true);
            return;
        }

        LastReport = Describe(result.Value);
        OnPropertyChanged(nameof(HasReport));

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    partial void OnPickerSearchChanged(string value) =>
        _ = ReloadAvailableEffectsAsync(CancellationToken.None);

    partial void OnSelectedEffectChanged(ActiveEffectViewModel? value) =>
        OnPropertyChanged(nameof(HasSelection));

    /// <summary>
    /// Переносит состояние эффектов в списки представления.
    /// </summary>
    /// <param name="state">Состояние эффектов.</param>
    private void Fill(EffectState state)
    {
        // Выбор сохраняется по идентификатору: перечитывание не должно сбрасывать
        // выделенный эффект, иначе действия над ним прерывались бы.
        var selectedId = SelectedEffect?.CharacterEffectId;

        Effects.Clear();

        foreach (var effect in state.Effects)
        {
            Effects.Add(new ActiveEffectViewModel(effect));
        }

        Units.Clear();

        foreach (var unit in state.Units)
        {
            Units.Add(new EffectTimerUnitViewModel(unit));
        }

        SelectedEffect = Effects.FirstOrDefault(effect => effect.CharacterEffectId == selectedId);

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasTimers));
        OnPropertyChanged(nameof(HasSelection));
    }

    private async Task ReloadAvailableEffectsAsync(CancellationToken cancellationToken)
    {
        if (!IsPickerOpen || _characterId == Guid.Empty)
        {
            return;
        }

        var page = await _effects
            .GetAvailableEffectsAsync(_characterId, PickerSearch, cancellationToken)
            .ConfigureAwait(true);

        AvailableEffects.Clear();

        foreach (var option in page.Options)
        {
            AvailableEffects.Add(new CharacterOptionViewModel(option));
        }

        SelectedAvailableEffect = AvailableEffects.FirstOrDefault();
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

        await _dialogs.ShowErrorAsync("Эффекты", result.Error!, null).ConfigureAwait(true);

        return false;
    }

    /// <summary>
    /// Описывает итог продвижения времени одной строкой.
    /// </summary>
    /// <param name="result">Итог продвижения.</param>
    /// <returns>Текст отчёта.</returns>
    private static string Describe(EffectAdvanceResult result)
    {
        var passed = $"Прошло: {SheetNumber.Format(result.Amount)} {result.Unit}.";

        return result.Expired.Count == 0
            ? passed
            : $"{passed} Закончилось: {string.Join(", ", result.Expired)}.";
    }
}
