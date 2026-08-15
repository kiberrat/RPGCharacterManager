using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Панель листа персонажа: ссылка на панель, которой больше нет.
/// </summary>
public partial class MissingPanelView : UserControl
{
    /// <summary>
    /// Создаёт панель.
    /// </summary>
    public MissingPanelView() => AvaloniaXamlLoader.Load(this);
}
