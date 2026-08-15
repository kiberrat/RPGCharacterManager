using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RPGCharacterManager.Core.Abstractions.Data;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Database.Repositories;

/// <summary>
/// Универсальная реализация хранилища сущностей поверх Entity Framework Core.
///
/// Каждая операция создаёт собственный контекст через фабрику и освобождает его.
/// Это исключает накопление отслеживаемых сущностей в долгоживущем контексте
/// и обеспечивает безопасность при обращении из нескольких потоков.
/// </summary>
/// <typeparam name="TEntity">Тип сущности.</typeparam>
public class Repository<TEntity> : IRepository<TEntity>
    where TEntity : EntityBase
{
    private readonly IDbContextFactory<RpgDbContext> _contextFactory;

    /// <summary>
    /// Создаёт хранилище сущностей.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    public Repository(IDbContextFactory<RpgDbContext> contextFactory) =>
        _contextFactory = Guard.NotNull(contextFactory);

    /// <inheritdoc />
    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.Set<TEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken).ConfigureAwait(false);

        return await ApplyFilter(context.Set<TEntity>().AsNoTracking(), predicate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PagedResult<TEntity>> GetPageAsync(
        int pageIndex,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        Guard.InRange(pageIndex, 0, int.MaxValue);
        Guard.InRange(pageSize, 1, int.MaxValue);

        await using var context = await CreateContextAsync(cancellationToken).ConfigureAwait(false);

        var query = ApplyFilter(context.Set<TEntity>().AsNoTracking(), predicate);
        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            // Устойчивый порядок обязателен: без сортировки SQLite не гарантирует
            // одинаковый состав страниц при повторных запросах.
            .OrderBy(entity => entity.CreatedAt)
            .ThenBy(entity => entity.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<TEntity>(items, totalCount, pageIndex, pageSize);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken).ConfigureAwait(false);

        return await ApplyFilter(context.Set<TEntity>().AsNoTracking(), predicate)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(entity);

        await using var context = await CreateContextAsync(cancellationToken).ConfigureAwait(false);

        context.Set<TEntity>().Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return entity;
    }

    /// <inheritdoc />
    public async Task<int> AddRangeAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(entities);

        await using var context = await CreateContextAsync(cancellationToken).ConfigureAwait(false);

        await context.Set<TEntity>().AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
        return await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(entity);

        await using var context = await CreateContextAsync(cancellationToken).ConfigureAwait(false);

        context.Set<TEntity>().Update(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.Set<TEntity>()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return false;
        }

        context.Set<TEntity>().Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Создаёт контекст базы данных для одной операции.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Новый контекст базы данных.</returns>
    protected Task<RpgDbContext> CreateContextAsync(CancellationToken cancellationToken) =>
        _contextFactory.CreateDbContextAsync(cancellationToken);

    private static IQueryable<TEntity> ApplyFilter(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, bool>>? predicate) =>
        predicate is null ? query : query.Where(predicate);
}
