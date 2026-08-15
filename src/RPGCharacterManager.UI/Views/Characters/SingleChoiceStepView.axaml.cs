using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Представление шага выбора одного объекта игрового контента.
/// </summary>
public partial class SingleChoiceStepView : UserControl
{
    /// <summary>
    /// Создаёт представление шага одиночного выбора.
    /// </summary>
    public SingleChoiceStepView() => AvaloniaXamlLoader.Load(this);
}
