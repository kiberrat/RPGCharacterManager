using RPGCharacterManager.Core.Abstractions.Layouts;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.ViewModels.Documents;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Панель на вкладке листа персонажа.
///
/// Собственных данных у панели нет: она показывает часть листа, поэтому
/// хранит ссылку на модель листа целиком. Так расположение панелей меняется
/// без разделения самого листа на одиннадцать моделей представления.
/// </summary>
public sealed class SheetPanelViewModel
{
    /// <summary>
    /// Создаёт панель листа.
    /// </summary>
    /// <param name="panel">Панель макета.</param>
    /// <param name="sheet">Модель листа персонажа.</param>
    public SheetPanelViewModel(LayoutPanel panel, CharacterSheetViewModel sheet)
    {
        Panel = Guard.NotNull(panel);
        Sheet = Guard.NotNull(sheet);
    }

    /// <summary>Панель макета.</summary>
    public LayoutPanel Panel { get; }

    /// <summary>Модель листа персонажа.</summary>
    public CharacterSheetViewModel Sheet { get; }

    /// <summary>Ключ панели.</summary>
    public string PanelId => Panel.PanelId;

    /// <summary>Название панели.</summary>
    public string Title => Panel.Title;

    /// <summary>Панели с таким ключом больше нет в приложении.</summary>
    public bool IsMissing => Panel.IsMissing;

    /// <summary>Доля ширины вкладки, которую занимает панель.</summary>
    public double Width => Panel.Width;
}

/// <summary>
/// Вкладка листа персонажа, собранная по макету.
/// </summary>
public sealed class SheetTabViewModel
{
    /// <summary>
    /// Создаёт вкладку листа.
    /// </summary>
    /// <param name="tab">Вкладка макета.</param>
    /// <param name="panels">Панели вкладки.</param>
    public SheetTabViewModel(LayoutTab tab, IReadOnlyList<SheetPanelViewModel> panels)
    {
        Guard.NotNull(tab);

        Title = tab.Title;
        Panels = Guard.NotNull(panels);
    }

    /// <summary>Заголовок вкладки.</summary>
    public string Title { get; }

    /// <summary>Панели вкладки в порядке отображения.</summary>
    public IReadOnlyList<SheetPanelViewModel> Panels { get; }

    /// <summary>На вкладке нет ни одной панели.</summary>
    public bool IsEmpty => Panels.Count == 0;
}
