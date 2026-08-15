using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Представление шага выбора игровой системы и источников контента.
/// </summary>
public partial class GameSystemStepView : UserControl
{
    /// <summary>
    /// Создаёт представление шага выбора игровой системы.
    /// </summary>
    public GameSystemStepView() => AvaloniaXamlLoader.Load(this);
}
