using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Представление шага мастера с формой полей персонажа.
/// </summary>
public partial class FieldsStepView : UserControl
{
    /// <summary>
    /// Создаёт представление шага формы полей.
    /// </summary>
    public FieldsStepView() => AvaloniaXamlLoader.Load(this);
}
