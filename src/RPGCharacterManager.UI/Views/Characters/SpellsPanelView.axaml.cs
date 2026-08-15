using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Панель листа персонажа: книга заклинаний.
/// </summary>
public partial class SpellsPanelView : UserControl
{
    /// <summary>
    /// Создаёт панель.
    /// </summary>
    public SpellsPanelView() => AvaloniaXamlLoader.Load(this);
}
