using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление документа «Лист персонажа».
/// </summary>
public partial class CharacterSheetView : UserControl
{
    /// <summary>
    /// Создаёт представление листа персонажа.
    /// </summary>
    public CharacterSheetView() => AvaloniaXamlLoader.Load(this);
}
