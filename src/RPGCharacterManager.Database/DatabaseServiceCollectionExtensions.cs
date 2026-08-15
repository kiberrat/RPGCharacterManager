using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RPGCharacterManager.Core.Abstractions.Data;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Database.Backup;
using RPGCharacterManager.Database.Configuration;
using RPGCharacterManager.Database.Repositories;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Database;

/// <summary>
/// Регистрация служб доступа к данным в контейнере зависимостей.
/// </summary>
public static class DatabaseServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует контекст базы данных SQLite и службы её обслуживания.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <param name="configuration">Конфигурация приложения.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        Guard.NotNull(services);
        Guard.NotNull(configuration);

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName));

        // Фабрика контекстов вместо контекста с временем жизни Scoped: настольное
        // приложение не имеет области запроса, а долгоживущий контекст накапливал бы
        // отслеживаемые сущности и потреблял память.
        services.AddDbContextFactory<RpgDbContext>((provider, builder) =>
        {
            var options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var paths = provider.GetRequiredService<IAppPathService>();

            var databasePath = string.IsNullOrWhiteSpace(options.DatabaseFilePath)
                ? paths.DatabaseFilePath
                : options.DatabaseFilePath;

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                // Пул соединений и общий кэш обеспечивают быстрый доступ при большом
                // количестве коротких операций чтения из интерфейса.
                Pooling = true,
            }.ToString();

            builder.UseSqlite(
                connectionString,
                sqlite => sqlite
                    .MigrationsAssembly(typeof(RpgDbContext).Assembly.FullName)
                    .CommandTimeout(options.CommandTimeoutSeconds)

                    // Объект, у которого несколько списков, читается несколькими
                    // запросами, а не одним (решение Р-104). Иначе база соединяет
                    // списки между собой и возвращает произведение их длин:
                    // предмет с 5 бонусами и 5 действиями даёт 25 строк вместо 10.
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));

            // Встроенный LIKE в SQLite сводит регистр только у латиницы, поэтому
            // он заменяется собственной функцией. Отбор по названию перестаёт
            // различать регистр кириллицы сразу во всех подсистемах (решение Р-95).
            builder.AddInterceptors(new UnicodeLikeInterceptor());

            if (options.EnableSensitiveDataLogging)
            {
                builder.EnableSensitiveDataLogging();
            }
        });

        services.TryAddSingleton<IDatabaseService, SqliteDatabaseService>();
        services.TryAddSingleton<IBackupService, SqliteBackupService>();

        // Универсальное хранилище доступно для любого типа сущности: подсистемам
        // не требуется регистрировать собственный репозиторий для каждой таблицы.
        services.TryAddSingleton(typeof(IRepository<>), typeof(Repository<>));

        return services;
    }
}
