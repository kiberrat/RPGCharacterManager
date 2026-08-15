using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Представление эффектов персонажа.
/// </summary>
public partial class EffectsView : UserControl
{
    /// <summary>
    /// Создаёт представление эффектов персонажа.
    /// </summary>
    public EffectsView() => AvaloniaXamlLoader.Load(this);
}
