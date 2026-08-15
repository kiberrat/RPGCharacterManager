using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Панель листа персонажа: навыки персонажа.
/// </summary>
public partial class SkillsPanelView : UserControl
{
    /// <summary>
    /// Создаёт панель.
    /// </summary>
    public SkillsPanelView() => AvaloniaXamlLoader.Load(this);
}
