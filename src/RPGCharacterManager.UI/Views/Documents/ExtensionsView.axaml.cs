using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление расширений приложения.
/// </summary>
public partial class ExtensionsView : UserControl
{
    /// <summary>
    /// Создаёт представление расширений.
    /// </summary>
    public ExtensionsView() => AvaloniaXamlLoader.Load(this);
}
