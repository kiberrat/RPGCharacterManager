using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Data;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Database.Backup;

/// <summary>
/// Резервное копирование и восстановление базы данных SQLite.
///
/// Копирование выполняется штатной командой SQLite <c>VACUUM INTO</c>, а не копированием
/// файла: она работает при открытых соединениях, учитывает незавершённые записи журнала
/// WAL и создаёт файл без внутренней фрагментации.
/// </summary>
public sealed class SqliteBackupService : IBackupService
{
    private const string BackupFilePrefix = "rpgmanager";
    private const string BackupFileExtension = ".db";
    private const string TimestampFormat = "yyyyMMdd-HHmmss";

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly IAppPathService _paths;
    private readonly ILogger<SqliteBackupService> _logger;

    /// <summary>
    /// Создаёт службу резервного копирования.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="paths">Служба путей пользовательских данных.</param>
    /// <param name="logger">Журналировщик.</param>
    public SqliteBackupService(
        IDbContextFactory<RpgDbContext> contextFactory,
        IAppPathService paths,
        ILogger<SqliteBackupService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _paths = Guard.NotNull(paths);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<BackupRecord>> CreateBackupAsync(
        string? comment = null,
        bool isAutomatic = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(_paths.BackupsDirectory);

            var backupPath = BuildBackupFilePath();

            await using (var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
            {
                // Путь подставляется параметром: имя файла может содержать
                // произвольные символы, а строковая склейка допускала бы внедрение SQL.
                await context.Database
                    .ExecuteSqlRawAsync(
                        "VACUUM INTO {0}",
                        [backupPath],
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var record = new BackupRecord
            {
                FilePath = backupPath,
                SizeInBytes = new FileInfo(backupPath).Length,
                IsAutomatic = isAutomatic,
                Comment = comment,
            };

            await using (var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
            {
                context.Backups.Add(record);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            DatabaseLog.BackupCreated(_logger, backupPath, record.SizeInBytes);
            return Result.Success(record);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            DatabaseLog.BackupFailed(_logger, exception);
            return Result.Failure<BackupRecord>($"Не удалось создать резервную копию: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var records = await context.Backups
            .AsNoTracking()
            .OrderByDescending(record => record.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Копии, удалённые из файловой системы вручную, не показываются пользователю.
        return records.Where(record => File.Exists(record.FilePath)).ToList();
    }

    /// <inheritdoc />
    public async Task<Result> RestoreAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(backupFilePath);

        if (!File.Exists(backupFilePath))
        {
            return Result.Failure($"Файл резервной копии не найден: {backupFilePath}");
        }

        try
        {
            // Состояние до восстановления сохраняется, чтобы операция была обратимой.
            var safetyCopy = await CreateBackupAsync(
                    "Автоматическая копия перед восстановлением",
                    isAutomatic: true,
                    cancellationToken)
                .ConfigureAwait(false);

            if (safetyCopy.IsFailure)
            {
                return Result.Failure(
                    $"Восстановление отменено: не удалось сохранить текущее состояние. {safetyCopy.Error}");
            }

            // Все соединения должны быть закрыты, иначе файл базы данных занят.
            SqliteConnection.ClearAllPools();

            var targetPath = _paths.DatabaseFilePath;
            RemoveWriteAheadLogFiles(targetPath);

            File.Copy(backupFilePath, targetPath, overwrite: true);

            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            // Восстановленная база может быть создана предыдущей версией приложения.
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            DatabaseLog.BackupRestored(_logger, backupFilePath);
            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            DatabaseLog.RestoreFailed(_logger, exception, backupFilePath);
            return Result.Failure($"Не удалось восстановить базу данных: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<int> RemoveObsoleteBackupsAsync(
        TimeSpan retentionPeriod,
        CancellationToken cancellationToken = default)
    {
        var threshold = DateTimeOffset.UtcNow - retentionPeriod;

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var obsolete = await context.Backups
            .Where(record => record.CreatedAt < threshold)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var removed = 0;

        foreach (var record in obsolete)
        {
            try
            {
                if (File.Exists(record.FilePath))
                {
                    File.Delete(record.FilePath);
                }

                context.Backups.Remove(record);
                removed++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Занятый файл будет удалён при следующей очистке.
                DatabaseLog.BackupDeleteFailed(_logger, exception, record.FilePath);
            }
        }

        if (removed > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return removed;
    }

    private string BuildBackupFilePath()
    {
        var timestamp = DateTimeOffset.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var path = Path.Combine(
            _paths.BackupsDirectory,
            $"{BackupFilePrefix}-{timestamp}{BackupFileExtension}");

        // Две копии, созданные в одну секунду, не должны перезаписывать друг друга.
        var attempt = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(
                _paths.BackupsDirectory,
                $"{BackupFilePrefix}-{timestamp}-{attempt.ToString(CultureInfo.InvariantCulture)}{BackupFileExtension}");
            attempt++;
        }

        return path;
    }

    /// <summary>
    /// Удаляет вспомогательные файлы журнала упреждающей записи.
    /// Без этого SQLite может применить к восстановленной базе незавершённые
    /// транзакции предыдущей.
    /// </summary>
    /// <param name="databasePath">Путь к файлу базы данных.</param>
    private static void RemoveWriteAheadLogFiles(string databasePath)
    {
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var path = databasePath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
