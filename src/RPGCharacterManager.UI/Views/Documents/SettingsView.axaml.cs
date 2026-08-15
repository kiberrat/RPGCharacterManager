using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление документа «Настройки».
/// </summary>
public partial class SettingsView : UserControl
{
    /// <summary>
    /// Создаёт представление документа настроек.
    /// </summary>
    public SettingsView() => AvaloniaXamlLoader.Load(this);
}
