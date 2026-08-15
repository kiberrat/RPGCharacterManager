using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление редактора макетов интерфейса.
/// </summary>
public partial class LayoutEditorView : UserControl
{
    /// <summary>
    /// Создаёт представление редактора макетов.
    /// </summary>
    public LayoutEditorView() => AvaloniaXamlLoader.Load(this);
}
