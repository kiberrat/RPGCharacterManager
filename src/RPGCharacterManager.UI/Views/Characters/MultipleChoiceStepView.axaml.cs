using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Представление шага выбора нескольких объектов игрового контента.
/// </summary>
public partial class MultipleChoiceStepView : UserControl
{
    /// <summary>
    /// Создаёт представление шага множественного выбора.
    /// </summary>
    public MultipleChoiceStepView() => AvaloniaXamlLoader.Load(this);
}
