using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление документа «Обзор».
/// </summary>
public partial class OverviewView : UserControl
{
    /// <summary>
    /// Создаёт представление документа обзора.
    /// </summary>
    public OverviewView() => AvaloniaXamlLoader.Load(this);
}
