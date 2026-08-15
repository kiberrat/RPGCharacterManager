using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление статистики игры.
/// </summary>
public partial class StatisticsView : UserControl
{
    /// <summary>
    /// Создаёт представление статистики.
    /// </summary>
    public StatisticsView() => AvaloniaXamlLoader.Load(this);
}
