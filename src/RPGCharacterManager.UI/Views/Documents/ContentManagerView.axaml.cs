using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление документа «Контент» — редактора всех игровых объектов.
/// </summary>
public partial class ContentManagerView : UserControl
{
    /// <summary>
    /// Создаёт представление менеджера контента.
    /// </summary>
    public ContentManagerView() => AvaloniaXamlLoader.Load(this);
}
