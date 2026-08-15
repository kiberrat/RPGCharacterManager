using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Rules;

/// <summary>
/// Вариант выбора оператора сравнения.
/// </summary>
/// <param name="Value">Оператор.</param>
/// <param name="Title">Отображаемое обозначение.</param>
public sealed record ComparisonOperatorOption(RuleComparisonOperator Value, string Title);

/// <summary>
/// Вариант выбора логической связки.
/// </summary>
/// <param name="Value">Связка.</param>
/// <param name="Title">Отображаемое название.</param>
public sealed record LogicalOperatorOption(RuleLogicalOperator Value, string Title);

/// <summary>
/// Узел дерева условий в редакторе правил.
///
/// Один класс представляет и группу, и сравнение: это позволяет отображать дерево
/// единым шаблоном и свободно превращать одно в другое при перестроении условия.
/// </summary>
public sealed partial class ConditionNodeViewModel : ViewModelBase
{
    private readonly ConditionNodeViewModel? _parent;
    private readonly Action _changed;

    [ObservableProperty]
    private RuleLogicalOperator _logicalOperator = RuleLogicalOperator.And;

    [ObservableProperty]
    private bool _isNegated;

    [ObservableProperty]
    private string _left = string.Empty;

    [ObservableProperty]
    private RuleComparisonOperator _comparisonOperator = RuleComparisonOperator.GreaterOrEqual;

    [ObservableProperty]
    private string _right = string.Empty;

    private ConditionNodeViewModel(bool isGroup, ConditionNodeViewModel? parent, Action changed)
    {
        IsGroup = isGroup;
        _parent = parent;
        _changed = changed;
    }

    /// <summary>Доступные операторы сравнения, перечисленные документом 019.</summary>
    public static IReadOnlyList<ComparisonOperatorOption> ComparisonOperators { get; } =
    [
        new(RuleComparisonOperator.Equal, "равно"),
        new(RuleComparisonOperator.NotEqual, "не равно"),
        new(RuleComparisonOperator.Less, "меньше"),
        new(RuleComparisonOperator.LessOrEqual, "меньше или равно"),
        new(RuleComparisonOperator.Greater, "больше"),
        new(RuleComparisonOperator.GreaterOrEqual, "больше или равно"),
        new(RuleComparisonOperator.Contains, "содержит"),
        new(RuleComparisonOperator.Has, "имеет"),
        new(RuleComparisonOperator.HasNot, "не имеет"),
    ];

    /// <summary>Доступные логические связки.</summary>
    public static IReadOnlyList<LogicalOperatorOption> LogicalOperators { get; } =
    [
        new(RuleLogicalOperator.And, "И — выполняются все условия"),
        new(RuleLogicalOperator.Or, "ИЛИ — выполняется хотя бы одно"),
    ];

    /// <summary>Узел является группой условий.</summary>
    public bool IsGroup { get; }

    /// <summary>Узел является сравнением.</summary>
    public bool IsComparison => !IsGroup;

    /// <summary>Узел можно удалить: корневая группа удалению не подлежит.</summary>
    public bool CanRemove => _parent is not null;

    /// <summary>Вложенные узлы группы.</summary>
    public ObservableCollection<ConditionNodeViewModel> Children { get; } = [];

    /// <summary>
    /// Создаёт корневую группу условий.
    /// </summary>
    /// <param name="changed">Обратный вызов, уведомляющий редактор об изменении дерева.</param>
    /// <returns>Корневой узел.</returns>
    public static ConditionNodeViewModel CreateRoot(Action changed)
    {
        Guard.NotNull(changed);
        return new ConditionNodeViewModel(isGroup: true, parent: null, changed);
    }

    /// <summary>
    /// Строит дерево редактора по сохранённому условию правила.
    /// </summary>
    /// <param name="condition">Условие правила или <see langword="null"/>.</param>
    /// <param name="changed">Обратный вызов, уведомляющий редактор об изменении дерева.</param>
    /// <returns>Корневой узел дерева.</returns>
    public static ConditionNodeViewModel FromCondition(RuleCondition? condition, Action changed)
    {
        Guard.NotNull(changed);

        var root = CreateRoot(changed);

        switch (condition)
        {
            case RuleConditionGroup group:
                root.LogicalOperator = group.Operator;
                root.IsNegated = group.IsNegated;

                foreach (var child in group.Children)
                {
                    root.Children.Add(Build(child, root, changed));
                }

                break;

            case RuleComparison comparison:
                // Одиночное сравнение помещается в корневую группу, чтобы редактор
                // всегда работал с однородной структурой.
                root.Children.Add(Build(comparison, root, changed));
                break;

            default:
                break;
        }

        return root;
    }

    /// <summary>
    /// Преобразует узел редактора в условие правила.
    /// </summary>
    /// <returns>Условие или <see langword="null"/>, если группа пуста.</returns>
    public RuleCondition? ToCondition()
    {
        if (IsComparison)
        {
            return new RuleComparison
            {
                Left = Left.Trim(),
                Operator = ComparisonOperator,
                Right = Right.Trim(),
            };
        }

        var group = new RuleConditionGroup
        {
            Operator = LogicalOperator,
            IsNegated = IsNegated,
        };

        foreach (var child in Children)
        {
            var condition = child.ToCondition();

            if (condition is not null)
            {
                group.Children.Add(condition);
            }
        }

        // Пустая группа без отрицания не ограничивает правило и не сохраняется.
        return group.Children.Count == 0 && !group.IsNegated ? null : group;
    }

    /// <summary>
    /// Добавляет в группу новое сравнение.
    /// </summary>
    [RelayCommand]
    private void AddComparison()
    {
        if (!IsGroup)
        {
            return;
        }

        Children.Add(new ConditionNodeViewModel(isGroup: false, this, _changed)
        {
            Left = "Уровень",
            ComparisonOperator = RuleComparisonOperator.GreaterOrEqual,
            Right = "1",
        });

        _changed();
    }

    /// <summary>
    /// Добавляет в группу вложенную группу условий.
    /// </summary>
    [RelayCommand]
    private void AddGroup()
    {
        if (!IsGroup)
        {
            return;
        }

        Children.Add(new ConditionNodeViewModel(isGroup: true, this, _changed));
        _changed();
    }

    /// <summary>
    /// Удаляет узел из родительской группы.
    /// </summary>
    [RelayCommand]
    private void Remove()
    {
        if (_parent is null)
        {
            return;
        }

        _parent.Children.Remove(this);
        _changed();
    }

    partial void OnLeftChanged(string value) => _changed();

    partial void OnRightChanged(string value) => _changed();

    partial void OnComparisonOperatorChanged(RuleComparisonOperator value) => _changed();

    partial void OnLogicalOperatorChanged(RuleLogicalOperator value) => _changed();

    partial void OnIsNegatedChanged(bool value) => _changed();

    private static ConditionNodeViewModel Build(
        RuleCondition condition,
        ConditionNodeViewModel parent,
        Action changed)
    {
        switch (condition)
        {
            case RuleConditionGroup group:
                var groupNode = new ConditionNodeViewModel(isGroup: true, parent, changed)
                {
                    LogicalOperator = group.Operator,
                    IsNegated = group.IsNegated,
                };

                foreach (var child in group.Children)
                {
                    groupNode.Children.Add(Build(child, groupNode, changed));
                }

                return groupNode;

            case RuleComparison comparison:
                return new ConditionNodeViewModel(isGroup: false, parent, changed)
                {
                    Left = comparison.Left,
                    ComparisonOperator = comparison.Operator,
                    Right = comparison.Right,
                };

            default:
                return new ConditionNodeViewModel(isGroup: true, parent, changed);
        }
    }
}
