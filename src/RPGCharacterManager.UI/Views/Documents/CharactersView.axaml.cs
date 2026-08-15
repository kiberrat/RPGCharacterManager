using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление документа «Персонажи» — списка созданных персонажей.
/// </summary>
public partial class CharactersView : UserControl
{
    /// <summary>
    /// Создаёт представление списка персонажей.
    /// </summary>
    public CharactersView() => AvaloniaXamlLoader.Load(this);
}
