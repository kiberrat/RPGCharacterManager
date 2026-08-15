using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Представление шага предварительного просмотра и проверки персонажа.
/// </summary>
public partial class SummaryStepView : UserControl
{
    /// <summary>
    /// Создаёт представление шага проверки.
    /// </summary>
    public SummaryStepView() => AvaloniaXamlLoader.Load(this);
}
