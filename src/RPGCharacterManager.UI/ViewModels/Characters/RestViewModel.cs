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
/// Что отдых сделает с одним ресурсом.
/// </summary>
public sealed class RestRestoreRowViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт строку восстановления.
    /// </summary>
    /// <param name="preview">Описание восстановления.</param>
    public RestRestoreRowViewModel(RestRestorePreview preview) => Preview = Guard.NotNull(preview);

    /// <summary>Описание восстановления.</summary>
    public RestRestorePreview Preview { get; }

    /// <summary>Что и насколько восстановится.</summary>
    public string Text => $"{Preview.ResourceName}: {Preview.Description}";

    /// <summary>Условие восстановления.</summary>
    public string Hint => string.IsNullOrWhiteSpace(Preview.Condition)
        ? string.Empty
        : $"при условии: {Preview.Condition}";

    /// <summary>Условие задано.</summary>
    public bool HasHint => Hint.Length > 0;
}

/// <summary>
/// Вид отдыха в списке.
/// </summary>
public sealed class RestOptionViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт строку вида отдыха.
    /// </summary>
    /// <param name="option">Вид отдыха.</param>
    public RestOptionViewModel(RestOption option)
    {
        Option = Guard.NotNull(option);
        Restores = new ObservableCollection<RestRestoreRowViewModel>(
            option.Restores.Select(restore => new RestRestoreRowViewModel(restore)));
    }

    /// <summary>Вид отдыха.</summary>
    public RestOption Option { get; }

    /// <summary>Идентификатор вида отдыха.</summary>
    public Guid Id => Option.Id;

    /// <summary>Название отдыха.</summary>
    public string Name => Option.Name;

    /// <summary>Описание отдыха.</summary>
    public string Description => Option.Description ?? string.Empty;

    /// <summary>Описание отдыха задано.</summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>Длительность отдыха.</summary>
    public string Duration => Option.Duration ?? "без затрат времени";

    /// <summary>Отдых доступен.</summary>
    public bool IsAvailable => Option.IsAvailable;

    /// <summary>Причина, по которой отдохнуть нельзя.</summary>
    public string UnavailableReason => Option.UnavailableReason ?? string.Empty;

    /// <summary>Причина недоступности показана.</summary>
    public bool HasUnavailableReason => !IsAvailable && UnavailableReason.Length > 0;

    /// <summary>Отдых что-то восстанавливает.</summary>
    public bool HasRestores => Restores.Count > 0;

    /// <summary>Что отдых восстановит.</summary>
    public ObservableCollection<RestRestoreRowViewModel> Restores { get; }
}

/// <summary>
/// Отдых персонажа на его листе.
///
/// Виды отдыха приходят из игрового контента, поэтому раздел одинаково показывает
/// и привычные короткий с длительным, и любой отдых, придуманный пользователем.
/// </summary>
public sealed partial class RestViewModel : ViewModelBase
{
    private readonly IRestService _rests;
    private readonly IDialogService _dialogs;

    private Guid _characterId;

    [ObservableProperty]
    private RestOptionViewModel? _selectedRest;

    [ObservableProperty]
    private string? _lastReport;

    /// <summary>
    /// Создаёт модель представления отдыха.
    /// </summary>
    /// <param name="rests">Служба отдыха.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    public RestViewModel(IRestService rests, IDialogService dialogs)
    {
        _rests = Guard.NotNull(rests);
        _dialogs = Guard.NotNull(dialogs);
    }

    /// <summary>Виды отдыха, доступные персонажу.</summary>
    public ObservableCollection<RestOptionViewModel> Options { get; } = [];

    /// <summary>Видов отдыха нет.</summary>
    public bool IsEmpty => Options.Count == 0;

    /// <summary>Вид отдыха выбран.</summary>
    public bool HasSelection => SelectedRest is not null;

    /// <summary>Отдых можно выполнить.</summary>
    public bool CanRest => SelectedRest?.IsAvailable == true;

    /// <summary>Отчёт о последнем отдыхе показан.</summary>
    public bool HasReport => !string.IsNullOrWhiteSpace(LastReport);

    /// <summary>
    /// Привязывает раздел к персонажу и загружает виды отдыха.
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
    /// Перечитывает виды отдыха персонажа.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (_characterId == Guid.Empty)
        {
            return;
        }

        var result = await _rests.GetAsync(_characterId, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await ReportAsync(result).ConfigureAwait(true);
            return;
        }

        var selected = SelectedRest?.Id;

        Options.Clear();

        foreach (var option in result.Value.Options)
        {
            Options.Add(new RestOptionViewModel(option));
        }

        SelectedRest = Options.FirstOrDefault(option => option.Id == selected) ?? Options.FirstOrDefault();

        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// Выполняет выбранный отдых.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после отдыха.</returns>
    [RelayCommand]
    private async Task RestAsync(CancellationToken cancellationToken)
    {
        if (SelectedRest is not { } rest)
        {
            return;
        }

        var result = await _rests.RestAsync(_characterId, rest.Id, cancellationToken).ConfigureAwait(true);

        if (!await ReportAsync(result).ConfigureAwait(true))
        {
            return;
        }

        LastReport = Describe(result.Value);
        OnPropertyChanged(nameof(HasReport));

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Описывает итог отдыха одной строкой.
    /// </summary>
    /// <param name="result">Итог отдыха.</param>
    /// <returns>Текст отчёта.</returns>
    private static string Describe(RestResult result)
    {
        var parts = new List<string> { $"Отдых «{result.RestName}» завершён." };

        if (result.Changes.Count > 0)
        {
            parts.Add("Восстановлено: " + string.Join(
                ", ",
                result.Changes.Select(change =>
                    $"{change.ResourceName} {Format(change.Before)} → {Format(change.After)}")));
        }
        else
        {
            parts.Add("Восстанавливать было нечего.");
        }

        if (result.Expired.Count > 0)
        {
            parts.Add("Закончилось: " + string.Join(", ", result.Expired) + ".");
        }

        if (result.AppliedRules.Count > 0)
        {
            parts.Add("Применены правила: " + string.Join(", ", result.AppliedRules) + ".");
        }

        if (result.Issues.Count > 0)
        {
            parts.Add("Замечания: " + string.Join("; ", result.Issues));
        }

        return string.Join(" ", parts);
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

        await _dialogs.ShowErrorAsync("Отдых", result.Error!, null).ConfigureAwait(true);

        return false;
    }

    private static string Format(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    partial void OnSelectedRestChanged(RestOptionViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanRest));
    }
}
