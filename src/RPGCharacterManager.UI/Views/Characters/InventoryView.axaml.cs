using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Представление инвентаря персонажа.
/// </summary>
public partial class InventoryView : UserControl
{
    /// <summary>
    /// Создаёт представление инвентаря персонажа.
    /// </summary>
    public InventoryView() => AvaloniaXamlLoader.Load(this);
}
