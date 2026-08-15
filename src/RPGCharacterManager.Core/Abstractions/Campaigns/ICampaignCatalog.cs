namespace RPGCharacterManager.Core.Abstractions.Campaigns;

/// <summary>
/// Идентификаторы видов объектов состава кампании, не являющихся игровым контентом.
///
/// Виды контента опознаются собственными идентификаторами из
/// <see cref="Content.ContentTypeIds"/>. Персонажи игроков контентом не являются,
/// поэтому их вид объявлен здесь — в контрактах, а не внутри одной подсистемы:
/// на него ссылается и состав кампании, и режим мастера.
/// </summary>
public static class CampaignObjectKinds
{
    /// <summary>Персонажи игроков.</summary>
    public const string Characters = "characters";
}

/// <summary>
/// Вид объектов, которые могут входить в состав кампании.
/// </summary>
/// <param name="Id">Идентификатор вида: «characters», «npcs», «monsters».</param>
/// <param name="Title">Название вида во множественном числе: «Монстры».</param>
/// <param name="SingularName">Название одного объекта: «Монстр».</param>
/// <param name="RoleTitle">Название столбца роли: «Игрок» у персонажей, «Роль» у остальных.</param>
/// <param name="Order">Порядок отображения.</param>
public sealed record CampaignKind(string Id, string Title, string SingularName, string RoleTitle, int Order)
{
    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>
/// Объект, который можно добавить в состав кампании.
/// </summary>
/// <param name="Id">Идентификатор объекта.</param>
/// <param name="Name">Название объекта.</param>
public sealed record CampaignObject(Guid Id, string Name)
{
    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// Перечень объектов, доступных кампании.
///
/// Каталог сводит вместе персонажей и игровой контент всех видов, поэтому состав
/// кампании работает с любым видом объектов одинаково, а новый вид контента
/// становится доступен кампаниям сразу после регистрации своего описания.
/// </summary>
public interface ICampaignCatalog
{
    /// <summary>Виды объектов в порядке отображения.</summary>
    IReadOnlyList<CampaignKind> Kinds { get; }

    /// <summary>
    /// Находит вид объектов по идентификатору.
    /// </summary>
    /// <param name="kindId">Идентификатор вида.</param>
    /// <returns>Вид объектов или <see langword="null"/>, если он не зарегистрирован.</returns>
    CampaignKind? FindKind(string kindId);

    /// <summary>
    /// Возвращает объекты вида, отфильтрованные по названию.
    /// </summary>
    /// <param name="kindId">Идентификатор вида.</param>
    /// <param name="search">Строка поиска по названию. Пустое значение отключает отбор.</param>
    /// <param name="limit">Наибольшее количество объектов в ответе.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Найденные объекты в порядке названий.</returns>
    Task<IReadOnlyList<CampaignObject>> SearchAsync(
        string kindId,
        string? search,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает названия объектов вида по их идентификаторам.
    ///
    /// Отсутствие идентификатора в ответе означает, что объект удалён: состав
    /// кампании ссылается на объекты без внешнего ключа, поэтому такую ссылку
    /// обнаруживает именно эта проверка.
    /// </summary>
    /// <param name="kindId">Идентификатор вида.</param>
    /// <param name="ids">Идентификаторы объектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Названия, сопоставленные идентификаторам найденных объектов.</returns>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        string kindId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}
