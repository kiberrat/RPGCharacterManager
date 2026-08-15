using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Abstractions.Data;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Extensions;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Content;

/// <summary>
/// Управление игровым контентом всех зарегистрированных видов.
/// </summary>
public sealed class ContentService : IContentService
{
    private const string DuplicateSuffix = " — копия";

    private readonly Dictionary<string, IContentTypeDescriptor> _types;
    private readonly Dictionary<string, IContentStore> _stores;
    private readonly ILogger<ContentService> _logger;

    /// <summary>
    /// Создаёт службу контента.
    /// </summary>
    /// <param name="descriptors">Зарегистрированные описания видов контента.</param>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="logger">Журналировщик.</param>
    public ContentService(
        IEnumerable<IContentTypeDescriptor> descriptors,
        IDbContextFactory<RpgDbContext> contextFactory,
        ILogger<ContentService> logger)
    {
        Guard.NotNull(descriptors);
        Guard.NotNull(contextFactory);

        _logger = Guard.NotNull(logger);

        var ordered = descriptors
            .OrderBy(descriptor => descriptor.Order)
            .ThenBy(descriptor => descriptor.DisplayName, StringComparer.CurrentCulture)
            .ToList();

        Types = ordered;
        _types = ordered.ToDictionary(descriptor => descriptor.Id, StringComparer.Ordinal);
        _stores = ordered.ToDictionary(
            descriptor => descriptor.Id,
            descriptor => CreateStore(descriptor, contextFactory),
            StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public IReadOnlyList<IContentTypeDescriptor> Types { get; }

    /// <inheritdoc />
    public IContentTypeDescriptor? FindType(string typeId) =>
        !string.IsNullOrWhiteSpace(typeId) && _types.TryGetValue(typeId, out var descriptor)
            ? descriptor
            : null;

    /// <inheritdoc />
    public Task<PagedResult<ContentItem>> SearchAsync(
        string typeId,
        string? search,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        GetStore(typeId).SearchAsync(search, pageIndex, pageSize, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ContentItem>> GetItemsAsync(
        string typeId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(ids);

        return ids.Count == 0
            ? Task.FromResult<IReadOnlyList<ContentItem>>([])
            : GetStore(typeId).GetItemsAsync(ids, cancellationToken);
    }

    /// <inheritdoc />
    public Task<EntityBase?> GetAsync(string typeId, Guid id, CancellationToken cancellationToken = default) =>
        GetStore(typeId).GetAsync(id, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> SaveAsync(
        string typeId,
        EntityBase entity,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(entity);

        var descriptor = FindType(typeId);

        if (descriptor is null)
        {
            return Result.Failure($"Неизвестный вид контента «{typeId}».");
        }

        var name = descriptor.GetName(entity);

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure($"Не задано название: «{descriptor.SingularName}».");
        }

        // Системные объекты доступны только для чтения: изменение выполняется
        // созданием пользовательской копии, как требует документ 002_Архитектура.md.
        if (entity is ContentEntity { IsSystem: true })
        {
            return Result.Failure(
                "Системный объект нельзя изменить. Создайте копию и правьте её.");
        }

        FillSystemName(entity, name);

        var store = GetStore(typeId);

        if (await store.IsSystemNameTakenAsync(entity, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(
                "Объект с таким внутренним именем уже существует в этой игровой системе. " +
                "Измените название или внутреннее имя.");
        }

        try
        {
            await store.SaveAsync(entity, cancellationToken).ConfigureAwait(false);
            ContentLog.ContentSaved(_logger, descriptor.SingularName, name);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ContentLog.ContentSaveFailed(_logger, exception, descriptor.SingularName, name);
            return Result.Failure(DescribeSaveFailure(exception));
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(
        string typeId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var descriptor = FindType(typeId);

        if (descriptor is null)
        {
            return Result.Failure($"Неизвестный вид контента «{typeId}».");
        }

        try
        {
            var deleted = await GetStore(typeId).DeleteAsync(id, cancellationToken).ConfigureAwait(false);

            if (deleted)
            {
                ContentLog.ContentDeleted(_logger, descriptor.SingularName, id);
            }

            return deleted
                ? Result.Success()
                : Result.Failure("Объект не найден: возможно, он уже удалён.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ContentLog.ContentDeleteFailed(_logger, exception, descriptor.SingularName, id);

            return Result.Failure(
                "Не удалось удалить объект: на него ссылаются другие записи. " +
                "Сначала удалите или измените связанные объекты.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<EntityBase>> DuplicateAsync(
        string typeId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var descriptor = FindType(typeId);

        if (descriptor is null)
        {
            return Result.Failure<EntityBase>($"Неизвестный вид контента «{typeId}».");
        }

        var source = await GetAsync(typeId, id, cancellationToken).ConfigureAwait(false);

        if (source is null)
        {
            return Result.Failure<EntityBase>("Копируемый объект не найден.");
        }

        var copy = descriptor.CreateInstance();

        foreach (var field in descriptor.Fields)
        {
            field.CopyValue(source, copy);
        }

        foreach (var collection in descriptor.Collections)
        {
            collection.CopyItems(source, copy);
        }

        // Копия всегда принадлежит пользователю, даже если исходный объект системный.
        if (copy is ContentEntity content)
        {
            content.IsSystem = false;
        }

        descriptor.SetName(copy, descriptor.GetName(source) + DuplicateSuffix);
        FillSystemName(copy, descriptor.GetName(copy), forceUnique: true);

        return Result.Success(copy);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ContentReference>> GetReferencesAsync(
        string typeId,
        CancellationToken cancellationToken = default) =>
        GetStore(typeId).GetReferencesAsync(cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<EntityBase>> GetOwnedAsync(
        string typeId,
        ContentOwner owner,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(owner);

        return GetStore(typeId).GetOwnedAsync(owner, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountOwnedAsync(
        string typeId,
        ContentOwner owner,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(owner);

        return GetStore(typeId).CountOwnedAsync(owner, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> DeleteOwnedAsync(
        string typeId,
        ContentOwner owner,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(owner);

        return GetStore(typeId).DeleteOwnedAsync(owner, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> SaveManyAsync(
        string typeId,
        IReadOnlyList<EntityBase> entities,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(entities);

        try
        {
            await GetStore(typeId).SaveManyAsync(entities, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (DbUpdateException exception)
        {
            var descriptor = FindType(typeId);

            ContentLog.ContentSaveFailed(
                _logger, exception, descriptor?.DisplayName ?? typeId, $"{entities.Count} шт.");

            return Result.Failure(
                $"Не удалось сохранить объекты вида «{descriptor?.DisplayName ?? typeId}»: "
                + (exception.InnerException?.Message ?? exception.Message));
        }
    }

    /// <summary>
    /// Заполняет внутреннее имя объекта, если пользователь не задал его вручную.
    /// Внутреннее имя используется формулами и правилами.
    /// </summary>
    /// <param name="entity">Игровой объект.</param>
    /// <param name="name">Название объекта.</param>
    /// <param name="forceUnique">Добавить к имени признак уникальности.</param>
    private static void FillSystemName(EntityBase entity, string name, bool forceUnique = false)
    {
        if (entity is not ContentEntity content)
        {
            return;
        }

        if (forceUnique || string.IsNullOrWhiteSpace(content.SystemName))
        {
            var systemName = name.ToSystemName();

            content.SystemName = forceUnique
                ? $"{systemName}_{entity.Id.ToString("N")[..6]}"
                : systemName;
        }
    }

    /// <summary>
    /// Преобразует ошибку сохранения в понятное пользователю сообщение.
    /// </summary>
    /// <param name="exception">Возникшее исключение.</param>
    /// <returns>Текст сообщения.</returns>
    private static string DescribeSaveFailure(Exception exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;

        // Нарушение уникального индекса — самая частая ошибка при вводе контента.
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            ? "Объект с таким внутренним именем уже существует в этой игровой системе. " +
              "Измените название или внутреннее имя."
            : $"Не удалось сохранить объект: {message}";
    }

    private IContentStore GetStore(string typeId) =>
        !string.IsNullOrWhiteSpace(typeId) && _stores.TryGetValue(typeId, out var store)
            ? store
            : throw new InvalidOperationException($"Вид контента «{typeId}» не зарегистрирован.");

    /// <summary>
    /// Создаёт хранилище для описания вида контента.
    /// Тип сущности известен только во время выполнения, поэтому обобщённое
    /// хранилище создаётся по типу из описания.
    /// </summary>
    /// <param name="descriptor">Описание вида контента.</param>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <returns>Хранилище объектов вида.</returns>
    private static IContentStore CreateStore(
        IContentTypeDescriptor descriptor,
        IDbContextFactory<RpgDbContext> contextFactory)
    {
        var storeType = typeof(ContentStore<>).MakeGenericType(descriptor.EntityType);

        return (IContentStore)Activator.CreateInstance(storeType, contextFactory, descriptor)!;
    }
}

/// <summary>
/// Сообщения журнала подсистемы контента.
/// </summary>
internal static partial class ContentLog
{
    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Сохранён объект: {TypeName} «{Name}».")]
    public static partial void ContentSaved(ILogger logger, string typeName, string name);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Error, Message = "Не удалось сохранить объект: {TypeName} «{Name}».")]
    public static partial void ContentSaveFailed(ILogger logger, Exception exception, string typeName, string name);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Information, Message = "Удалён объект: {TypeName} {ObjectId}.")]
    public static partial void ContentDeleted(ILogger logger, string typeName, Guid objectId);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Error, Message = "Не удалось удалить объект: {TypeName} {ObjectId}.")]
    public static partial void ContentDeleteFailed(ILogger logger, Exception exception, string typeName, Guid objectId);

    [LoggerMessage(
        EventId = 5005,
        Level = LogLevel.Information,
        Message = "Сохранено пользовательское свойство «{Name}» для вида «{TargetType}».")]
    public static partial void PropertyDefinitionSaved(ILogger logger, string name, string targetType);

    [LoggerMessage(
        EventId = 5006,
        Level = LogLevel.Error,
        Message = "Не удалось сохранить пользовательское свойство «{Name}».")]
    public static partial void PropertyDefinitionSaveFailed(ILogger logger, Exception exception, string name);
}
