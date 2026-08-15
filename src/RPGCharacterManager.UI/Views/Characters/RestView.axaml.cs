using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Представление отдыха персонажа.
/// </summary>
public partial class RestView : UserControl
{
    /// <summary>
    /// Создаёт представление отдыха персонажа.
    /// </summary>
    public RestView() => AvaloniaXamlLoader.Load(this);
}
