using System.Linq.Expressions;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Core.Abstractions.Data;

/// <summary>
/// Постраничный результат выборки.
/// </summary>
/// <typeparam name="TItem">Тип элементов страницы.</typeparam>
/// <param name="Items">Элементы текущей страницы.</param>
/// <param name="TotalCount">Общее количество записей, удовлетворяющих условию.</param>
/// <param name="PageIndex">Номер страницы, начиная с нуля.</param>
/// <param name="PageSize">Размер страницы.</param>
public sealed record PagedResult<TItem>(
    IReadOnlyList<TItem> Items,
    int TotalCount,
    int PageIndex,
    int PageSize);

/// <summary>
/// Хранилище сущностей одного типа.
///
/// Модели представления обращаются к данным только через репозитории и никогда
/// не выполняют запросы напрямую — требование раздела «MVVM» документа 002.
/// </summary>
/// <typeparam name="TEntity">Тип сущности.</typeparam>
public interface IRepository<TEntity>
    where TEntity : EntityBase
{
    /// <summary>
    /// Возвращает сущность по идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор сущности.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Найденная сущность или <see langword="null"/>.</returns>
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает все сущности, удовлетворяющие условию.
    /// </summary>
    /// <param name="predicate">Условие отбора. Отсутствие условия означает выбор всех записей.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список найденных сущностей.</returns>
    Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает страницу сущностей.
    /// Постраничная выборка обязательна для списков, рассчитанных на сотни тысяч записей.
    /// </summary>
    /// <param name="pageIndex">Номер страницы, начиная с нуля.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="predicate">Условие отбора.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница результатов и общее количество записей.</returns>
    Task<PagedResult<TEntity>> GetPageAsync(
        int pageIndex,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает количество записей, удовлетворяющих условию.
    /// </summary>
    /// <param name="predicate">Условие отбора.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество записей.</returns>
    Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет сущность.
    /// </summary>
    /// <param name="entity">Добавляемая сущность.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Добавленная сущность.</returns>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет несколько сущностей одной операцией.
    /// </summary>
    /// <param name="entities">Добавляемые сущности.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество добавленных записей.</returns>
    Task<int> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет изменения сущности.
    /// </summary>
    /// <param name="entity">Изменённая сущность.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет сущность по идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор удаляемой сущности.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><see langword="true"/>, если запись была найдена и удалена.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
