using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Панель листа персонажа: пользовательские поля.
/// </summary>
public partial class CustomFieldsPanelView : UserControl
{
    /// <summary>
    /// Создаёт панель.
    /// </summary>
    public CustomFieldsPanelView() => AvaloniaXamlLoader.Load(this);
}
