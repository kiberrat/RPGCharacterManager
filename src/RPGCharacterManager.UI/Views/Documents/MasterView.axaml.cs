using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление режима мастера.
/// </summary>
public partial class MasterView : UserControl
{
    /// <summary>
    /// Создаёт представление режима мастера.
    /// </summary>
    public MasterView() => AvaloniaXamlLoader.Load(this);
}
