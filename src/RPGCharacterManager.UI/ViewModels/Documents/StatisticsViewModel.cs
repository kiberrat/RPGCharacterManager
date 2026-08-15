using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.History;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Abstractions.Statistics;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Документ «Статистика»: что бросали, чем били, что применяли и на что тратили.
///
/// Раздел ничего не накапливает: он показывает сводку по журналу за выбранный
/// период (решение Р-99). Поэтому очистка журнала обнуляет и статистику — числа
/// не могут пережить события, о которых они говорят.
/// </summary>
public sealed partial class StatisticsViewModel : DocumentViewModelBase
{
    private readonly IStatisticsService _statistics;
    private readonly IHistoryService _history;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private CharacterFilterOption? _selectedCharacter;

    [ObservableProperty]
    private StatisticsPeriod _selectedPeriod = StatisticsPeriod.All[0];

    [ObservableProperty]
    private StatisticsReport _report = StatisticsReport.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Создаёт модель представления статистики.
    /// </summary>
    /// <param name="statistics">Служба статистики.</param>
    /// <param name="history">Служба журнала: она знает, у кого есть события.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    public StatisticsViewModel(
        IStatisticsService statistics,
        IHistoryService history,
        IDialogService dialogs)
        : base(StatisticsShellContributor.ReportDocumentId, "Статистика")
    {
        _statistics = Guard.NotNull(statistics);
        _history = Guard.NotNull(history);
        _dialogs = Guard.NotNull(dialogs);
    }

    /// <summary>Персонажи, доступные для отбора.</summary>
    public ObservableCollection<CharacterFilterOption> Characters { get; } = [];

    /// <summary>Периоды наблюдения.</summary>
    public IReadOnlyList<StatisticsPeriod> Periods { get; } = StatisticsPeriod.All;

    /// <summary>За выбранный период считать нечего.</summary>
    public bool IsEmpty => Report.IsEmpty;

    /// <summary>Краткая сводка над разделом.</summary>
    public string Summary => Report.IsEmpty
        ? "За выбранный период событий не было."
        : string.Join(
            " · ",
            $"бросков: {Format(Report.Rolls.Count)}",
            $"атак: {Format(Report.Attacks.Attacks)}",
            $"заклинаний: {Format(Report.Spells.Sum(spell => spell.Casts))}",
            $"изменений ресурсов: {Format(Report.Resources.Sum(resource => resource.Changes))}");

    /// <inheritdoc />
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await ReloadCharactersAsync(cancellationToken).ConfigureAwait(true);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Перечитывает сводку с учётом отбора.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после чтения.</returns>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        try
        {
            var query = new StatisticsQuery(SelectedCharacter?.Id, SelectedPeriod.Days);
            var result = await _statistics.GetAsync(query, cancellationToken).ConfigureAwait(true);

            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync("Статистика", result.Error!, null).ConfigureAwait(true);
                return;
            }

            Report = result.Value;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Перечитывает сводку по требованию пользователя.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после чтения.</returns>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
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

    private static string Format(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    partial void OnReportChanged(StatisticsReport value)
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnSelectedCharacterChanged(CharacterFilterOption? value) => Reload();

    partial void OnSelectedPeriodChanged(StatisticsPeriod value) => Reload();

    /// <summary>
    /// Перечитывает сводку, не дожидаясь завершения: вызывается из обработчиков
    /// изменения отбора, которые не могут быть асинхронными.
    /// </summary>
    private void Reload() => _ = ReloadAsync(CancellationToken.None);
}
