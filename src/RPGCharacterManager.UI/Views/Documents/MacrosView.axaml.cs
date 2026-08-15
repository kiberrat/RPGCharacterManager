using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление раздела макросов.
/// </summary>
public partial class MacrosView : UserControl
{
    /// <summary>
    /// Создаёт представление раздела макросов.
    /// </summary>
    public MacrosView() => AvaloniaXamlLoader.Load(this);
}
