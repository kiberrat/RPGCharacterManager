namespace RPGCharacterManager.Core.Models.Entities;

/// <summary>
/// Макет листа персонажа: набор вкладок с расставленными по ним панелями.
///
/// Макетов может быть несколько, но применяется один: мастер держит «боевой»
/// макет с оружием и эффектами и «городской» с инвентарём и описанием, переключая
/// их по ходу игры.
/// </summary>
public class SheetLayout : EntityBase
{
    /// <summary>Название макета.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Макет применяется к листу персонажа.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Вкладки макета в порядке отображения.</summary>
    public ICollection<SheetLayoutTab> Tabs { get; set; } = [];
}

/// <summary>
/// Вкладка макета.
/// </summary>
public class SheetLayoutTab : EntityBase
{
    /// <summary>Идентификатор макета.</summary>
    public Guid LayoutId { get; set; }

    /// <summary>Макет.</summary>
    public SheetLayout? Layout { get; set; }

    /// <summary>Заголовок вкладки.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Порядок вкладки в макете.</summary>
    public int SortOrder { get; set; }

    /// <summary>Панели вкладки в порядке отображения.</summary>
    public ICollection<SheetLayoutPanel> Panels { get; set; } = [];
}

/// <summary>
/// Панель, поставленная на вкладку макета.
/// </summary>
public class SheetLayoutPanel : EntityBase
{
    /// <summary>Идентификатор вкладки.</summary>
    public Guid TabId { get; set; }

    /// <summary>Вкладка.</summary>
    public SheetLayoutTab? Tab { get; set; }

    /// <summary>
    /// Ключ панели из каталога панелей.
    ///
    /// Хранится строкой, а не ссылкой: панели объявляют подсистемы, и в базе
    /// их нет. Неизвестный ключ означает, что панель исчезла из приложения, —
    /// такая запись показывается явно, а не пропадает молча.
    /// </summary>
    public string PanelId { get; set; } = string.Empty;

    /// <summary>Порядок панели на вкладке.</summary>
    public int SortOrder { get; set; }

    /// <summary>Доля ширины вкладки, которую занимает панель.</summary>
    public double Width { get; set; } = 1;
}
