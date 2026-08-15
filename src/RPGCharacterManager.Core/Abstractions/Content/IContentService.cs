using RPGCharacterManager.Core.Abstractions.Data;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Content;

/// <summary>
/// Описание вида игрового контента.
///
/// Каждый вид объектов — расы, классы, заклинания и прочие — описывается набором
/// полей. Редактор и служба контента работают только с этими описаниями, поэтому
/// новый вид контента подключается регистрацией описания и не требует изменения
/// ни интерфейса, ни хранилища.
/// </summary>
public interface IContentTypeDescriptor
{
    /// <summary>Внутренний идентификатор вида контента.</summary>
    string Id { get; }

    /// <summary>Название вида во множественном числе: «Расы».</summary>
    string DisplayName { get; }

    /// <summary>Название одного объекта: «Раса».</summary>
    string SingularName { get; }

    /// <summary>Пояснение к виду контента.</summary>
    string Description { get; }

    /// <summary>Порядок отображения в списке видов контента.</summary>
    int Order { get; }

    /// <summary>Тип сущности базы данных.</summary>
    Type EntityType { get; }

    /// <summary>Поля объекта в порядке отображения.</summary>
    IReadOnlyList<IContentField> Fields { get; }

    /// <summary>Списки вложенных записей объекта в порядке отображения.</summary>
    IReadOnlyList<IContentList> Collections { get; }

    /// <summary>
    /// Создаёт новый объект этого вида со значениями по умолчанию.
    /// </summary>
    /// <returns>Новый объект.</returns>
    EntityBase CreateInstance();

    /// <summary>
    /// Возвращает название объекта.
    /// </summary>
    /// <param name="entity">Игровой объект.</param>
    /// <returns>Название.</returns>
    string GetName(EntityBase entity);

    /// <summary>
    /// Задаёт название объекта.
    /// </summary>
    /// <param name="entity">Игровой объект.</param>
    /// <param name="name">Новое название.</param>
    void SetName(EntityBase entity, string name);
}

/// <summary>
/// Управление игровым контентом: поиск, чтение, сохранение и удаление объектов
/// любого зарегистрированного вида.
/// </summary>
public interface IContentService
{
    /// <summary>Зарегистрированные виды контента в порядке отображения.</summary>
    IReadOnlyList<IContentTypeDescriptor> Types { get; }

    /// <summary>
    /// Находит описание вида контента по идентификатору.
    /// </summary>
    /// <param name="typeId">Идентификатор вида.</param>
    /// <returns>Описание вида или <see langword="null"/>.</returns>
    IContentTypeDescriptor? FindType(string typeId);

    /// <summary>
    /// Возвращает страницу объектов вида, отфильтрованных по названию.
    /// </summary>
    /// <param name="typeId">Идентификатор вида контента.</param>
    /// <param name="search">Строка поиска по названию. Пустое значение отключает фильтр.</param>
    /// <param name="pageIndex">Номер страницы, начиная с нуля.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница объектов и общее количество найденных записей.</returns>
    Task<PagedResult<ContentItem>> SearchAsync(
        string typeId,
        string? search,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает строки списка для объектов с указанными идентификаторами.
    ///
    /// Отсутствие идентификатора в ответе означает, что объекта больше нет:
    /// так подсистемы, хранящие ссылки без внешнего ключа, узнают об удалении.
    /// </summary>
    /// <param name="typeId">Идентификатор вида контента.</param>
    /// <param name="ids">Идентификаторы объектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Найденные объекты; порядок не определён.</returns>
    Task<IReadOnlyList<ContentItem>> GetItemsAsync(
        string typeId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Загружает объект целиком для редактирования.
    /// </summary>
    /// <param name="typeId">Идентификатор вида контента.</param>
    /// <param name="id">Идентификатор объекта.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Объект или <see langword="null"/>, если он не найден.</returns>
    Task<EntityBase?> GetAsync(string typeId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет объект: создаёт новый либо обновляет существующий.
    /// </summary>
    /// <param name="typeId">Идентификатор вида контента.</param>
    /// <param name="entity">Сохраняемый объект.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат сохранения.</returns>
    Task<Result> SaveAsync(string typeId, EntityBase entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет объект.
    /// </summary>
    /// <param name="typeId">Идентификатор вида контента.</param>
    /// <param name="id">Идентификатор объекта.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат удаления.</returns>
    Task<Result> DeleteAsync(string typeId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт копию объекта.
    ///
    /// Копирование — основной способ изменить системный объект: сам он остаётся
    /// неизменным, а пользователь правит собственную копию.
    /// </summary>
    /// <param name="typeId">Идентификатор вида контента.</param>
    /// <param name="id">Идентификатор копируемого объекта.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Созданная копия.</returns>
    Task<Result<EntityBase>> DuplicateAsync(
        string typeId,
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает перечень объектов вида для заполнения полей-ссылок.
    /// </summary>
    /// <param name="typeId">Идентификатор вида контента.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список пар «идентификатор — название».</returns>
    Task<IReadOnlyList<ContentReference>> GetReferencesAsync(
        string typeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает объекты вида, принадлежащие игровой системе или расширению,
    /// загруженные целиком — вместе с вложенными списками.
    ///
    /// Нужен выгрузке расширений: собрать всё, что относится к игровой системе,
    /// можно только зная, какие вообще виды объектов существуют, а это знает
    /// служба контента. Поэтому новый вид контента попадает в расширения сам,
    /// без единой правки (решение Р-103).
    /// </summary>
    /// <param name="typeId">Идентификатор вида контента.</param>
    /// <param name="owner">Владелец объектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Объекты владельца; пустой список, если вид ему не принадлежит.</returns>
    Task<IReadOnlyList<EntityBase>> GetOwnedAsync(
        string typeId,
        ContentOwner owner,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает количество объектов вида, принадлежащих владельцу.
    /// </summary>
    /// <param name="typeId">Идентификатор вида контента.</param>
    /// <param name="owner">Владелец объектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество объектов.</returns>
    Task<int> CountOwnedAsync(
        string typeId,
        ContentOwner owner,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет объекты вида, принадлежащие владельцу.
    /// </summary>
    /// <param name="typeId">Идентификатор вида контента.</param>
    /// <param name="owner">Владелец объектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество удалённых объектов.</returns>
    Task<int> DeleteOwnedAsync(
        string typeId,
        ContentOwner owner,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет объекты одного вида за одно обращение к базе данных.
    ///
    /// Установка расширения добавляет сотни объектов, и сохранение по одному
    /// означало бы сотни отдельных записей в базу.
    /// </summary>
    /// <param name="typeId">Идентификатор вида контента.</param>
    /// <param name="entities">Сохраняемые объекты.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат сохранения.</returns>
    Task<Result> SaveManyAsync(
        string typeId,
        IReadOnlyList<EntityBase> entities,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Пользовательские свойства игровых объектов.
///
/// Позволяют добавить любому объекту собственное поле без изменения структуры
/// базы данных — ключевая возможность, описанная в документе 004_База_данных.md.
/// </summary>
public interface ICustomPropertyService
{
    /// <summary>
    /// Возвращает описания пользовательских свойств указанного вида контента.
    /// </summary>
    /// <param name="targetType">Идентификатор вида контента.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список описаний свойств.</returns>
    Task<IReadOnlyList<PropertyDefinition>> GetDefinitionsAsync(
        string targetType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает значения пользовательских свойств объекта.
    /// </summary>
    /// <param name="objectId">Идентификатор объекта.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Значения, сопоставленные идентификатору описания свойства.</returns>
    Task<IReadOnlyDictionary<Guid, string?>> GetValuesAsync(
        Guid objectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет значения пользовательских свойств объекта.
    /// </summary>
    /// <param name="objectId">Идентификатор объекта.</param>
    /// <param name="values">Значения, сопоставленные идентификатору описания свойства.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    Task SaveValuesAsync(
        Guid objectId,
        IReadOnlyDictionary<Guid, string?> values,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт описание пользовательского свойства.
    /// </summary>
    /// <param name="definition">Описание свойства.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат сохранения.</returns>
    Task<Result> SaveDefinitionAsync(
        PropertyDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет описание пользовательского свойства вместе со всеми его значениями.
    /// </summary>
    /// <param name="definitionId">Идентификатор описания свойства.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><see langword="true"/>, если описание было найдено и удалено.</returns>
    Task<bool> DeleteDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default);
}
