using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление раздела кампаний.
/// </summary>
public partial class CampaignsView : UserControl
{
    /// <summary>
    /// Создаёт представление раздела кампаний.
    /// </summary>
    public CampaignsView() => AvaloniaXamlLoader.Load(this);
}
