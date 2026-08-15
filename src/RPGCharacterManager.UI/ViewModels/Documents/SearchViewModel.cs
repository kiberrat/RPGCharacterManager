using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Abstractions.Search;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Документ «Поиск»: глобальный поиск по всему приложению.
///
/// Окно не знает, где что лежит, и не перечисляет виды объектов: находки
/// приходят готовыми группами от подсистем (решение Р-96).
/// </summary>
public sealed partial class SearchViewModel : DocumentViewModelBase
{
    private readonly ISearchService _search;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasSearched;

    /// <summary>
    /// Создаёт модель представления поиска.
    /// </summary>
    /// <param name="search">Служба глобального поиска.</param>
    /// <param name="navigation">Навигация по документам.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    public SearchViewModel(
        ISearchService search,
        INavigationService navigation,
        IDialogService dialogs)
        : base(SearchShellContributor.ResultsDocumentId, "Поиск")
    {
        _search = Guard.NotNull(search);
        _navigation = Guard.NotNull(navigation);
        _dialogs = Guard.NotNull(dialogs);
    }

    /// <summary>Найденное, сгруппированное по видам объектов.</summary>
    public ObservableCollection<SearchGroup> Groups { get; } = [];

    /// <summary>Ничего не найдено.</summary>
    public bool IsEmpty => Groups.Count == 0;

    /// <summary>
    /// Ищет объекты по введённому запросу.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после поиска.</returns>
    [RelayCommand]
    private async Task FindAsync(CancellationToken cancellationToken)
    {
        var query = Query.Trim();

        if (query.Length < SearchDefaults.MinimumQueryLength)
        {
            Summary = $"Введите не меньше {SearchDefaults.MinimumQueryLength} знаков.";
            Groups.Clear();
            HasSearched = false;
            OnPropertyChanged(nameof(IsEmpty));

            return;
        }

        IsBusy = true;

        try
        {
            var result = await _search
                .SearchAsync(query, SearchDefaults.GroupLimit, cancellationToken).ConfigureAwait(true);

            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync("Поиск", result.Error!).ConfigureAwait(true);
                return;
            }

            Groups.Clear();

            foreach (var group in result.Value.Groups)
            {
                Groups.Add(group);
            }

            HasSearched = true;

            Summary = result.Value.IsEmpty
                ? $"По запросу «{query}» ничего не найдено."
                : $"Найдено: {result.Value.Count} в {Groups.Count} группах.";

            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Открывает документ, показывающий находку.
    /// </summary>
    /// <param name="hit">Находка.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после перехода.</returns>
    [RelayCommand]
    private async Task OpenAsync(SearchHit? hit, CancellationToken cancellationToken)
    {
        if (hit is null)
        {
            return;
        }

        try
        {
            await _navigation
                .OpenAsync(hit.DocumentId, hit.Parameter, cancellationToken).ConfigureAwait(true);
        }
        catch (InvalidOperationException exception)
        {
            await _dialogs.ShowErrorAsync("Переход к находке", exception.Message).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Очищает запрос и найденное.
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        Query = string.Empty;
        Summary = string.Empty;
        HasSearched = false;

        Groups.Clear();
        OnPropertyChanged(nameof(IsEmpty));
    }
}
