using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Models.Characters;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Страница выбора нескольких объектов: навыков, черт, заклинаний.
/// </summary>
public sealed partial class MultipleChoiceStepViewModel : WizardStepViewModel
{
    /// <summary>Количество вариантов, загружаемых в список за один раз.</summary>
    public const int PageSize = 200;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showUnavailable = true;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _selectionSummary = string.Empty;

    [ObservableProperty]
    private bool _isLimitExceeded;

    private int? _limit;

    /// <summary>
    /// Создаёт страницу множественного выбора.
    /// </summary>
    /// <param name="definition">Описание шага.</param>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="builder">Мастер создания персонажа.</param>
    /// <param name="changed">Обратный вызов при изменении данных персонажа.</param>
    public MultipleChoiceStepViewModel(
        CharacterStepDefinition definition,
        CharacterDraft draft,
        ICharacterBuilderService builder,
        Action changed)
        : base(definition, draft, builder, changed)
    {
    }

    /// <summary>Доступные варианты выбора.</summary>
    public ObservableCollection<CharacterOptionViewModel> Options { get; } = [];

    /// <summary>Список вариантов пуст.</summary>
    public bool IsEmpty => Options.Count == 0;

    /// <inheritdoc />
    public override Task ActivateAsync(CancellationToken cancellationToken = default) =>
        ReloadAsync(cancellationToken);

    /// <summary>
    /// Снимает выбор со всех вариантов шага.
    /// </summary>
    [RelayCommand]
    private void ClearSelection()
    {
        Draft.GetSelections(Definition.Id).Clear();

        foreach (var option in Options)
        {
            option.IsSelected = false;
        }

        UpdateSelectionSummary();
        NotifyChanged();
    }

    /// <summary>
    /// Перечитывает список вариантов.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    [RelayCommand]
    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            _limit = await Builder
                .GetSelectionLimitAsync(Definition, Draft, cancellationToken)
                .ConfigureAwait(true);

            var page = await Builder
                .GetOptionsAsync(Definition, Draft, SearchText, ShowUnavailable, PageSize, cancellationToken)
                .ConfigureAwait(true);

            var selected = Draft.GetSelections(Definition.Id);

            Options.Clear();

            foreach (var option in page.Options)
            {
                Options.Add(new CharacterOptionViewModel(
                    option,
                    selected.Contains(option.Id),
                    OnOptionToggled));
            }

            OnPropertyChanged(nameof(IsEmpty));

            Summary = page.TotalCount > page.Options.Count
                ? string.Create(
                    CultureInfo.CurrentCulture,
                    $"Показано {page.Options.Count} из {page.TotalCount}. Уточните поиск, чтобы увидеть остальные.")
                : string.Create(CultureInfo.CurrentCulture, $"Вариантов: {page.Options.Count}");

            UpdateSelectionSummary();
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSearchTextChanged(string value) => _ = ReloadAsync(CancellationToken.None);

    partial void OnShowUnavailableChanged(bool value) => _ = ReloadAsync(CancellationToken.None);

    private void OnOptionToggled(CharacterOptionViewModel option)
    {
        var selected = Draft.GetSelections(Definition.Id);

        if (option.IsSelected)
        {
            // Недоступный вариант выбрать нельзя: отметка немедленно снимается.
            if (!option.IsAvailable)
            {
                option.IsSelected = false;
                return;
            }

            selected.Add(option.Id);
        }
        else
        {
            selected.Remove(option.Id);
        }

        UpdateSelectionSummary();
        NotifyChanged();
    }

    private void UpdateSelectionSummary()
    {
        var count = Draft.GetSelections(Definition.Id).Count;

        IsLimitExceeded = _limit is { } limit && count > limit;

        SelectionSummary = _limit is { } maximum
            ? string.Create(CultureInfo.CurrentCulture, $"Выбрано {count} из {maximum}")
            : string.Create(CultureInfo.CurrentCulture, $"Выбрано: {count}");
    }
}
