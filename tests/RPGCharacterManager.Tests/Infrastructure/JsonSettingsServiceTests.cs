using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.Settings;
using RPGCharacterManager.Infrastructure.Events;
using RPGCharacterManager.Infrastructure.Settings;

namespace RPGCharacterManager.Tests.Infrastructure;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _root;
    private readonly TestPathService _paths;
    private readonly InMemoryEventBus _eventBus;

    public JsonSettingsServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "rpgcm-tests", Guid.NewGuid().ToString("N"));
        _paths = new TestPathService(_root);
        _eventBus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private JsonSettingsService CreateService() =>
        new(_paths, _eventBus, NullLogger<JsonSettingsService>.Instance);

    [Fact]
    public async Task LoadAsync_СоздаётФайлСоЗначениямиПоУмолчанию()
    {
        using var service = CreateService();

        await service.LoadAsync();

        Assert.True(File.Exists(_paths.SettingsFilePath));
        Assert.Equal(ThemeMode.Dark, service.Current.Theme);
        Assert.Equal(AccentColor.Blue, service.Current.Accent);
    }

    [Fact]
    public async Task UpdateAsync_СохраняетИзменения_ИОниЧитаютсяЗаново()
    {
        using (var service = CreateService())
        {
            await service.LoadAsync();
            await service.UpdateAsync(settings => settings.Theme = ThemeMode.Light);
        }

        using var reloaded = CreateService();
        await reloaded.LoadAsync();

        Assert.Equal(ThemeMode.Light, reloaded.Current.Theme);
    }

    [Fact]
    public async Task UpdateAsync_ПубликуетСобытиеИзменения()
    {
        using var service = CreateService();
        await service.LoadAsync();

        AppSettings? received = null;
        using var subscription = _eventBus.Subscribe<SettingsChangedEvent>(payload => received = payload.Settings);

        await service.UpdateAsync(settings => settings.Accent = AccentColor.Green);

        Assert.NotNull(received);
        Assert.Equal(AccentColor.Green, received!.Accent);
    }

    [Fact]
    public async Task UpdateAsync_ПриводитЗначенияКДопустимомуДиапазону()
    {
        using var service = CreateService();
        await service.LoadAsync();

        await service.UpdateAsync(settings =>
        {
            settings.FontSize = 999;
            settings.InterfaceScale = 0.01;
            settings.DiceHistoryLimit = -5;
        });

        Assert.Equal(AppSettings.MaximumFontSize, service.Current.FontSize);
        Assert.Equal(AppSettings.MinimumInterfaceScale, service.Current.InterfaceScale);
        Assert.Equal(0, service.Current.DiceHistoryLimit);
    }

    [Fact]
    public async Task LoadAsync_ПрименяетЗначенияПоУмолчанию_ЕслиФайлПовреждён()
    {
        _paths.EnsureDirectoriesExist();
        await File.WriteAllTextAsync(_paths.SettingsFilePath, "{ это не JSON ");

        using var service = CreateService();
        await service.LoadAsync();

        // Повреждённый файл не должен препятствовать запуску приложения.
        Assert.Equal(ThemeMode.Dark, service.Current.Theme);
    }

    private sealed class TestPathService : IAppPathService
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
}
