using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Панель листа персонажа: надетые предметы по слотам.
/// </summary>
public partial class EquipmentPanelView : UserControl
{
    /// <summary>
    /// Создаёт панель.
    /// </summary>
    public EquipmentPanelView() => AvaloniaXamlLoader.Load(this);
}
