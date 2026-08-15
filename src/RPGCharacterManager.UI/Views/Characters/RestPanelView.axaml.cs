using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Панель листа персонажа: отдых и восстановление ресурсов.
/// </summary>
public partial class RestPanelView : UserControl
{
    /// <summary>
    /// Создаёт панель.
    /// </summary>
    public RestPanelView() => AvaloniaXamlLoader.Load(this);
}
