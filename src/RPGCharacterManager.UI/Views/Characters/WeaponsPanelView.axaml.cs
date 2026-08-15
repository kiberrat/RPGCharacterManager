using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Панель листа персонажа: оружие и атаки.
/// </summary>
public partial class WeaponsPanelView : UserControl
{
    /// <summary>
    /// Создаёт панель.
    /// </summary>
    public WeaponsPanelView() => AvaloniaXamlLoader.Load(this);
}
