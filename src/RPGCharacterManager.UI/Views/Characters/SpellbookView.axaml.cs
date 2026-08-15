using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Представление книги заклинаний персонажа.
/// </summary>
public partial class SpellbookView : UserControl
{
    /// <summary>
    /// Создаёт представление книги заклинаний персонажа.
    /// </summary>
    public SpellbookView() => AvaloniaXamlLoader.Load(this);
}
