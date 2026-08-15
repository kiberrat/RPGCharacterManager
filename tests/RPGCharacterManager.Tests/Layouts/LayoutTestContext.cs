using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Core.Abstractions.Layouts;
using RPGCharacterManager.Layouts;
using RPGCharacterManager.Tests.Support;

namespace RPGCharacterManager.Tests.Layouts;

/// <summary>
/// Каталог панелей проверки: три панели вместо настоящих одиннадцати.
///
/// Подсистема макетов не знает ни одной панели по имени, поэтому проверять её
/// можно на любом наборе — этим и подтверждается независимость от интерфейса.
/// </summary>
internal sealed class TestPanelCatalog : ISheetPanelCatalog
{
    /// <summary>Ключ первой панели.</summary>
    public const string First = "первая";

    /// <summary>Ключ второй панели.</summary>
    public const string Second = "вторая";

    /// <summary>Ключ третьей панели.</summary>
    public const string Third = "третья";

    /// <inheritdoc />
    public IReadOnlyList<SheetPanelDescriptor> Panels { get; } =
    [
        new(First, "Первая", "Панель проверки.", 10),
        new(Second, "Вторая", "Панель проверки.", 20),
        new(Third, "Третья", "Панель проверки.", 30),
    ];

    /// <inheritdoc />
    public SheetPanelDescriptor? Find(string panelId) =>
        Panels.FirstOrDefault(panel => string.Equals(panel.Id, panelId, StringComparison.Ordinal));
}

/// <summary>
/// Окружение проверок макетов: настоящая база данных и каталог панелей.
/// </summary>
internal sealed class LayoutTestContext : IAsyncDisposable
{
    private readonly TestDatabase _database;

    private LayoutTestContext(TestDatabase database)
    {
        _database = database;

        Service = new LayoutService(
            database.ContextFactory,
            Catalog,
            NullLogger<LayoutService>.Instance);
    }

    /// <summary>Каталог панелей.</summary>
    public ISheetPanelCatalog Catalog { get; } = new TestPanelCatalog();

    /// <summary>Служба макетов.</summary>
    public ILayoutService Service { get; }

    /// <summary>
    /// Создаёт окружение проверки.
    /// </summary>
    /// <returns>Готовое окружение.</returns>
    public static async Task<LayoutTestContext> CreateAsync() =>
        new(await TestDatabase.CreateAsync());

    /// <summary>
    /// Возвращает применяемый макет.
    /// </summary>
    /// <returns>Макет листа персонажа.</returns>
    public async Task<Layout> GetCurrentAsync()
    {
        var result = await Service.GetCurrentAsync();

        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    /// <summary>
    /// Возвращает макет целиком.
    /// </summary>
    /// <param name="layoutId">Идентификатор макета.</param>
    /// <returns>Макет.</returns>
    public async Task<Layout> GetAsync(Guid layoutId)
    {
        var result = await Service.GetAsync(layoutId);

        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    /// <summary>
    /// Создаёт вкладку в применяемом макете.
    /// </summary>
    /// <param name="title">Заголовок вкладки.</param>
    /// <returns>Идентификатор вкладки.</returns>
    public async Task<Guid> AddTabAsync(string title = "Своя вкладка")
    {
        var layout = await GetCurrentAsync();
        var result = await Service.AddTabAsync(layout.Id, title);

        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _database.DisposeAsync();
}
