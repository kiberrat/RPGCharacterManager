using Microsoft.EntityFrameworkCore;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Characters;

/// <summary>
/// Условия отбора объектов для шага мастера.
/// </summary>
/// <param name="GameSystemId">Выбранная игровая система.</param>
/// <param name="UseAllSources">Разрешены объекты всех источников.</param>
/// <param name="SourceIds">Разрешённые контент-паки.</param>
/// <param name="ParentPropertyName">Свойство, связывающее объект с выбором родительского шага.</param>
/// <param name="ParentId">Выбор родительского шага.</param>
/// <param name="Search">Строка поиска по названию.</param>
/// <param name="Limit">Наибольшее количество загружаемых объектов.</param>
internal sealed record ContentOptionQuery(
    Guid? GameSystemId,
    bool UseAllSources,
    IReadOnlyCollection<Guid> SourceIds,
    string? ParentPropertyName,
    Guid? ParentId,
    string? Search,
    int Limit);

/// <summary>
/// Загрузка объектов игрового контента для шага мастера.
/// Скрывает конкретный тип сущности, позволяя мастеру работать с любым видом объектов.
/// </summary>
internal interface IContentOptionSource
{
    /// <summary>
    /// Загружает объекты, удовлетворяющие условиям отбора.
    /// </summary>
    /// <param name="query">Условия отбора.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Объекты и общее количество подходящих записей.</returns>
    Task<(IReadOnlyList<ContentEntity> Items, int TotalCount)> LoadAsync(
        ContentOptionQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Загружает объекты по идентификаторам без учёта условий отбора.
    /// Применяется для отображения ранее сделанного выбора и требуемых объектов.
    /// </summary>
    /// <param name="ids">Идентификаторы объектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Найденные объекты.</returns>
    Task<IReadOnlyList<ContentEntity>> LoadByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);
}

/// <summary>
/// Загрузка объектов одного вида контента поверх Entity Framework Core.
/// </summary>
/// <typeparam name="TEntity">Тип сущности игрового объекта.</typeparam>
internal sealed class ContentOptionSource<TEntity> : IContentOptionSource
    where TEntity : ContentEntity
{
    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly IReadOnlyList<string> _includePaths;
    private readonly string _contentTypeId;

    /// <summary>
    /// Создаёт источник объектов вида контента.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="includePaths">Связанные данные, загружаемые вместе с объектами.</param>
    public ContentOptionSource(
        IDbContextFactory<RpgDbContext> contextFactory,
        IReadOnlyList<string> includePaths,
        string contentTypeId)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _includePaths = Guard.NotNull(includePaths);
        _contentTypeId = Guard.NotNullOrWhiteSpace(contentTypeId);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<ContentEntity> Items, int TotalCount)> LoadAsync(
        ContentOptionQuery query,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var filtered = ApplyFilters(context, context.Set<TEntity>().AsNoTracking(), query);

        var totalCount = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await ApplyIncludes(filtered)
            .OrderBy(entity => entity.Name)
            .ThenBy(entity => entity.Id)
            .Take(query.Limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentEntity>> LoadByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await ApplyIncludes(context.Set<TEntity>().AsNoTracking())
            .Where(entity => ids.Contains(entity.Id))
            .OrderBy(entity => entity.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Применяет отбор по игровой системе, источникам, родительскому выбору и поиску.
    /// </summary>
    /// <param name="query">Исходный запрос.</param>
    /// <param name="options">Условия отбора.</param>
    /// <returns>Запрос с наложенными условиями.</returns>
    private IQueryable<TEntity> ApplyFilters(
        RpgDbContext context, IQueryable<TEntity> query, ContentOptionQuery options)
    {
        // Объекты, не привязанные к игровой системе, доступны любой системе:
        // пользователь может создавать контент, не выбирая систему заранее.
        query = query.Where(entity =>
            entity.GameSystemId == null || entity.GameSystemId == options.GameSystemId);

        if (!options.UseAllSources)
        {
            var sources = options.SourceIds.ToList();

            query = query.Where(entity =>
                entity.ContentPackId == null || sources.Contains(entity.ContentPackId.Value));
        }

        if (options.ParentPropertyName is { } parentProperty)
        {
            var parentId = options.ParentId;

            query = query.Where(entity => EF.Property<Guid?>(entity, parentProperty) == parentId);
        }

        query = query.WhereNameOrAlias(context, _contentTypeId, options.Search);
        return query;
    }

    private IQueryable<TEntity> ApplyIncludes(IQueryable<TEntity> query)
    {
        foreach (var path in _includePaths)
        {
            query = query.Include(path);
        }

        return query;
    }
}

/// <summary>
/// Создание источников объектов по описанию шага.
/// </summary>
internal static class ContentOptionSourceFactory
{
    /// <summary>
    /// Создаёт источник объектов для шага мастера.
    /// Тип сущности известен только во время выполнения, поэтому обобщённый
    /// источник создаётся по типу из описания шага.
    /// </summary>
    /// <param name="step">Описание шага.</param>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <returns>Источник объектов либо <see langword="null"/>, если шаг не выбирает объекты.</returns>
    public static IContentOptionSource? Create(
        CharacterStepDefinition step,
        IDbContextFactory<RpgDbContext> contextFactory)
    {
        if (step.OptionEntityType is null)
        {
            return null;
        }

        if (!typeof(ContentEntity).IsAssignableFrom(step.OptionEntityType))
        {
            throw new InvalidOperationException(
                $"Шаг «{step.Title}» указывает тип {step.OptionEntityType.Name}, "
                + $"который не наследует {nameof(ContentEntity)}.");
        }

        var sourceType = typeof(ContentOptionSource<>).MakeGenericType(step.OptionEntityType);

        return (IContentOptionSource)Activator.CreateInstance(
            sourceType,
            contextFactory,
            step.IncludePaths,
            ResolveTypeId(step.OptionEntityType))!;
    }

    private static string ResolveTypeId(Type entityType)
    {
        if (entityType == typeof(AttributeDefinition)) return ContentTypeIds.Attributes;
        if (entityType == typeof(Skill)) return ContentTypeIds.Skills;
        if (entityType == typeof(Race)) return ContentTypeIds.Races;
        if (entityType == typeof(Background)) return ContentTypeIds.Backgrounds;
        if (entityType == typeof(CharacterClass)) return ContentTypeIds.Classes;
        if (entityType == typeof(Subclass)) return ContentTypeIds.Subclasses;
        if (entityType == typeof(Trait)) return ContentTypeIds.Traits;
        if (entityType == typeof(Spell)) return ContentTypeIds.Spells;
        if (entityType == typeof(GameResource)) return ContentTypeIds.Resources;

        throw new InvalidOperationException(
            $"Для типа {entityType.Name} не задан идентификатор контента для поиска.");
    }
}
