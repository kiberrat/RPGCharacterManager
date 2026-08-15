using Microsoft.EntityFrameworkCore;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Database;

/// <summary>Общие SQL-запросы многоязычного поиска игрового контента.</summary>
public static class ContentAliasQueries
{
    /// <summary>Оставляет объекты, найденные по основному или дополнительному имени.</summary>
    public static IQueryable<TEntity> WhereNameOrAlias<TEntity>(
        this IQueryable<TEntity> query,
        RpgDbContext context,
        string contentTypeId,
        string? search)
        where TEntity : ContentEntity
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var pattern = $"%{search.Trim()}%";

        return query.Where(entity =>
            EF.Functions.Like(entity.Name, pattern)
            || context.ContentAliases.Any(alias =>
                alias.ContentTypeId == contentTypeId
                && alias.TargetSystemName == entity.SystemName
                && (alias.GameSystemId == null || alias.GameSystemId == entity.GameSystemId)
                && alias.ContentPack != null
                && alias.ContentPack.Enabled
                && EF.Functions.Like(alias.Alias, pattern)));
    }

    /// <summary>Возвращает внутренние имена объектов, совпавших с псевдонимом.</summary>
    public static async Task<HashSet<string>> FindAliasTargetsAsync(
        this RpgDbContext context,
        string contentTypeId,
        Guid? gameSystemId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return [];
        }

        var pattern = $"%{search.Trim()}%";
        var names = await context.ContentAliases
            .AsNoTracking()
            .Where(alias => alias.ContentTypeId == contentTypeId)
            .Where(alias => alias.GameSystemId == null || alias.GameSystemId == gameSystemId)
            .Where(alias => alias.ContentPack != null && alias.ContentPack.Enabled)
            .Where(alias => EF.Functions.Like(alias.Alias, pattern))
            .Select(alias => alias.TargetSystemName)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return names.ToHashSet(StringComparer.Ordinal);
    }
}
