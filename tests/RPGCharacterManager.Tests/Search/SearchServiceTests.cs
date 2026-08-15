using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Core.Abstractions.Search;
using RPGCharacterManager.Core.Models.Shell;
using RPGCharacterManager.Search;

namespace RPGCharacterManager.Tests.Search;

/// <summary>
/// Поставщик находок для проверок: отдаёт заранее заданные группы.
/// </summary>
/// <param name="order">Порядок групп.</param>
/// <param name="groups">Группы, которые поставщик возвращает.</param>
internal sealed class FakeSearchProvider(int order, params SearchGroup[] groups) : ISearchProvider
{
    /// <summary>Сколько раз поставщика спрашивали.</summary>
    public int Calls { get; private set; }

    /// <summary>Запрос, с которым обращались последний раз.</summary>
    public string? LastQuery { get; private set; }

    /// <inheritdoc />
    public int Order => order;

    /// <inheritdoc />
    public Task<IReadOnlyList<SearchGroup>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        LastQuery = query;

        return Task.FromResult<IReadOnlyList<SearchGroup>>(groups);
    }
}

/// <summary>
/// Поставщик находок, всегда завершающийся ошибкой.
/// </summary>
internal sealed class BrokenSearchProvider : ISearchProvider
{
    /// <inheritdoc />
    public int Order => 1;

    /// <inheritdoc />
    public Task<IReadOnlyList<SearchGroup>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Подсистема недоступна.");
}

/// <summary>
/// Проверка глобального поиска.
/// </summary>
public sealed class SearchServiceTests
{
    private static SearchGroup Group(string title, int order, params string[] titles) =>
        new(title, order, [.. titles.Select(name => new SearchHit(name, null, DocumentIds.Content, null))], titles.Length);

    private static SearchService Create(params ISearchProvider[] providers) =>
        new(providers, NullLogger<SearchService>.Instance);

    [Fact]
    public async Task Поиск_СобираетНаходкиВсехПоставщиков()
    {
        var service = Create(
            new FakeSearchProvider(20, Group("Заклинания", 20, "Огненный шар")),
            new FakeSearchProvider(10, Group("Персонажи", 10, "Аргус")));

        var result = await service.SearchAsync("ар");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Value.Count);

        // Группы идут в порядке, заданном поставщиками, а не порядке их вызова.
        Assert.Equal(["Персонажи", "Заклинания"], result.Value.Groups.Select(group => group.Title));
    }

    [Fact]
    public async Task Поиск_НеСпрашиваетПоставщиковНаКороткомЗапросе()
    {
        var provider = new FakeSearchProvider(10, Group("Персонажи", 10, "Аргус"));
        var service = Create(provider);

        var result = await service.SearchAsync("а");

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(result.Value.IsEmpty);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task Поиск_ОбрезаетПробелыЗапроса()
    {
        var provider = new FakeSearchProvider(10, Group("Персонажи", 10, "Аргус"));
        var service = Create(provider);

        await service.SearchAsync("  Аргус  ");

        Assert.Equal("Аргус", provider.LastQuery);
    }

    [Fact]
    public async Task Поиск_ПропускаетПустыеГруппы()
    {
        var service = Create(new FakeSearchProvider(
            10,
            Group("Персонажи", 10, "Аргус"),
            Group("Монстры", 20)));

        var result = await service.SearchAsync("ар");

        Assert.True(result.IsSuccess, result.Error);

        // Пустая группа только занимала бы место в списке находок.
        Assert.Equal("Персонажи", Assert.Single(result.Value.Groups).Title);
    }

    [Fact]
    public async Task Поиск_ПереживаетОтказПоставщика()
    {
        var service = Create(
            new BrokenSearchProvider(),
            new FakeSearchProvider(10, Group("Персонажи", 10, "Аргус")));

        var result = await service.SearchAsync("ар");

        // Поиск, замолчавший из-за одной подсистемы, бесполезен целиком.
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("Персонажи", Assert.Single(result.Value.Groups).Title);
    }

    [Fact]
    public void Группа_СообщаетОСкрытыхНаходках()
    {
        var shown = Group("Заклинания", 20, "Огненный шар", "Ледяной кинжал");
        var group = shown with { Total = 43 };

        Assert.True(group.HasMore);
        Assert.Equal("показано 2 из 43", group.Caption);

        Assert.False(shown.HasMore);
        Assert.Equal("найдено: 2", shown.Caption);
    }
}
