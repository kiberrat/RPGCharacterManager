using Avalonia.Controls;
using Avalonia.Controls.Templates;
using RPGCharacterManager.UI.ViewModels.Characters;

namespace RPGCharacterManager.UI.Views.Characters;

/// <summary>
/// Шаблон панели листа персонажа: по ключу панели строит её представление.
///
/// Выбор представления — задача слоя представления, поэтому он здесь, а не
/// в модели: модель знает только ключ панели, полученный из макета.
/// </summary>
public sealed class SheetPanelTemplate : IDataTemplate
{
    /// <inheritdoc />
    public Control Build(object? param) =>
        param is SheetPanelViewModel panel
            ? SheetPanelCatalog.CreateView(panel.PanelId)
            : new MissingPanelView();

    /// <inheritdoc />
    public bool Match(object? data) => data is SheetPanelViewModel;
}
