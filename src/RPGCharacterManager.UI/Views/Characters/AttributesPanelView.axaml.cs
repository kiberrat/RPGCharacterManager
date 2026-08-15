using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Панель листа персонажа: характеристики и производные значения.
/// </summary>
public partial class AttributesPanelView : UserControl
{
    /// <summary>
    /// Создаёт панель.
    /// </summary>
    public AttributesPanelView() => AvaloniaXamlLoader.Load(this);
}
