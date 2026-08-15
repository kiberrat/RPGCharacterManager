using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Представление шага распределения характеристик.
/// </summary>
public partial class AttributesStepView : UserControl
{
    /// <summary>
    /// Создаёт представление шага распределения характеристик.
    /// </summary>
    public AttributesStepView() => AvaloniaXamlLoader.Load(this);
}
