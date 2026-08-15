using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RPGCharacterManager.Core.Abstractions.Layouts;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Панель на вкладке в редакторе макета.
/// </summary>
public sealed partial class LayoutPanelRowViewModel : ObservableObject
{
    [ObservableProperty]
    private double _width;

    /// <summary>
    /// Создаёт строку панели.
    /// </summary>
    /// <param name="panel">Панель макета.</param>
    public LayoutPanelRowViewModel(LayoutPanel panel)
    {
        Panel = Guard.NotNull(panel);
        _width = panel.Width;
    }

    /// <summary>Панель макета.</summary>
    public LayoutPanel Panel { get; }

    /// <summary>Идентификатор записи макета.</summary>
    public Guid Id => Panel.Id;

    /// <summary>Название панели.</summary>
    public string Title => Panel.Title;

    /// <summary>Панели с таким ключом больше нет в приложении.</summary>
    public bool IsMissing => Panel.IsMissing;

    /// <summary>Доля ширины, записанная текстом.</summary>
    public string WidthText => Width.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>
    /// Обновляет подпись доли при её изменении.
    /// </summary>
    /// <param name="value">Новая доля.</param>
    partial void OnWidthChanged(double value) => OnPropertyChanged(nameof(WidthText));
}

/// <summary>
/// Вкладка в редакторе макета.
/// </summary>
public sealed partial class LayoutTabRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title;

    /// <summary>
    /// Создаёт строку вкладки.
    /// </summary>
    /// <param name="tab">Вкладка макета.</param>
    public LayoutTabRowViewModel(LayoutTab tab)
    {
        Guard.NotNull(tab);

        Id = tab.Id;
        _title = tab.Title;
        StoredTitle = tab.Title;

        foreach (var panel in tab.Panels)
        {
            Panels.Add(new LayoutPanelRowViewModel(panel));
        }
    }

    /// <summary>Идентификатор вкладки.</summary>
    public Guid Id { get; }

    /// <summary>Заголовок, сохранённый в базе.</summary>
    public string StoredTitle { get; }

    /// <summary>Панели вкладки в порядке отображения.</summary>
    public ObservableCollection<LayoutPanelRowViewModel> Panels { get; } = [];

    /// <summary>На вкладке нет ни одной панели.</summary>
    public bool IsEmpty => Panels.Count == 0;

    /// <summary>Заголовок изменён и не сохранён.</summary>
    public bool IsChanged => !string.Equals(Title.Trim(), StoredTitle, StringComparison.Ordinal);

    /// <summary>
    /// Обновляет признак изменения при правке заголовка.
    /// </summary>
    /// <param name="value">Новый заголовок.</param>
    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(IsChanged));
}

/// <summary>
/// Запрос переноса панели, полученный от перетаскивания.
/// </summary>
/// <param name="Panel">Перетаскиваемая панель.</param>
/// <param name="Tab">Вкладка, на которую панель отпустили.</param>
/// <param name="Position">Место вставки на вкладке, начиная с нуля.</param>
public sealed record PanelDropRequest(
    LayoutPanelRowViewModel Panel,
    LayoutTabRowViewModel Tab,
    int Position);
