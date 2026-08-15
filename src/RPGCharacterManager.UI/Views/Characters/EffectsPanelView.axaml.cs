using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Панель листа персонажа: действующие эффекты.
/// </summary>
public partial class EffectsPanelView : UserControl
{
    /// <summary>
    /// Создаёт панель.
    /// </summary>
    public EffectsPanelView() => AvaloniaXamlLoader.Load(this);
}
