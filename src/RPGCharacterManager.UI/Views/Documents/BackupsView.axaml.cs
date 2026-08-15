using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление документа «Резервные копии».
/// </summary>
public partial class BackupsView : UserControl
{
    /// <summary>
    /// Создаёт представление документа резервных копий.
    /// </summary>
    public BackupsView() => AvaloniaXamlLoader.Load(this);
}
