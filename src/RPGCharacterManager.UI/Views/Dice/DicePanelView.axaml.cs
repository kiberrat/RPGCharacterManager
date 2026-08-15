using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Dice;

/// <summary>
/// Представление панели бросков кубиков.
/// </summary>
public partial class DicePanelView : UserControl
{
    /// <summary>
    /// Создаёт представление панели бросков.
    /// </summary>
    public DicePanelView() => AvaloniaXamlLoader.Load(this);
}
