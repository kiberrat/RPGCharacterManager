using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Content;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database;
using RPGCharacterManager.Extensions;
using RPGCharacterManager.Tests.Support;

namespace RPGCharacterManager.Tests.Extensions;

/// <summary>
/// Подсистема расширений, собранная поверх временной базы данных вместе
/// со службой контента, которая знает все виды игровых объектов.
/// </summary>
internal sealed class ExtensionTestContext : IAsyncDisposable
{
    private readonly TestDatabase _database;
    private readonly string _root;

    private ExtensionTestContext(TestDatabase database, ContentService content, ExtensionService service)
    {
        _database = database;
        _root = Path.Combine(Path.GetTempPath(), "rpg-extensions-" + Guid.NewGuid().ToString("N"));
        Content = content;
        Service = service;

        Directory.CreateDirectory(_root);
    }

    /// <summary>Служба расширений.</summary>
    public ExtensionService Service { get; }

    /// <summary>Служба контента.</summary>
    public ContentService Content { get; }

    /// <summary>Фабрика контекстов базы данных.</summary>
    public IDbContextFactory<RpgDbContext> ContextFactory => _database.ContextFactory;

    /// <summary>
    /// Создаёт окружение теста.
    /// </summary>
    /// <returns>Готовое окружение.</returns>
    public static async Task<ExtensionTestContext> CreateAsync()
    {
        var database = await TestDatabase.CreateAsync();

        var content = new ContentService(
            StandardContentTypes.Create(),
            database.ContextFactory,
            NullLogger<ContentService>.Instance);

        var service = new ExtensionService(
            database.ContextFactory,
            content,
            NullLogger<ExtensionService>.Instance);

        return new ExtensionTestContext(database, content, service);
    }

    /// <summary>
    /// Возвращает путь к файлу расширения во временном каталоге теста.
    /// </summary>
    /// <param name="name">Имя файла без расширения.</param>
    /// <returns>Полный путь.</returns>
    public string PathFor(string name) =>
        Path.Combine(_root, name + Core.Abstractions.Extensions.ExtensionPackage.FileExtension);

    /// <summary>
    /// Сохраняет объекты в базе данных.
    /// </summary>
    /// <typeparam name="TEntity">Тип объекта.</typeparam>
    /// <param name="entities">Сохраняемые объекты.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    public async Task AddAsync<TEntity>(params TEntity[] entities)
        where TEntity : class
    {
        await using var context = await _database.ContextFactory.CreateDbContextAsync();

        context.AddRange(entities);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Создаёт контекст базы данных для проверок.
    /// </summary>
    /// <returns>Контекст базы данных.</returns>
    public Task<RpgDbContext> CreateContextAsync() => _database.ContextFactory.CreateDbContextAsync();

    /// <summary>
    /// Создаёт игровую систему.
    /// </summary>
    /// <param name="name">Название системы.</param>
    /// <returns>Созданная система.</returns>
    public static GameSystem System(string name) => new()
    {
        Name = name,
        SystemName = name.ToLowerInvariant(),
        Version = "1.0",
        CarryCapacityFormula = "Сила * 10",
        WeightUnit = "кг",
    };

    /// <summary>
    /// Создаёт расу.
    /// </summary>
    /// <param name="name">Название расы.</param>
    /// <param name="gameSystemId">Игровая система.</param>
    /// <returns>Созданная раса.</returns>
    public static Race Race(string name, Guid? gameSystemId = null) => new()
    {
        Name = name,
        SystemName = name.ToLowerInvariant(),
        Description = $"Раса {name}.",
        GameSystemId = gameSystemId,
    };

    /// <summary>
    /// Создаёт заклинание.
    /// </summary>
    /// <param name="name">Название заклинания.</param>
    /// <param name="gameSystemId">Игровая система.</param>
    /// <param name="level">Уровень заклинания.</param>
    /// <returns>Созданное заклинание.</returns>
    public static Spell Spell(string name, Guid? gameSystemId = null, int level = 1) => new()
    {
        Name = name,
        SystemName = name.ToLowerInvariant(),
        Level = level,
        Formula = "1d6",
        GameSystemId = gameSystemId,
    };

    /// <summary>
    /// Создаёт игровое правило.
    /// </summary>
    /// <param name="name">Название правила.</param>
    /// <param name="gameSystemId">Игровая система.</param>
    /// <returns>Созданное правило.</returns>
    public static GameRule Rule(string name, Guid? gameSystemId = null) => new()
    {
        Name = name,
        SystemName = name.ToLowerInvariant(),
        Trigger = "персонаж.создание",
        ActionsJson = "[]",
        GameSystemId = gameSystemId,
    };

    /// <summary>
    /// Создаёт макрос.
    /// </summary>
    /// <param name="name">Название макроса.</param>
    /// <param name="gameSystemId">Игровая система.</param>
    /// <returns>Созданный макрос.</returns>
    public static Macro Macro(string name, Guid? gameSystemId = null) => new()
    {
        Name = name,
        SystemName = name.ToLowerInvariant(),
        ActionsJson = "[]",
        GameSystemId = gameSystemId,
    };

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _database.DisposeAsync();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
