using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Database;

namespace RPGCharacterManager.Tests.Support;

/// <summary>
/// Служба путей, указывающая на временный каталог теста.
/// </summary>
internal sealed class TestPathService : IAppPathService
{
    public TestPathService(string root)
    {
        DataDirectory = root;
        LogsDirectory = Path.Combine(root, "logs");
        BackupsDirectory = Path.Combine(root, "backups");
        ContentDirectory = Path.Combine(root, "content");
        DatabaseFilePath = Path.Combine(root, "test.db");
        SettingsFilePath = Path.Combine(root, "settings.json");
    }

    public string DataDirectory { get; }

    public string LogsDirectory { get; }

    public string BackupsDirectory { get; }

    public string ContentDirectory { get; }

    public string DatabaseFilePath { get; }

    public string SettingsFilePath { get; }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(ContentDirectory);
    }
}

/// <summary>
/// Фабрика контекстов, работающая с файлом базы данных теста.
/// </summary>
internal sealed class TestContextFactory : IDbContextFactory<RpgDbContext>
{
    private readonly string _connectionString;

    public TestContextFactory(string databasePath) =>
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

    public RpgDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RpgDbContext>()
            // Объект с несколькими списками читается несколькими запросами —
            // так же, как в работающем приложении (решение Р-104).
            .UseSqlite(_connectionString, sqlite => sqlite
                .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            // Приложение заменяет встроенный LIKE своей функцией; без неё
            // проверки отбора шли бы не по тем правилам, что настоящая работа.
            .AddInterceptors(new UnicodeLikeInterceptor())
            .Options);
}

/// <summary>
/// Временная база данных теста: создаёт файл во временном каталоге, применяет
/// миграции и удаляет каталог по завершении.
/// </summary>
internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _root;

    private TestDatabase(string root, TestPathService paths, IDbContextFactory<RpgDbContext> contextFactory)
    {
        _root = root;
        Paths = paths;
        ContextFactory = contextFactory;
    }

    /// <summary>Пути временного каталога.</summary>
    public TestPathService Paths { get; }

    /// <summary>Фабрика контекстов базы данных.</summary>
    public IDbContextFactory<RpgDbContext> ContextFactory { get; }

    /// <summary>
    /// Создаёт временную базу данных и применяет к ней миграции.
    /// </summary>
    /// <returns>Готовая к работе временная база данных.</returns>
    public static async Task<TestDatabase> CreateAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "rpgcm-tests", Guid.NewGuid().ToString("N"));

        var paths = new TestPathService(root);
        paths.EnsureDirectoriesExist();

        var factory = new TestContextFactory(paths.DatabaseFilePath);

        var service = new SqliteDatabaseService(factory, paths, NullLogger<SqliteDatabaseService>.Instance);
        var result = await service.InitializeAsync();

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error);
        }

        return new TestDatabase(root, paths, factory);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        // Соединения удерживают файл базы данных, поэтому пул очищается перед удалением.
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}
