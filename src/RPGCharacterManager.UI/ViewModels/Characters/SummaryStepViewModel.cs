using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Замечание проверки персонажа в списке предварительного просмотра.
/// </summary>
public sealed class CharacterIssueViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт строку замечания.
    /// </summary>
    /// <param name="issue">Найденное замечание.</param>
    /// <param name="stepTitle">Название шага, на котором замечание устраняется.</param>
    public CharacterIssueViewModel(CharacterIssue issue, string stepTitle)
    {
        Issue = Guard.NotNull(issue);
        StepTitle = stepTitle;
    }

    /// <summary>Найденное замечание.</summary>
    public CharacterIssue Issue { get; }

    /// <summary>Идентификатор шага, на котором замечание устраняется.</summary>
    public string StepId => Issue.StepId;

    /// <summary>Название шага.</summary>
    public string StepTitle { get; }

    /// <summary>Текст замечания.</summary>
    public string Message => Issue.Message;

    /// <summary>Замечание препятствует созданию персонажа.</summary>
    public bool IsError => Issue.Severity == CharacterIssueSeverity.Error;

    /// <summary>Отображаемая важность замечания.</summary>
    public string SeverityName => IsError ? "Ошибка" : "Предупреждение";
}

/// <summary>
/// Страница предварительного просмотра и проверки персонажа.
///
/// Показывает готового персонажа со всеми вычисленными значениями и перечень
/// замечаний; на любой шаг можно вернуться, не теряя сделанный выбор.
/// </summary>
public sealed partial class SummaryStepViewModel : WizardStepViewModel
{
    [ObservableProperty]
    private string _characterSummary = string.Empty;

    [ObservableProperty]
    private bool _hasErrors;

    private readonly Action<string> _goToStep;

    /// <summary>
    /// Создаёт страницу проверки.
    /// </summary>
    /// <param name="definition">Описание шага.</param>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="builder">Мастер создания персонажа.</param>
    /// <param name="changed">Обратный вызов при изменении данных персонажа.</param>
    /// <param name="goToStep">Переход на шаг мастера по идентификатору.</param>
    public SummaryStepViewModel(
        CharacterStepDefinition definition,
        CharacterDraft draft,
        ICharacterBuilderService builder,
        Action changed,
        Action<string> goToStep)
        : base(definition, draft, builder, changed) =>
        _goToStep = Guard.NotNull(goToStep);

    /// <summary>
    /// Открывает шаг, на котором устраняется замечание.
    /// </summary>
    /// <param name="stepId">Идентификатор шага.</param>
    [RelayCommand]
    private void OpenStep(string? stepId)
    {
        if (!string.IsNullOrWhiteSpace(stepId))
        {
            _goToStep(stepId);
        }
    }

    /// <summary>Замечания, найденные при проверке.</summary>
    public ObservableCollection<CharacterIssueViewModel> Issues { get; } = [];

    /// <summary>Вычисленные характеристики.</summary>
    public ObservableCollection<CalculatedAttributeValue> Attributes { get; } = [];

    /// <summary>Вычисленные навыки.</summary>
    public ObservableCollection<CalculatedSkill> Skills { get; } = [];

    /// <summary>Вычисленные ресурсы.</summary>
    public ObservableCollection<CalculatedResource> Resources { get; } = [];

    /// <summary>Правила, применённые при расчёте.</summary>
    public ObservableCollection<string> AppliedRules { get; } = [];

    /// <summary>Замечаний нет.</summary>
    public bool HasNoIssues => Issues.Count == 0;

    /// <summary>Персонаж владеет хотя бы одним навыком.</summary>
    public bool HasSkills => Skills.Count > 0;

    /// <summary>У персонажа есть ресурсы.</summary>
    public bool HasResources => Resources.Count > 0;

    /// <summary>При расчёте применялись правила.</summary>
    public bool HasAppliedRules => AppliedRules.Count > 0;

    /// <summary>
    /// Названия шагов мастера, сопоставленные их идентификаторам.
    /// Требуются, чтобы замечание указывало на понятное пользователю место.
    /// </summary>
    private Dictionary<string, string> StepTitles => Builder.Steps
        .ToDictionary(step => step.Id, step => step.Title, StringComparer.Ordinal);

    /// <inheritdoc />
    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        try
        {
            var calculation = await Builder.CalculateAsync(Draft, cancellationToken).ConfigureAwait(true);
            var issues = await Builder.ValidateAsync(Draft, cancellationToken).ConfigureAwait(true);
            var visibleAttributes = await Builder.GetAttributesAsync(Draft, cancellationToken).ConfigureAwait(true);
            var visibleAttributeIds = visibleAttributes.Select(attribute => attribute.Id).ToHashSet();

            Fill(Attributes, calculation.Attributes.Where(attribute => visibleAttributeIds.Contains(attribute.Id)));
            Fill(Skills, calculation.Skills);
            Fill(Resources, calculation.Resources);
            Fill(AppliedRules, calculation.AppliedRules.Distinct());

            var titles = StepTitles;

            Issues.Clear();

            foreach (var issue in issues.OrderByDescending(item => item.Severity))
            {
                Issues.Add(new CharacterIssueViewModel(
                    issue,
                    titles.TryGetValue(issue.StepId, out var title) ? title : "Мастер"));
            }

            HasErrors = Issues.Any(issue => issue.IsError);
            CharacterSummary = BuildSummary();

            OnPropertyChanged(nameof(HasNoIssues));
            OnPropertyChanged(nameof(HasSkills));
            OnPropertyChanged(nameof(HasResources));
            OnPropertyChanged(nameof(HasAppliedRules));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string BuildSummary()
    {
        var character = Draft.Character;
        var parts = new List<string>
        {
            string.IsNullOrWhiteSpace(character.Name) ? "Без имени" : character.Name,
            $"уровень {character.Level.ToString(CultureInfo.CurrentCulture)}",
        };

        if (!string.IsNullOrWhiteSpace(character.Gender))
        {
            parts.Add(character.Gender);
        }

        if (!string.IsNullOrWhiteSpace(character.Alignment))
        {
            parts.Add(character.Alignment);
        }

        return string.Join(", ", parts);
    }

    private static void Fill<TItem>(ObservableCollection<TItem> target, IEnumerable<TItem> items)
    {
        target.Clear();

        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
