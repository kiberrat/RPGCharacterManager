using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>Представление формы обратной связи.</summary>
public partial class FeedbackView : UserControl
{
    /// <summary>Создаёт представление.</summary>
    public FeedbackView() => AvaloniaXamlLoader.Load(this);
}