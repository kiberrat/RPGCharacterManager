using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Models.Characters;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Страница выбора одного объекта: расы, класса, подкласса, происхождения.
///
/// Требования каждого варианта проверяются заранее, поэтому недоступные варианты
/// показываются вместе с причиной, по которой они недоступны.
/// </summary>
public sealed partial class SingleChoiceStepViewModel : WizardStepViewModel
{
    /// <summary>Количество вариантов, загружаемых в список за один раз.</summary>
    public const int PageSize = 200;

    [ObservableProperty]
    private CharacterOptionViewModel? _selectedOption;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showUnavailable = true;

    [ObservableProperty]
    private string _summary = string.Empty;

    private bool _isRestoringSelection;

    /// <summary>
    /// Создаёт страницу одиночного выбора.
    /// </summary>
    /// <param name="definition">Описание шага.</param>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="builder">Мастер создания персонажа.</param>
    /// <param name="changed">Обратный вызов при изменении данных персонажа.</param>
    public SingleChoiceStepViewModel(
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

    /// <summary>Шаг зависит от другого шага, выбор на котором ещё не сделан.</summary>
    public bool IsWaitingForParent => Definition.ParentStepId is { } parent
        && Draft.GetSelection(parent) is null;

    /// <inheritdoc />
    public override Task ActivateAsync(CancellationToken cancellationToken = default) =>
        ReloadAsync(cancellationToken);

    /// <summary>
    /// Снимает выбор на шаге.
    /// </summary>
    [RelayCommand]
    private void ClearSelection() => SelectedOption = null;

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
            var page = await Builder
                .GetOptionsAsync(Definition, Draft, SearchText, ShowUnavailable, PageSize, cancellationToken)
                .ConfigureAwait(true);

            var current = Draft.GetSelection(Definition.Id);

            Options.Clear();

            foreach (var option in page.Options)
            {
                Options.Add(new CharacterOptionViewModel(option));
            }

            // Присваивание выбранного варианта не должно выглядеть новым выбором:
            // при обновлении списка состав персонажа не меняется.
            SetSelectedWithoutApplying(Options.FirstOrDefault(option => option.Id == current));

            UpdateSummary(page);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedOptionChanged(CharacterOptionViewModel? value)
    {
        if (_isRestoringSelection)
        {
            return;
        }

        // Недоступный вариант выбрать нельзя: выбор возвращается к прежнему значению.
        if (value is { IsAvailable: false })
        {
            SetSelectedWithoutApplying(
                Options.FirstOrDefault(option => option.Id == Draft.GetSelection(Definition.Id)));

            return;
        }

        Builder.SetSelection(Definition, Draft, value?.Id);
        NotifyChanged();
    }

    partial void OnSearchTextChanged(string value) => _ = ReloadAsync(CancellationToken.None);

    partial void OnShowUnavailableChanged(bool value) => _ = ReloadAsync(CancellationToken.None);

    /// <summary>
    /// Отмечает вариант выбранным, не изменяя состав персонажа.
    /// Применяется при обновлении списка и при отказе от недоступного варианта.
    /// </summary>
    /// <param name="option">Отмечаемый вариант.</param>
    private void SetSelectedWithoutApplying(CharacterOptionViewModel? option)
    {
        _isRestoringSelection = true;

        try
        {
            SelectedOption = option;
        }
        finally
        {
            _isRestoringSelection = false;
        }
    }

    private void UpdateSummary(CharacterOptionPage page)
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsWaitingForParent));

        if (page.TotalCount > page.Options.Count)
        {
            Summary = string.Create(
                CultureInfo.CurrentCulture,
                $"Показано {page.Options.Count} из {page.TotalCount}. Уточните поиск, чтобы увидеть остальные.");

            return;
        }

        Summary = string.Create(CultureInfo.CurrentCulture, $"Вариантов: {page.Options.Count}");
    }
}
