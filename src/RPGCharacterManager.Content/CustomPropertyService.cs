using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Extensions;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Content;

/// <summary>
/// Пользовательские свойства игровых объектов.
///
/// Позволяют добавить любому виду контента собственное поле без изменения структуры
/// базы данных: описание свойства хранится в таблице описаний, а значение — в таблице
/// значений, связанной с объектом по идентификатору.
/// </summary>
public sealed class CustomPropertyService : ICustomPropertyService
{
    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly ILogger<CustomPropertyService> _logger;

    /// <summary>
    /// Создаёт службу пользовательских свойств.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="logger">Журналировщик.</param>
    public CustomPropertyService(
        IDbContextFactory<RpgDbContext> contextFactory,
        ILogger<CustomPropertyService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PropertyDefinition>> GetDefinitionsAsync(
        string targetType,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(targetType);

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.PropertyDefinitions
            .AsNoTracking()
            .Where(definition => definition.TargetType == targetType)
            .OrderBy(definition => definition.SortOrder)
            .ThenBy(definition => definition.DisplayName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string?>> GetValuesAsync(
        Guid objectId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var values = await context.PropertyValues
            .AsNoTracking()
            .Where(value => value.ObjectId == objectId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return values.ToDictionary(value => value.PropertyDefinitionId, value => value.Value);
    }

    /// <inheritdoc />
    public async Task SaveValuesAsync(
        Guid objectId,
        IReadOnlyDictionary<Guid, string?> values,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(values);

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var existing = await context.PropertyValues
            .Where(value => value.ObjectId == objectId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var pair in values)
        {
            var current = existing.Find(value => value.PropertyDefinitionId == pair.Key);

            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                // Пустое значение не хранится: свойство просто не задано у объекта.
                if (current is not null)
                {
                    context.PropertyValues.Remove(current);
                }

                continue;
            }

            if (current is null)
            {
                context.PropertyValues.Add(new PropertyValue
                {
                    ObjectId = objectId,
                    PropertyDefinitionId = pair.Key,
                    Value = pair.Value,
                });
            }
            else
            {
                current.Value = pair.Value;
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result> SaveDefinitionAsync(
        PropertyDefinition definition,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(definition);

        if (string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            return Result.Failure("Не задано название свойства.");
        }

        if (string.IsNullOrWhiteSpace(definition.TargetType))
        {
            return Result.Failure("Не указан вид контента, к которому относится свойство.");
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            definition.Name = definition.DisplayName;
        }

        if (string.IsNullOrWhiteSpace(definition.SystemName))
        {
            definition.SystemName = definition.DisplayName.ToSystemName();
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var existing = await context.PropertyDefinitions
                .FirstOrDefaultAsync(item => item.Id == definition.Id, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                context.PropertyDefinitions.Add(definition);
            }
            else
            {
                existing.Name = definition.Name;
                existing.SystemName = definition.SystemName;
                existing.DisplayName = definition.DisplayName;
                existing.Description = definition.Description;
                existing.TargetType = definition.TargetType;
                existing.DataType = definition.DataType;
                existing.DefaultValue = definition.DefaultValue;
                existing.Category = definition.Category;
                existing.SortOrder = definition.SortOrder;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            ContentLog.PropertyDefinitionSaved(_logger, definition.DisplayName, definition.TargetType);
            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ContentLog.PropertyDefinitionSaveFailed(_logger, exception, definition.DisplayName);
            return Result.Failure($"Не удалось сохранить свойство: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var definition = await context.PropertyDefinitions
            .FirstOrDefaultAsync(item => item.Id == definitionId, cancellationToken)
            .ConfigureAwait(false);

        if (definition is null)
        {
            return false;
        }

        // Значения удаляются вместе с описанием: связь настроена каскадным удалением,
        // поэтому объекты, использовавшие свойство, остаются целыми.
        context.PropertyDefinitions.Remove(definition);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
