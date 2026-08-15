using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Панель листа персонажа: инвентарь персонажа.
/// </summary>
public partial class InventoryPanelView : UserControl
{
    /// <summary>
    /// Создаёт панель.
    /// </summary>
    public InventoryPanelView() => AvaloniaXamlLoader.Load(this);
}
