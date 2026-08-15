using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Data;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Database;

/// <summary>
/// Обслуживание базы данных SQLite.
/// </summary>
public sealed class SqliteDatabaseService : IDatabaseService
{
    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly IAppPathService _paths;
    private readonly ILogger<SqliteDatabaseService> _logger;

    /// <summary>
    /// Создаёт службу обслуживания базы данных.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="paths">Служба путей пользовательских данных.</param>
    /// <param name="logger">Журналировщик.</param>
    public SqliteDatabaseService(
        IDbContextFactory<RpgDbContext> contextFactory,
        IAppPathService paths,
        ILogger<SqliteDatabaseService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _paths = Guard.NotNull(paths);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _paths.EnsureDirectoriesExist();

            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            // Миграции создают файл базы данных при первом запуске и приводят схему
            // к актуальному состоянию. Ручное изменение схемы запрещено документом 004.
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            DatabaseLog.DatabaseReady(_logger, _paths.DatabaseFilePath);
            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            DatabaseLog.DatabaseInitializationFailed(_logger, exception, _paths.DatabaseFilePath);
            return Result.Failure($"Не удалось подготовить базу данных: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            return await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            DatabaseLog.ConnectionCheckFailed(_logger, exception);
            return false;
        }
    }
}
