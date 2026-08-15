using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Models.Rules;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;
using RPGCharacterManager.UI.ViewModels.Rules;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Строка списка правил.
/// </summary>
/// <param name="Rule">Правило.</param>
/// <param name="TriggerName">Отображаемое название события.</param>
public sealed record RuleListItem(RuleDefinition Rule, string TriggerName)
{
    /// <summary>Название правила.</summary>
    public string Name => Rule.Name;

    /// <summary>Категория правила.</summary>
    public string Category => Rule.Category;

    /// <summary>Приоритет правила.</summary>
    public int Priority => Rule.Priority;

    /// <summary>Правило включено.</summary>
    public bool Enabled => Rule.Enabled;
}

/// <summary>
/// Документ «Правила»: визуальный редактор игровых механик.
///
/// Расположение элементов соответствует документу 019_Редактор_правил.md:
/// слева — категории и список правил, в центре — конструктор события, условий
/// и действий, справа — свойства правила, снизу — тестирование на пробном объекте.
/// </summary>
public sealed partial class RulesEditorViewModel : DocumentViewModelBase
{
    private const string AllCategoriesTitle = "Все категории";

    private readonly IRuleService _rules;
    private readonly IRuleEngine _ruleEngine;
    private readonly IRuleValidator _validator;
    private readonly IRuleTriggerCatalog _triggers;
    private readonly IBackgroundTaskService _backgroundTasks;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;

    /// <summary>Признак загрузки правила в редактор: подавляет отметку об изменениях.</summary>
    private bool _isLoadingRule;

    [ObservableProperty]
    private string _selectedCategory = AllCategoriesTitle;

    [ObservableProperty]
    private RuleListItem? _selectedRule;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private string _ruleName = string.Empty;

    [ObservableProperty]
    private string _ruleDescription = string.Empty;

    [ObservableProperty]
    private string _ruleCategory = RuleCategories.Custom;

    [ObservableProperty]
    private RuleTrigger? _ruleTrigger;

    [ObservableProperty]
    private int _rulePriority;

    [ObservableProperty]
    private bool _ruleEnabled = true;

    [ObservableProperty]
    private string _ruleAuthor = string.Empty;

    [ObservableProperty]
    private ConditionNodeViewModel? _conditionRoot;

    [ObservableProperty]
    private IRuleActionHandler? _selectedActionKind;

    [ObservableProperty]
    private string _testVariables = string.Empty;

    [ObservableProperty]
    private string _testTags = string.Empty;

    [ObservableProperty]
    private string _testResult = string.Empty;

    [ObservableProperty]
    private bool _hasTestResult;

    /// <summary>
    /// Создаёт модель представления редактора правил.
    /// </summary>
    /// <param name="rules">Служба хранения правил.</param>
    /// <param name="ruleEngine">Движок выполнения правил.</param>
    /// <param name="validator">Служба проверки правил.</param>
    /// <param name="triggers">Перечень событий.</param>
    /// <param name="backgroundTasks">Служба фоновых задач.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    /// <param name="notifications">Служба всплывающих уведомлений.</param>
    public RulesEditorViewModel(
        IRuleService rules,
        IRuleEngine ruleEngine,
        IRuleValidator validator,
        IRuleTriggerCatalog triggers,
        IBackgroundTaskService backgroundTasks,
        IDialogService dialogs,
        INotificationService notifications)
        : base(CoreShellContributor.RulesDocumentId, "Правила")
    {
        _rules = Guard.NotNull(rules);
        _ruleEngine = Guard.NotNull(ruleEngine);
        _validator = Guard.NotNull(validator);
        _triggers = Guard.NotNull(triggers);
        _backgroundTasks = Guard.NotNull(backgroundTasks);
        _dialogs = Guard.NotNull(dialogs);
        _notifications = Guard.NotNull(notifications);

        Categories = [AllCategoriesTitle, .. RuleCategories.All];
        AvailableTriggers = _triggers.Triggers;
        AvailableActionKinds = _ruleEngine.ActionHandlers
            .OrderBy(handler => handler.DisplayName, StringComparer.CurrentCulture)
            .ToList();

        _selectedActionKind = FirstOrNull(AvailableActionKinds);
    }

    /// <summary>
    /// Возвращает первый элемент списка или <see langword="null"/> для пустого списка.
    /// </summary>
    /// <typeparam name="TItem">Тип элементов.</typeparam>
    /// <param name="items">Список.</param>
    /// <returns>Первый элемент или <see langword="null"/>.</returns>
    private static TItem? FirstOrNull<TItem>(IReadOnlyList<TItem> items)
        where TItem : class => items.Count > 0 ? items[0] : null;

    /// <summary>Категории для фильтра списка правил.</summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary>Категории, доступные при создании правила.</summary>
    public IReadOnlyList<string> EditableCategories { get; } = RuleCategories.All;

    /// <summary>События, доступные для выбора.</summary>
    public IReadOnlyList<RuleTrigger> AvailableTriggers { get; }

    /// <summary>Виды действий, доступные для добавления.</summary>
    public IReadOnlyList<IRuleActionHandler> AvailableActionKinds { get; }

    /// <summary>Все загруженные правила.</summary>
    public ObservableCollection<RuleListItem> AllRules { get; } = [];

    /// <summary>Правила, прошедшие фильтр по категории.</summary>
    public ObservableCollection<RuleListItem> VisibleRules { get; } = [];

    /// <summary>Действия редактируемого правила в порядке применения.</summary>
    public ObservableCollection<ActionEditorViewModel> Actions { get; } = [];

    /// <summary>Замечания проверки текущего правила и всего набора.</summary>
    public ObservableCollection<RuleIssue> Issues { get; } = [];

    /// <summary>Правило открыто в редакторе.</summary>
    public bool IsRuleOpen => ConditionRoot is not null;

    /// <summary>Проверка не нашла замечаний.</summary>
    public bool HasNoIssues => Issues.Count == 0;

    /// <inheritdoc />
    public override Task InitializeAsync(CancellationToken cancellationToken = default) =>
        ReloadAsync(cancellationToken);

    /// <inheritdoc />
    public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
    {
        if (!HasUnsavedChanges)
        {
            return true;
        }

        return await _dialogs.ShowConfirmationAsync(
                "Несохранённое правило",
                "В правиле есть несохранённые изменения. Закрыть раздел и потерять их?")
            .ConfigureAwait(true);
    }

    /// <summary>
    /// Перечитывает список правил из базы данных.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    [RelayCommand]
    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        var loaded = await _backgroundTasks
            .RunAsync("Загрузка игровых правил", _rules.GetAllAsync, cancellationToken)
            .ConfigureAwait(true);

        AllRules.Clear();

        foreach (var rule in loaded)
        {
            AllRules.Add(CreateListItem(rule));
        }

        ApplyCategoryFilter();
        ValidateAll();
    }

    /// <summary>
    /// Создаёт новое правило и открывает его в конструкторе.
    /// </summary>
    [RelayCommand]
    private void CreateRule()
    {
        var rule = new RuleDefinition
        {
            Name = "Новое правило",
            Category = RuleCategories.Custom,
            Trigger = FirstOrNull(AvailableTriggers)?.Key ?? string.Empty,
            Enabled = true,
        };

        LoadRule(rule);
        HasUnsavedChanges = true;
        SelectedRule = null;
    }

    /// <summary>
    /// Создаёт копию выбранного правила.
    /// </summary>
    [RelayCommand]
    private void DuplicateRule()
    {
        if (SelectedRule is null)
        {
            return;
        }

        var copy = SelectedRule.Rule.Clone();
        copy.Id = Guid.NewGuid();
        copy.Name = $"{copy.Name} — копия";

        LoadRule(copy);
        HasUnsavedChanges = true;
        SelectedRule = null;
    }

    /// <summary>
    /// Сохраняет правило в базу данных.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var rule = BuildRule();

        if (rule is null)
        {
            return;
        }

        var issues = _validator.Validate(rule);
        ShowIssues(issues);

        if (issues.Any(issue => issue.Severity == RuleIssueSeverity.Error))
        {
            await _dialogs.ShowErrorAsync(
                    "Правило содержит ошибки",
                    "Исправьте ошибки, перечисленные в разделе проверки, и повторите сохранение.")
                .ConfigureAwait(true);
            return;
        }

        var result = await _backgroundTasks
            .RunAsync("Сохранение правила", token => _rules.SaveAsync(rule, token), cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Сохранение правила", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);
            return;
        }

        HasUnsavedChanges = false;
        _notifications.Show($"Правило «{rule.Name}» сохранено", NotificationKind.Success);

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
        SelectedRule = AllRules.FirstOrDefault(item => item.Rule.Id == rule.Id);
    }

    /// <summary>
    /// Удаляет выбранное правило.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (SelectedRule is null)
        {
            return;
        }

        var name = SelectedRule.Name;

        var confirmed = await _dialogs
            .ShowConfirmationAsync("Удаление правила", $"Удалить правило «{name}»?")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        await _rules.DeleteAsync(SelectedRule.Rule.Id, cancellationToken).ConfigureAwait(true);

        CloseEditor();
        _notifications.Show($"Правило «{name}» удалено", NotificationKind.Success);

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Добавляет в правило действие выбранного вида.
    /// </summary>
    [RelayCommand]
    private void AddAction()
    {
        if (SelectedActionKind is null || !IsRuleOpen)
        {
            return;
        }

        Actions.Add(CreateActionViewModel(SelectedActionKind, action: null));
        MarkChanged();
    }

    /// <summary>
    /// Проверяет текущее правило и весь набор на ошибки и конфликты.
    /// </summary>
    [RelayCommand]
    private void Validate() => ValidateAll();

    /// <summary>
    /// Применяет правило к пробному объекту и показывает результат.
    ///
    /// Проверка выполняется на копии пробного объекта, поэтому исходные значения
    /// сохраняются между запусками.
    /// </summary>
    [RelayCommand]
    private void RunTest()
    {
        var rule = BuildRule();

        if (rule is null)
        {
            return;
        }

        var target = BuildTestTarget();
        var report = _ruleEngine.Execute(rule.Trigger, target, [rule]);

        TestResult = FormatReport(report, target);
        HasTestResult = true;
    }

    partial void OnSelectedCategoryChanged(string value) => ApplyCategoryFilter();

    partial void OnSelectedRuleChanged(RuleListItem? value)
    {
        if (value is not null)
        {
            LoadRule(value.Rule.Clone());
        }
    }

    partial void OnRuleNameChanged(string value) => MarkChanged();

    partial void OnRuleDescriptionChanged(string value) => MarkChanged();

    partial void OnRuleCategoryChanged(string value) => MarkChanged();

    partial void OnRuleTriggerChanged(RuleTrigger? value) => MarkChanged();

    partial void OnRulePriorityChanged(int value) => MarkChanged();

    partial void OnRuleEnabledChanged(bool value) => MarkChanged();

    partial void OnRuleAuthorChanged(string value) => MarkChanged();

    partial void OnConditionRootChanged(ConditionNodeViewModel? value) => OnPropertyChanged(nameof(IsRuleOpen));

    private void LoadRule(RuleDefinition rule)
    {
        _isLoadingRule = true;

        try
        {
            RuleName = rule.Name;
            RuleDescription = rule.Description ?? string.Empty;
            RuleCategory = rule.Category;
            RuleTrigger = _triggers.Find(rule.Trigger) ?? FirstOrNull(AvailableTriggers);
            RulePriority = rule.Priority;
            RuleEnabled = rule.Enabled;
            RuleAuthor = rule.Author ?? string.Empty;

            EditedRuleId = rule.Id;
            ConditionRoot = ConditionNodeViewModel.FromCondition(rule.Condition, MarkChanged);

            Actions.Clear();

            foreach (var action in rule.Actions)
            {
                var handler = AvailableActionKinds
                    .FirstOrDefault(item => string.Equals(item.Kind, action.Kind, StringComparison.OrdinalIgnoreCase));

                // Действие неизвестного вида сохраняется в правиле, но не может быть
                // отредактировано: проверка сообщит о нём пользователю.
                if (handler is not null)
                {
                    Actions.Add(CreateActionViewModel(handler, action));
                }
            }

            HasUnsavedChanges = false;
            HasTestResult = false;
            TestResult = string.Empty;
        }
        finally
        {
            _isLoadingRule = false;
        }

        ValidateAll();
    }

    /// <summary>Идентификатор редактируемого правила.</summary>
    private Guid EditedRuleId { get; set; }

    private void CloseEditor()
    {
        _isLoadingRule = true;

        try
        {
            ConditionRoot = null;
            Actions.Clear();
            RuleName = string.Empty;
            RuleDescription = string.Empty;
            HasUnsavedChanges = false;
            HasTestResult = false;
            SelectedRule = null;
        }
        finally
        {
            _isLoadingRule = false;
        }
    }

    private ActionEditorViewModel CreateActionViewModel(IRuleActionHandler handler, RuleAction? action) =>
        new(handler, action, MarkChanged, RemoveAction, MoveAction);

    private void RemoveAction(ActionEditorViewModel action) => Actions.Remove(action);

    private void MoveAction(ActionEditorViewModel action, int offset)
    {
        var index = Actions.IndexOf(action);
        var target = index + offset;

        if (index < 0 || target < 0 || target >= Actions.Count)
        {
            return;
        }

        Actions.Move(index, target);
        MarkChanged();
    }

    private void MarkChanged()
    {
        if (_isLoadingRule)
        {
            return;
        }

        HasUnsavedChanges = true;

        // Проверка выполняется при каждом изменении, поэтому раздел «Результат проверки»
        // всегда описывает текущее состояние правила, а не последнее сохранённое.
        // Разбор формул кэшируется движком, поэтому повторная проверка недорога.
        ValidateAll();
    }

    private RuleDefinition? BuildRule()
    {
        if (ConditionRoot is null)
        {
            return null;
        }

        var rule = new RuleDefinition
        {
            Id = EditedRuleId == Guid.Empty ? Guid.NewGuid() : EditedRuleId,
            Name = RuleName.Trim(),
            Description = string.IsNullOrWhiteSpace(RuleDescription) ? null : RuleDescription.Trim(),
            Category = RuleCategory,
            Trigger = RuleTrigger?.Key ?? string.Empty,
            Priority = RulePriority,
            Enabled = RuleEnabled,
            Condition = ConditionRoot.ToCondition(),
            Author = string.IsNullOrWhiteSpace(RuleAuthor) ? null : RuleAuthor.Trim(),
        };

        foreach (var action in Actions)
        {
            rule.Actions.Add(action.ToAction());
        }

        EditedRuleId = rule.Id;
        return rule;
    }

    private void ApplyCategoryFilter()
    {
        VisibleRules.Clear();

        var filtered = string.Equals(SelectedCategory, AllCategoriesTitle, StringComparison.Ordinal)
            ? AllRules
            : AllRules.Where(item => string.Equals(item.Category, SelectedCategory, StringComparison.Ordinal));

        foreach (var item in filtered)
        {
            VisibleRules.Add(item);
        }
    }

    private void ValidateAll()
    {
        var rules = AllRules.Select(item => item.Rule).ToList();

        var current = BuildRule();

        if (current is not null)
        {
            // Редактируемое правило заменяет свою сохранённую версию, чтобы проверка
            // конфликтов учитывала ещё не сохранённые изменения.
            rules.RemoveAll(rule => rule.Id == current.Id);
            rules.Add(current);
        }

        ShowIssues(_validator.ValidateSet(rules));
    }

    private void ShowIssues(IReadOnlyList<RuleIssue> issues)
    {
        Issues.Clear();

        foreach (var issue in issues.OrderByDescending(issue => issue.Severity))
        {
            Issues.Add(issue);
        }

        OnPropertyChanged(nameof(HasNoIssues));
    }

    private RuleListItem CreateListItem(RuleDefinition rule) =>
        new(rule, _triggers.Find(rule.Trigger)?.DisplayName ?? rule.Trigger);

    /// <summary>
    /// Строит пробный объект из значений, введённых пользователем.
    /// Формат: по одной паре «имя = значение» в строке, признаки — через запятую.
    /// </summary>
    /// <returns>Пробный объект правил.</returns>
    private RuleTarget BuildTestTarget()
    {
        var target = new RuleTarget("Пробный персонаж");

        foreach (var line in TestVariables.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('=', 2);

            if (parts.Length != 2)
            {
                continue;
            }

            var name = parts[0].Trim();
            var text = parts[1].Trim().Replace(',', '.');

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            target.SetVariable(
                name,
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                    ? FormulaValue.FromNumber(number)
                    : FormulaValue.FromText(parts[1].Trim()));
        }

        foreach (var tag in TestTags.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            target.AddTag(tag.Trim());
        }

        return target;
    }

    private static string FormatReport(RuleExecutionReport report, RuleTarget target)
    {
        var lines = new List<string>();

        if (report.ExecutedRules.Count > 0)
        {
            lines.Add("Условия выполнены — правило применено.");
        }
        else
        {
            lines.Add("Условия не выполнены — правило пропущено.");
        }

        if (report.Outcomes.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Действия:");

            foreach (var outcome in report.Outcomes)
            {
                lines.Add($"  {(outcome.Succeeded ? "✓" : "✕")} {outcome.Description}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("Состояние объекта после применения:");

        foreach (var name in target.VariableNames.OrderBy(item => item, StringComparer.CurrentCulture))
        {
            if (target.TryGetVariable(name, out var value))
            {
                lines.Add($"  {name} = {value.AsText()}");
            }
        }

        if (target.Tags.Count > 0)
        {
            lines.Add($"  Признаки: {string.Join(", ", target.Tags.OrderBy(tag => tag, StringComparer.CurrentCulture))}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
