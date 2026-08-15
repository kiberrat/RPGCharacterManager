using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Shell;

/// <summary>
/// Представление строки состояния главного окна.
/// </summary>
public partial class StatusBarView : UserControl
{
    /// <summary>
    /// Создаёт представление строки состояния.
    /// </summary>
    public StatusBarView() => AvaloniaXamlLoader.Load(this);
}
