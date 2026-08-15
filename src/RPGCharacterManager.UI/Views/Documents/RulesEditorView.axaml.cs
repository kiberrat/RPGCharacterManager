using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление документа «Правила» — визуального конструктора игровых механик.
/// </summary>
public partial class RulesEditorView : UserControl
{
    /// <summary>
    /// Создаёт представление редактора правил.
    /// </summary>
    public RulesEditorView() => AvaloniaXamlLoader.Load(this);
}
