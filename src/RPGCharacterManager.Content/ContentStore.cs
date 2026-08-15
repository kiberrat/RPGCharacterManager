using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Abstractions.Data;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Content;

/// <summary>
/// Хранилище объектов одного вида контента.
/// Скрывает конкретный тип сущности, позволяя службе контента работать
/// с любым видом объектов единообразно.
/// </summary>
internal interface IContentStore
{
    /// <summary>
    /// Возвращает страницу объектов, отфильтрованных по названию.
    /// </summary>
    /// <param name="search">Строка поиска.</param>
    /// <param name="pageIndex">Номер страницы.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница объектов.</returns>
    Task<PagedResult<ContentItem>> SearchAsync(
        string? search,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает строки списка для объектов с указанными идентификаторами.
    /// </summary>
    /// <param name="ids">Идентификаторы объектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Найденные объекты.</returns>
    Task<IReadOnlyList<ContentItem>> GetItemsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);

    /// <summary>
    /// Загружает объект вместе со связанными данными.
    /// </summary>
    /// <param name="id">Идентификатор объекта.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Объект или <see langword="null"/>.</returns>
    Task<EntityBase?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет объект.
    /// </summary>
    /// <param name="entity">Сохраняемый объект.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    Task SaveAsync(EntityBase entity, CancellationToken cancellationToken);

    /// <summary>
    /// Удаляет объект.
    /// </summary>
    /// <param name="id">Идентификатор объекта.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><see langword="true"/>, если объект был найден и удалён.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает перечень объектов для полей-ссылок.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список пар «идентификатор — название».</returns>
    Task<IReadOnlyList<ContentReference>> GetReferencesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Проверяет, занято ли внутреннее имя другим объектом этого же вида
    /// в пределах той же игровой системы.
    /// </summary>
    /// <param name="entity">Проверяемый объект.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><see langword="true"/>, если имя уже используется.</returns>
    Task<bool> IsSystemNameTakenAsync(EntityBase entity, CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает объекты владельца целиком, вместе с вложенными списками.
    /// </summary>
    /// <param name="owner">Владелец объектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Объекты владельца.</returns>
    Task<IReadOnlyList<EntityBase>> GetOwnedAsync(ContentOwner owner, CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает количество объектов владельца.
    /// </summary>
    /// <param name="owner">Владелец объектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество объектов.</returns>
    Task<int> CountOwnedAsync(ContentOwner owner, CancellationToken cancellationToken);

    /// <summary>
    /// Удаляет объекты владельца.
    /// </summary>
    /// <param name="owner">Владелец объектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество удалённых объектов.</returns>
    Task<int> DeleteOwnedAsync(ContentOwner owner, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет объекты вида за одно обращение к базе данных.
    /// </summary>
    /// <param name="entities">Сохраняемые объекты.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    Task SaveManyAsync(IReadOnlyList<EntityBase> entities, CancellationToken cancellationToken);
}

/// <summary>
/// Хранилище объектов вида контента поверх Entity Framework Core.
/// </summary>
/// <typeparam name="TEntity">Тип сущности базы данных.</typeparam>
internal sealed class ContentStore<TEntity> : IContentStore
    where TEntity : EntityBase, new()
{
    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly ContentTypeDescriptor<TEntity> _descriptor;

    /// <summary>
    /// Создаёт хранилище вида контента.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="descriptor">Описание вида контента.</param>
    public ContentStore(
        IDbContextFactory<RpgDbContext> contextFactory,
        ContentTypeDescriptor<TEntity> descriptor)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _descriptor = Guard.NotNull(descriptor);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ContentItem>> SearchAsync(
        string? search,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = ApplyFilter(context.Set<TEntity>().AsNoTracking());
        query = ApplySearch(context, query, search);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Выбираются только отображаемые поля: список рассчитан на сотни тысяч записей,
        // загружать объекты целиком для его отрисовки недопустимо.
        var entities = await query
            .OrderBy(entity => EF.Property<string>(entity, nameof(ContentEntity.Name)))
            .ThenBy(entity => entity.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = entities.Select(ToContentItem).ToList();

        return new PagedResult<ContentItem>(items, totalCount, pageIndex, pageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentItem>> GetItemsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        // Список идентификаторов переносится в запрос целиком, поэтому названия
        // всех участников состава кампании читаются одним обращением к базе.
        var keys = ids.ToList();

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entities = await ApplyFilter(context.Set<TEntity>().AsNoTracking())
            .Where(entity => keys.Contains(entity.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToContentItem).ToList();
    }

    /// <inheritdoc />
    public async Task<EntityBase?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = context.Set<TEntity>().AsNoTracking();

        if (_descriptor.Include is not null)
        {
            query = _descriptor.Include(query);
        }

        return await query
            .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAsync(EntityBase entity, CancellationToken cancellationToken)
    {
        var typed = (TEntity)entity;

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = context.Set<TEntity>().AsQueryable();

        if (_descriptor.Include is not null)
        {
            query = _descriptor.Include(query);
        }

        var existing = await query
            .FirstOrDefaultAsync(item => item.Id == typed.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            context.Set<TEntity>().Add(typed);
        }
        else
        {
            // Значения переносятся на отслеживаемый объект по описанию полей.
            // Такой способ корректно обрабатывает связанные записи: созданные
            // при редактировании дочерние объекты добавляются, а не помечаются
            // изменёнными, как это произошло бы при подключении отсоединённого графа.
            foreach (var field in _descriptor.Fields)
            {
                field.CopyValue(typed, existing);
            }

            foreach (var collection in _descriptor.Collections)
            {
                Reconcile(collection, typed, existing, context);
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

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

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentReference>> GetReferencesAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entities = await ApplyFilter(context.Set<TEntity>().AsNoTracking())
            .OrderBy(entity => EF.Property<string>(entity, nameof(ContentEntity.Name)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities
            .Select(entity => new ContentReference(entity.Id, _descriptor.GetName(entity)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<bool> IsSystemNameTakenAsync(EntityBase entity, CancellationToken cancellationToken)
    {
        // Проверка применима только к игровым объектам: у игровых систем и контент-паков
        // уникальность обеспечивается собственным индексом по непустому полю.
        if (entity is not ContentEntity content || string.IsNullOrWhiteSpace(content.SystemName))
        {
            return false;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Уникальный индекс базы данных не срабатывает, когда игровая система не задана:
        // в SQL значения NULL считаются различными. Поэтому совпадение внутреннего имени
        // проверяется явно — иначе формулы и правила ссылались бы на объект неоднозначно.
        return await context.Set<TEntity>()
            .AsNoTracking()
            .AnyAsync(
                item => item.Id != content.Id
                    && EF.Property<string>(item, nameof(ContentEntity.SystemName)) == content.SystemName
                    && EF.Property<Guid?>(item, nameof(ContentEntity.GameSystemId)) == content.GameSystemId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EntityBase>> GetOwnedAsync(
        ContentOwner owner,
        CancellationToken cancellationToken)
    {
        if (OwnedQuery(owner) is not { } prepared)
        {
            return [];
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = prepared(context.Set<TEntity>().AsNoTracking());

        if (_descriptor.Include is not null)
        {
            query = _descriptor.Include(query);
        }

        var entities = await query
            .OrderBy(entity => EF.Property<string>(entity, nameof(ContentEntity.Name)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Cast<EntityBase>().ToList();
    }

    /// <inheritdoc />
    public async Task<int> CountOwnedAsync(ContentOwner owner, CancellationToken cancellationToken)
    {
        if (OwnedQuery(owner) is not { } prepared)
        {
            return 0;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await prepared(context.Set<TEntity>().AsNoTracking())
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteOwnedAsync(ContentOwner owner, CancellationToken cancellationToken)
    {
        if (OwnedQuery(owner) is not { } prepared)
        {
            return 0;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Удаление выполняется запросом, а не загрузкой объектов: расширение
        // может содержать сотни тысяч записей, и поднимать их в память ради
        // удаления незачем. Вложенные записи убирает каскад базы данных.
        return await prepared(context.Set<TEntity>())
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveManyAsync(IReadOnlyList<EntityBase> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var keys = entities.Select(entity => entity.Id).ToList();

        var stored = await context.Set<TEntity>()
            .Where(entity => keys.Contains(entity.Id))
            .Select(entity => entity.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Уже существующие объекты пропускаются: список приходит от установки
        // расширения, которая перед записью убирает прежнее содержимое, поэтому
        // совпадение означает чужой объект с тем же идентификатором.
        foreach (var entity in entities.Where(entity => !stored.Contains(entity.Id)))
        {
            context.Add(entity);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Готовит отбор объектов владельца.
    /// </summary>
    /// <param name="owner">Владелец объектов.</param>
    /// <returns>Отбор либо <see langword="null"/>, если вид владельцу не принадлежит.</returns>
    private Func<IQueryable<TEntity>, IQueryable<TEntity>>? OwnedQuery(ContentOwner owner)
    {
        // Владельца имеют только игровые объекты: игровые системы и расширения
        // сами себе владельцы и в чужой набор не входят.
        if (!owner.IsSpecified || !typeof(ContentEntity).IsAssignableFrom(typeof(TEntity)))
        {
            return null;
        }

        return query =>
        {
            query = ApplyFilter(query);

            if (owner.GameSystemId is { } gameSystemId)
            {
                query = query.Where(entity =>
                    EF.Property<Guid?>(entity, nameof(ContentEntity.GameSystemId)) == gameSystemId);
            }

            if (owner.ContentPackId is { } contentPackId)
            {
                query = query.Where(entity =>
                    EF.Property<Guid?>(entity, nameof(ContentEntity.ContentPackId)) == contentPackId);
            }

            return query;
        };
    }

    /// <summary>
    /// Приводит список вложенных записей отслеживаемого объекта в соответствие
    /// с изменённым: добавляет новые записи, обновляет изменённые и удаляет убранные.
    /// </summary>
    /// <param name="collection">Описание списка.</param>
    /// <param name="source">Изменённый объект.</param>
    /// <param name="destination">Отслеживаемый объект.</param>
    /// <param name="context">Контекст базы данных.</param>
    private static void Reconcile(
        IContentList collection,
        TEntity source,
        TEntity destination,
        RpgDbContext context)
    {
        var stored = collection.GetItems(destination).Cast<EntityBase>().ToDictionary(item => item.Id);
        var edited = collection.GetItems(source).Cast<EntityBase>().ToList();

        foreach (var item in edited)
        {
            if (stored.Remove(item.Id, out var existing))
            {
                foreach (var field in collection.Fields)
                {
                    field.CopyValue(item, existing);
                }

                continue;
            }

            // Новая запись создаётся у отслеживаемого объекта, а не подключается
            // отсоединённой: её идентификатор задан в коде, и подключение графа
            // приняло бы её за изменение существующей строки (решение Р-28).
            var added = collection.AddItem(destination);

            foreach (var field in collection.Fields)
            {
                field.CopyValue(item, added);
            }

            // Того, что запись появилась в списке отслеживаемого объекта, мало:
            // её ключ заполнен конструктором, и по этому признаку EF Core считает
            // запись уже существующей и готовит UPDATE вместо INSERT. Обновлять
            // нечего — строки нет, — и сохранение обрывается ошибкой. Явное
            // добавление задаёт состояние однозначно (решение Р-28).
            context.Add(added);
        }

        foreach (var removed in stored.Values)
        {
            collection.RemoveItem(destination, removed);
            context.Remove(removed);
        }
    }

    private IQueryable<TEntity> ApplyFilter(IQueryable<TEntity> query) =>
        _descriptor.Filter is null ? query : query.Where(_descriptor.Filter);

    private IQueryable<TEntity> ApplySearch(
        RpgDbContext context,
        IQueryable<TEntity> query,
        string? search)
    {
        if (typeof(ContentEntity).IsAssignableFrom(typeof(TEntity)))
        {
            return ((IQueryable<ContentEntity>)query).WhereNameOrAlias(context, _descriptor.Id, search)
                .Cast<TEntity>();
        }

        return query;
    }

    private ContentItem ToContentItem(TEntity entity) => new(
        entity.Id,
        _descriptor.GetName(entity),
        entity is ContentEntity content ? content.Description : null,
        entity is ContentEntity { IsSystem: true });

    /// <summary>
    /// Возвращает выражение отбора объектов вида. Используется тестами и диагностикой.
    /// </summary>
    /// <returns>Условие отбора или <see langword="null"/>.</returns>
    public Expression<Func<TEntity, bool>>? GetFilter() => _descriptor.Filter;
}
