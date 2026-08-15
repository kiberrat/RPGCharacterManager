using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление документа «Создание персонажа» — пошагового мастера.
/// </summary>
public partial class CharacterWizardView : UserControl
{
    /// <summary>
    /// Создаёт представление мастера создания персонажа.
    /// </summary>
    public CharacterWizardView() => AvaloniaXamlLoader.Load(this);
}
