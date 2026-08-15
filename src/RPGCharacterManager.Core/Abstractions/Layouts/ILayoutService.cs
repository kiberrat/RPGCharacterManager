using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Layouts;

/// <summary>
/// Описание панели, которую можно поставить на макет.
///
/// Панель — готовая часть интерфейса: «Характеристики», «Инвентарь», «Оружие».
/// Приложение не хранит перечень панелей в базе: их объявляют сами подсистемы,
/// поэтому панель, добавленная на будущем этапе, становится доступна макетам
/// без изменения ни службы, ни редактора (решение Р-93).
/// </summary>
/// <param name="Id">Внутренний ключ панели: «характеристики», «инвентарь».</param>
/// <param name="Title">Название панели для пользователя.</param>
/// <param name="Description">Пояснение: что панель показывает.</param>
/// <param name="Order">Порядок в перечне доступных панелей.</param>
public sealed record SheetPanelDescriptor(string Id, string Title, string Description, int Order)
{
    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>
/// Перечень панелей, доступных макету листа персонажа.
/// </summary>
public interface ISheetPanelCatalog
{
    /// <summary>Панели в порядке отображения.</summary>
    IReadOnlyList<SheetPanelDescriptor> Panels { get; }

    /// <summary>
    /// Находит описание панели по ключу.
    /// </summary>
    /// <param name="panelId">Ключ панели.</param>
    /// <returns>Описание панели или <see langword="null"/>, если она не объявлена.</returns>
    SheetPanelDescriptor? Find(string panelId);
}

/// <summary>
/// Панель, поставленная на вкладку макета.
/// </summary>
/// <param name="Id">Идентификатор записи макета.</param>
/// <param name="PanelId">Ключ панели.</param>
/// <param name="Title">Название панели.</param>
/// <param name="Width">
/// Доля ширины вкладки, которую занимает панель. Панели одной вкладки делят
/// ширину пропорционально этим долям.
/// </param>
/// <param name="IsMissing">Панели с таким ключом больше нет в приложении.</param>
public sealed record LayoutPanel(Guid Id, string PanelId, string Title, double Width, bool IsMissing);

/// <summary>
/// Вкладка макета.
/// </summary>
/// <param name="Id">Идентификатор вкладки.</param>
/// <param name="Title">Заголовок вкладки.</param>
/// <param name="Panels">Панели вкладки в порядке отображения.</param>
public sealed record LayoutTab(Guid Id, string Title, IReadOnlyList<LayoutPanel> Panels)
{
    /// <summary>На вкладке нет ни одной панели.</summary>
    public bool IsEmpty => Panels.Count == 0;
}

/// <summary>
/// Макет листа персонажа целиком.
/// </summary>
/// <param name="Id">Идентификатор макета.</param>
/// <param name="Name">Название макета.</param>
/// <param name="IsDefault">Макет применяется к листу персонажа.</param>
/// <param name="Tabs">Вкладки в порядке отображения.</param>
public sealed record Layout(Guid Id, string Name, bool IsDefault, IReadOnlyList<LayoutTab> Tabs)
{
    /// <summary>В макете нет ни одной вкладки.</summary>
    public bool IsEmpty => Tabs.Count == 0;
}

/// <summary>
/// Строка списка макетов.
/// </summary>
/// <param name="Id">Идентификатор макета.</param>
/// <param name="Name">Название макета.</param>
/// <param name="IsDefault">Макет применяется к листу персонажа.</param>
/// <param name="TabCount">Количество вкладок.</param>
/// <param name="PanelCount">Количество панелей.</param>
public sealed record LayoutListItem(Guid Id, string Name, bool IsDefault, int TabCount, int PanelCount)
{
    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// Макеты интерфейса: расположение панелей по вкладкам листа персонажа.
///
/// Приложение всегда работает по макету: встроенной разметки листа больше нет.
/// Если макета в базе ещё нет, служба создаёт его из каталога панелей, поэтому
/// первый запуск даёт привычный лист, который пользователь волен переделать.
/// </summary>
public interface ILayoutService
{
    /// <summary>
    /// Возвращает все макеты.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Макеты в порядке названий.</returns>
    Task<Result<IReadOnlyList<LayoutListItem>>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает применяемый макет, создавая его при первом обращении.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Макет листа персонажа.</returns>
    Task<Result<Layout>> GetCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает макет целиком.
    /// </summary>
    /// <param name="layoutId">Идентификатор макета.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Макет.</returns>
    Task<Result<Layout>> GetAsync(Guid layoutId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт макет: копию встроенного либо копию указанного.
    /// </summary>
    /// <param name="name">Название нового макета.</param>
    /// <param name="sourceLayoutId">Макет-образец; <see langword="null"/> — встроенный.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор созданного макета.</returns>
    Task<Result<Guid>> CreateAsync(
        string name,
        Guid? sourceLayoutId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Переименовывает макет.
    /// </summary>
    /// <param name="layoutId">Идентификатор макета.</param>
    /// <param name="name">Новое название.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат переименования.</returns>
    Task<Result> RenameAsync(Guid layoutId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Делает макет применяемым к листу персонажа.
    /// </summary>
    /// <param name="layoutId">Идентификатор макета.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат применения.</returns>
    Task<Result> ApplyAsync(Guid layoutId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет макет. Применяемый макет удалить нельзя.
    /// </summary>
    /// <param name="layoutId">Идентификатор макета.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат удаления.</returns>
    Task<Result> DeleteAsync(Guid layoutId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет вкладку в макет.
    /// </summary>
    /// <param name="layoutId">Идентификатор макета.</param>
    /// <param name="title">Заголовок вкладки.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор созданной вкладки.</returns>
    Task<Result<Guid>> AddTabAsync(
        Guid layoutId,
        string title,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Переименовывает вкладку.
    /// </summary>
    /// <param name="tabId">Идентификатор вкладки.</param>
    /// <param name="title">Новый заголовок.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат переименования.</returns>
    Task<Result> RenameTabAsync(Guid tabId, string title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет вкладку вместе со стоящими на ней панелями.
    /// </summary>
    /// <param name="tabId">Идентификатор вкладки.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат удаления.</returns>
    Task<Result> DeleteTabAsync(Guid tabId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Переставляет вкладку на новое место.
    /// </summary>
    /// <param name="tabId">Идентификатор вкладки.</param>
    /// <param name="position">Новая позиция, начиная с нуля.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат перестановки.</returns>
    Task<Result> MoveTabAsync(Guid tabId, int position, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ставит панель на вкладку.
    /// </summary>
    /// <param name="tabId">Идентификатор вкладки.</param>
    /// <param name="panelId">Ключ панели.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат добавления.</returns>
    Task<Result> AddPanelAsync(Guid tabId, string panelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Убирает панель с макета.
    /// </summary>
    /// <param name="panelId">Идентификатор записи макета.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат удаления.</returns>
    Task<Result> RemovePanelAsync(Guid panelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Переносит панель на вкладку и место, куда её перетащили.
    /// </summary>
    /// <param name="panelId">Идентификатор записи макета.</param>
    /// <param name="targetTabId">Вкладка назначения.</param>
    /// <param name="position">Новая позиция на вкладке, начиная с нуля.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат переноса.</returns>
    Task<Result> MovePanelAsync(
        Guid panelId,
        Guid targetTabId,
        int position,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Задаёт долю ширины, которую панель занимает на вкладке.
    /// </summary>
    /// <param name="panelId">Идентификатор записи макета.</param>
    /// <param name="width">Доля ширины.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат изменения.</returns>
    Task<Result> ResizePanelAsync(Guid panelId, double width, CancellationToken cancellationToken = default);
}
