using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>Представление каталога встроенных мини-игр.</summary>
public partial class QuietTimeView : UserControl
{
    /// <summary>Создаёт представление.</summary>
    public QuietTimeView() => AvaloniaXamlLoader.Load(this);
}
