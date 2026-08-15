using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Rules;

/// <summary>
/// Представление одного узла дерева условий.
/// Вкладывает само себя для отображения вложенных групп.
/// </summary>
public partial class ConditionNodeView : UserControl
{
    /// <summary>
    /// Создаёт представление узла условия.
    /// </summary>
    public ConditionNodeView() => AvaloniaXamlLoader.Load(this);
}
