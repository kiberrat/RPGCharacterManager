using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Панель листа персонажа: описание персонажа.
/// </summary>
public partial class DescriptionPanelView : UserControl
{
    /// <summary>
    /// Создаёт панель.
    /// </summary>
    public DescriptionPanelView() => AvaloniaXamlLoader.Load(this);
}
