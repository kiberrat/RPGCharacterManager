using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление глобального поиска.
/// </summary>
public partial class SearchView : UserControl
{
    /// <summary>
    /// Создаёт представление глобального поиска.
    /// </summary>
    public SearchView() => AvaloniaXamlLoader.Load(this);
}
