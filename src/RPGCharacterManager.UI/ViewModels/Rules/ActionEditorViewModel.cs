using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Rules;

/// <summary>
/// Поле ввода одного параметра действия.
/// </summary>
public sealed partial class ActionParameterViewModel : ViewModelBase
{
    private readonly Action _changed;

    [ObservableProperty]
    private string _value = string.Empty;

    /// <summary>
    /// Создаёт поле ввода параметра.
    /// </summary>
    /// <param name="definition">Описание параметра, объявленное обработчиком действия.</param>
    /// <param name="value">Текущее значение.</param>
    /// <param name="changed">Обратный вызов, уведомляющий редактор об изменении.</param>
    public ActionParameterViewModel(RuleActionParameter definition, string value, Action changed)
    {
        Definition = Guard.NotNull(definition);
        _changed = Guard.NotNull(changed);
        _value = value;
    }

    /// <summary>Описание параметра.</summary>
    public RuleActionParameter Definition { get; }

    /// <summary>Отображаемое название параметра.</summary>
    public string DisplayName => Definition.DisplayName;

    /// <summary>Подсказка, поясняющая ожидаемое значение.</summary>
    public string Placeholder => Definition.Kind switch
    {
        RuleParameterKind.Expression => "выражение, например: Сила + 2",
        RuleParameterKind.VariableName => "имя параметра, например: Здоровье",
        RuleParameterKind.TagName => "название эффекта",
        _ => "текст",
    };

    /// <summary>Параметр обязателен к заполнению.</summary>
    public bool IsRequired => Definition.IsRequired;

    partial void OnValueChanged(string value) => _changed();
}

/// <summary>
/// Действие правила в редакторе.
/// </summary>
public sealed partial class ActionEditorViewModel : ViewModelBase
{
    private readonly Action _changed;
    private readonly Action<ActionEditorViewModel> _remove;
    private readonly Action<ActionEditorViewModel, int> _move;

    /// <summary>
    /// Создаёт модель представления действия.
    /// </summary>
    /// <param name="handler">Обработчик, определяющий вид действия и его параметры.</param>
    /// <param name="action">Сохранённое действие или <see langword="null"/> для нового.</param>
    /// <param name="changed">Обратный вызов, уведомляющий редактор об изменении.</param>
    /// <param name="remove">Удаление действия из списка.</param>
    /// <param name="move">Перемещение действия в списке на указанное число позиций.</param>
    public ActionEditorViewModel(
        IRuleActionHandler handler,
        RuleAction? action,
        Action changed,
        Action<ActionEditorViewModel> remove,
        Action<ActionEditorViewModel, int> move)
    {
        Handler = Guard.NotNull(handler);
        _changed = Guard.NotNull(changed);
        _remove = Guard.NotNull(remove);
        _move = Guard.NotNull(move);

        foreach (var parameter in handler.Parameters)
        {
            var value = action?.GetParameter(parameter.Name) ?? string.Empty;
            Parameters.Add(new ActionParameterViewModel(parameter, value, changed));
        }
    }

    /// <summary>Обработчик действия.</summary>
    public IRuleActionHandler Handler { get; }

    /// <summary>Отображаемое название действия.</summary>
    public string DisplayName => Handler.DisplayName;

    /// <summary>Пояснение к действию.</summary>
    public string Description => Handler.Description;

    /// <summary>Поля ввода параметров.</summary>
    public ObservableCollection<ActionParameterViewModel> Parameters { get; } = [];

    /// <summary>
    /// Преобразует состояние редактора в действие правила.
    /// </summary>
    /// <returns>Действие правила.</returns>
    public RuleAction ToAction()
    {
        var action = new RuleAction { Kind = Handler.Kind };

        foreach (var parameter in Parameters)
        {
            action.Parameters[parameter.Definition.Name] = parameter.Value.Trim();
        }

        return action;
    }

    /// <summary>
    /// Удаляет действие из правила.
    /// </summary>
    [RelayCommand]
    private void Remove()
    {
        _remove(this);
        _changed();
    }

    /// <summary>
    /// Перемещает действие на одну позицию выше: порядок действий определяет
    /// последовательность их применения.
    /// </summary>
    [RelayCommand]
    private void MoveUp() => _move(this, -1);

    /// <summary>
    /// Перемещает действие на одну позицию ниже.
    /// </summary>
    [RelayCommand]
    private void MoveDown() => _move(this, 1);
}
