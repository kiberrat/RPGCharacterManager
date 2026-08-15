using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database;
using RPGCharacterManager.Database.Backup;
using RPGCharacterManager.Database.Repositories;
using RPGCharacterManager.Tests.Support;

namespace RPGCharacterManager.Tests.Database;

/// <summary>
/// Интеграционные тесты хранилища данных: миграции, связи, индексы,
/// репозитории и резервное копирование выполняются на настоящем файле SQLite.
/// </summary>
public sealed class DatabaseIntegrationTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "rpgcm-db-tests",
        Guid.NewGuid().ToString("N"));

    private TestPathService _paths = null!;

    [SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "Метод CreateDbContextAsync объявлен реализацией по умолчанию в интерфейсе " +
                        "IDbContextFactory и недоступен через конкретный тип.")]
    private IDbContextFactory<RpgDbContext> _contextFactory = null!;

    public async Task InitializeAsync()
    {
        _paths = new TestPathService(_root);
        _paths.EnsureDirectoriesExist();
        _contextFactory = new TestContextFactory(_paths.DatabaseFilePath);

        var service = new SqliteDatabaseService(
            _contextFactory,
            _paths,
            NullLogger<SqliteDatabaseService>.Instance);

        var result = await service.InitializeAsync();
        Assert.True(result.IsSuccess, result.Error);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Миграции_СоздаютВсеТаблицыСхемы()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var applied = await context.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(applied);

        var pending = await context.Database.GetPendingMigrationsAsync();
        Assert.Empty(pending);
    }

    [Fact]
    public async Task Репозиторий_СохраняетИЧитаетИгровуюСистему()
    {
        var repository = new Repository<GameSystem>(_contextFactory);

        await repository.AddAsync(new GameSystem
        {
            Name = "Моя система",
            SystemName = "моя_система",
            Author = "Пользователь",
        });

        var loaded = await repository.ListAsync();

        Assert.Single(loaded);
        Assert.Equal("Моя система", loaded[0].Name);
        Assert.NotEqual(default, loaded[0].CreatedAt);
    }

    [Fact]
    public async Task УникальныйИндекс_ЗапрещаетДваОдинаковыхВнутреннихИмени()
    {
        var repository = new Repository<GameSystem>(_contextFactory);

        await repository.AddAsync(new GameSystem { Name = "Первая", SystemName = "система" });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            repository.AddAsync(new GameSystem { Name = "Вторая", SystemName = "система" }));
    }

    [Fact]
    public async Task УдалениеПерсонажа_УдаляетЕгоХарактеристики()
    {
        var attribute = new AttributeDefinition { Name = "Сила", SystemName = "сила" };
        var character = new Character { Name = "Тестовый герой" };

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.Attributes.Add(attribute);
            context.Characters.Add(character);
            context.CharacterAttributes.Add(new CharacterAttributeValue
            {
                CharacterId = character.Id,
                AttributeId = attribute.Id,
                BaseValue = 16,
            });

            await context.SaveChangesAsync();
        }

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.Characters.Remove(await context.Characters.FirstAsync());
            await context.SaveChangesAsync();
        }

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            // Данные персонажа удаляются вместе с ним, а справочная характеристика остаётся.
            Assert.Empty(await context.CharacterAttributes.ToListAsync());
            Assert.Single(await context.Attributes.ToListAsync());
        }
    }

    [Fact]
    public async Task УдалениеИгровойСистемы_НеУдаляетПользовательскийКонтент()
    {
        var system = new GameSystem { Name = "Система", SystemName = "система" };

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.GameSystems.Add(system);
            context.Spells.Add(new Spell
            {
                Name = "Огненный шар",
                SystemName = "огненный_шар",
                GameSystemId = system.Id,
                Level = 3,
            });

            await context.SaveChangesAsync();
        }

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.GameSystems.Remove(await context.GameSystems.FirstAsync());
            await context.SaveChangesAsync();
        }

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var spell = await context.Spells.SingleAsync();
            Assert.Equal("Огненный шар", spell.Name);
            Assert.Null(spell.GameSystemId);
        }
    }

    [Fact]
    public async Task ПостраничнаяВыборка_ВозвращаетЗапрошеннуюСтраницу()
    {
        const int TotalItems = 25;
        const int PageSize = 10;

        var repository = new Repository<Item>(_contextFactory);

        await repository.AddRangeAsync(Enumerable.Range(0, TotalItems).Select(index => new Item
        {
            Name = $"Предмет {index}",
            SystemName = $"предмет_{index}",
        }));

        var page = await repository.GetPageAsync(pageIndex: 2, pageSize: PageSize);

        Assert.Equal(TotalItems, page.TotalCount);
        Assert.Equal(TotalItems - (2 * PageSize), page.Items.Count);
    }

    [Fact]
    public async Task ПользовательскоеСвойство_СохраняетсяДляЛюбогоОбъекта()
    {
        var definition = new PropertyDefinition
        {
            Name = "Удача",
            SystemName = "удача",
            DisplayName = "Удача",
            TargetType = nameof(Character),
            DataType = GameValueType.WholeNumber,
        };

        var character = new Character { Name = "Герой" };

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.PropertyDefinitions.Add(definition);
            context.Characters.Add(character);
            context.PropertyValues.Add(new PropertyValue
            {
                ObjectId = character.Id,
                PropertyDefinitionId = definition.Id,
                Value = "7",
            });

            await context.SaveChangesAsync();
        }

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var value = await context.PropertyValues
                .Include(item => item.PropertyDefinition)
                .SingleAsync(item => item.ObjectId == character.Id);

            Assert.Equal("7", value.Value);
            Assert.Equal("Удача", value.PropertyDefinition!.DisplayName);
        }
    }

    [Fact]
    public async Task РезервноеКопирование_СоздаётФайлИВосстанавливаетДанные()
    {
        var backupService = new SqliteBackupService(
            _contextFactory,
            _paths,
            NullLogger<SqliteBackupService>.Instance);

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.Campaigns.Add(new Campaign { Name = "Кампания до копии" });
            await context.SaveChangesAsync();
        }

        var backup = await backupService.CreateBackupAsync("Проверка");
        Assert.True(backup.IsSuccess, backup.Error);
        Assert.True(File.Exists(backup.Value.FilePath));

        // Изменяем данные после создания копии.
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.Campaigns.Add(new Campaign { Name = "Кампания после копии" });
            await context.SaveChangesAsync();
        }

        var restore = await backupService.RestoreAsync(backup.Value.FilePath);
        Assert.True(restore.IsSuccess, restore.Error);

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var campaigns = await context.Campaigns.Select(item => item.Name).ToListAsync();
            Assert.Contains("Кампания до копии", campaigns);
            Assert.DoesNotContain("Кампания после копии", campaigns);
        }
    }

    [Fact]
    public async Task СписокКопий_ПоказываетСозданныеКопии()
    {
        var backupService = new SqliteBackupService(
            _contextFactory,
            _paths,
            NullLogger<SqliteBackupService>.Instance);

        await backupService.CreateBackupAsync("Первая");
        await backupService.CreateBackupAsync("Вторая");

        var backups = await backupService.ListBackupsAsync();

        Assert.Equal(2, backups.Count);
        Assert.All(backups, record => Assert.True(File.Exists(record.FilePath)));
    }

}
