using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Панель листа персонажа: черты и способности.
/// </summary>
public partial class TraitsPanelView : UserControl
{
    /// <summary>
    /// Создаёт панель.
    /// </summary>
    public TraitsPanelView() => AvaloniaXamlLoader.Load(this);
}
