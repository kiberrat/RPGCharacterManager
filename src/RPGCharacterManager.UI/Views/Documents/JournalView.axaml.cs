using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление журнала событий.
/// </summary>
public partial class JournalView : UserControl
{
    /// <summary>
    /// Создаёт представление журнала событий.
    /// </summary>
    public JournalView() => AvaloniaXamlLoader.Load(this);
}
