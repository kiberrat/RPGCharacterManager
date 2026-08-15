using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RPGCharacterManager.Shared;

namespace RPGCharacterManager.Database;

/// <summary>
/// Фабрика контекста для средств разработки Entity Framework Core.
///
/// Используется только командой <c>dotnet ef</c> при создании и применении миграций.
/// Во время работы приложения контекст создаётся контейнером зависимостей, поэтому
/// эта фабрика не влияет на выполнение программы.
/// </summary>
public sealed class RpgDbContextFactory : IDesignTimeDbContextFactory<RpgDbContext>
{
    /// <inheritdoc />
    public RpgDbContext CreateDbContext(string[] args)
    {
        // Миграции описывают схему и не обращаются к пользовательским данным,
        // поэтому во время разработки используется временный файл базы данных.
        var designTimePath = Path.Combine(
            Path.GetTempPath(),
            ApplicationConstants.DataFolderName,
            "design-time.db");

        Directory.CreateDirectory(Path.GetDirectoryName(designTimePath)!);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = designTimePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        var options = new DbContextOptionsBuilder<RpgDbContext>()
            .UseSqlite(connectionString, sqlite => sqlite.MigrationsAssembly(
                typeof(RpgDbContext).Assembly.FullName))
            .Options;

        return new RpgDbContext(options);
    }
}
