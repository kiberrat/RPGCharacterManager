using RPGCharacterManager.Layouts;

namespace RPGCharacterManager.Tests.Layouts;

/// <summary>
/// Проверка макетов интерфейса: вкладки, панели, размеры и расположение.
/// </summary>
public sealed class LayoutServiceTests
{
    [Fact]
    public async Task Макет_СоздаётсяПриПервомОбращении()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var layout = await context.GetCurrentAsync();

        // Встроенный макет повторяет привычный лист: вкладка на каждую панель.
        Assert.True(layout.IsDefault);
        Assert.Equal(context.Catalog.Panels.Count, layout.Tabs.Count);

        Assert.Equal(
            context.Catalog.Panels.Select(panel => panel.Title),
            layout.Tabs.Select(tab => tab.Title));

        Assert.All(layout.Tabs, tab => Assert.Single(tab.Panels));
    }

    [Fact]
    public async Task Макет_НеСоздаётсяПовторно()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var first = await context.GetCurrentAsync();
        var second = await context.GetCurrentAsync();

        Assert.Equal(first.Id, second.Id);

        var all = await context.Service.GetAllAsync();
        Assert.True(all.IsSuccess, all.Error);
        Assert.Single(all.Value);
    }

    [Fact]
    public async Task Макет_СоздаётсяКопиейУказанного()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var source = await context.GetCurrentAsync();
        var created = await context.Service.CreateAsync("Боевой", source.Id);

        Assert.True(created.IsSuccess, created.Error);

        var copy = await context.GetAsync(created.Value);

        Assert.Equal("Боевой", copy.Name);
        Assert.False(copy.IsDefault);
        Assert.Equal(source.Tabs.Count, copy.Tabs.Count);

        // Копия не делит вкладки с образцом: правка одной не трогает другую.
        Assert.DoesNotContain(copy.Tabs.Select(tab => tab.Id), id => source.Tabs.Any(tab => tab.Id == id));
    }

    [Fact]
    public async Task Применение_ОставляетОдинПрименяемыйМакет()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var first = await context.GetCurrentAsync();
        var second = await context.Service.CreateAsync("Городской");

        Assert.True(second.IsSuccess, second.Error);
        Assert.True((await context.Service.ApplyAsync(second.Value)).IsSuccess);

        var all = await context.Service.GetAllAsync();

        Assert.True(all.IsSuccess, all.Error);
        Assert.Equal(second.Value, Assert.Single(all.Value, layout => layout.IsDefault).Id);
        Assert.False(all.Value.Single(layout => layout.Id == first.Id).IsDefault);
    }

    [Fact]
    public async Task Применяемый_Макет_НеУдаляется()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var layout = await context.GetCurrentAsync();

        // Лист персонажа должен остаться хоть с каким-то макетом.
        Assert.True((await context.Service.DeleteAsync(layout.Id)).IsFailure);
    }

    [Fact]
    public async Task Вкладка_ДобавляетсяИПереименовывается()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var tabId = await context.AddTabAsync("Бой");

        Assert.True((await context.Service.RenameTabAsync(tabId, "Схватка")).IsSuccess);

        var layout = await context.GetCurrentAsync();

        Assert.Equal("Схватка", layout.Tabs.Single(tab => tab.Id == tabId).Title);
        Assert.True(layout.Tabs.Single(tab => tab.Id == tabId).IsEmpty);
    }

    [Fact]
    public async Task Последняя_Вкладка_НеУдаляется()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var layout = await context.GetCurrentAsync();

        foreach (var tab in layout.Tabs.Take(layout.Tabs.Count - 1))
        {
            Assert.True((await context.Service.DeleteTabAsync(tab.Id)).IsSuccess);
        }

        var last = (await context.GetCurrentAsync()).Tabs.Single();

        Assert.True((await context.Service.DeleteTabAsync(last.Id)).IsFailure);
    }

    [Fact]
    public async Task Вкладка_ПереставляетсяНаНовоеМесто()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var layout = await context.GetCurrentAsync();
        var last = layout.Tabs[^1];

        Assert.True((await context.Service.MoveTabAsync(last.Id, 0)).IsSuccess);

        Assert.Equal(last.Id, (await context.GetCurrentAsync()).Tabs[0].Id);
    }

    [Fact]
    public async Task Панель_СтавитсяНаВкладкуОдинРаз()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var tabId = await context.AddTabAsync();

        Assert.True((await context.Service.AddPanelAsync(tabId, TestPanelCatalog.First)).IsSuccess);

        // Одна и та же панель дважды на вкладке означала бы две одинаковых области.
        Assert.True((await context.Service.AddPanelAsync(tabId, TestPanelCatalog.First)).IsFailure);

        var tab = (await context.GetCurrentAsync()).Tabs.Single(item => item.Id == tabId);

        Assert.Equal("Первая", Assert.Single(tab.Panels).Title);
    }

    [Fact]
    public async Task Неизвестная_Панель_НеСтавится()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var tabId = await context.AddTabAsync();

        Assert.True((await context.Service.AddPanelAsync(tabId, "нет-такой-панели")).IsFailure);
    }

    [Fact]
    public async Task Панель_ПереноситсяНаДругуюВкладку()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var layout = await context.GetCurrentAsync();
        var source = layout.Tabs[0];
        var target = layout.Tabs[1];
        var panel = source.Panels[0];

        Assert.True((await context.Service.MovePanelAsync(panel.Id, target.Id, 0)).IsSuccess);

        var updated = await context.GetCurrentAsync();

        Assert.Empty(updated.Tabs.Single(tab => tab.Id == source.Id).Panels);

        var moved = updated.Tabs.Single(tab => tab.Id == target.Id);

        Assert.Equal(2, moved.Panels.Count);
        Assert.Equal(panel.PanelId, moved.Panels[0].PanelId);
    }

    [Fact]
    public async Task Перенос_НаВкладкуСТойЖеПанелью_Отклоняется()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var tabId = await context.AddTabAsync();

        Assert.True((await context.Service.AddPanelAsync(tabId, TestPanelCatalog.First)).IsSuccess);

        var layout = await context.GetCurrentAsync();
        var original = layout.Tabs.First(tab => tab.Panels.Any(panel =>
            panel.PanelId == TestPanelCatalog.First && tab.Id != tabId));

        var panel = original.Panels.Single(item => item.PanelId == TestPanelCatalog.First);

        Assert.True((await context.Service.MovePanelAsync(panel.Id, tabId, 0)).IsFailure);
    }

    [Fact]
    public async Task Перенос_ВнутриВкладки_МеняетПорядок()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var tabId = await context.AddTabAsync();

        Assert.True((await context.Service.AddPanelAsync(tabId, TestPanelCatalog.First)).IsSuccess);
        Assert.True((await context.Service.AddPanelAsync(tabId, TestPanelCatalog.Second)).IsSuccess);

        var tab = (await context.GetCurrentAsync()).Tabs.Single(item => item.Id == tabId);
        var second = tab.Panels[1];

        Assert.True((await context.Service.MovePanelAsync(second.Id, tabId, 0)).IsSuccess);

        var reordered = (await context.GetCurrentAsync()).Tabs.Single(item => item.Id == tabId);

        Assert.Equal(TestPanelCatalog.Second, reordered.Panels[0].PanelId);
        Assert.Equal(TestPanelCatalog.First, reordered.Panels[1].PanelId);
    }

    [Fact]
    public async Task Панель_УбираетсяСМакета()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var layout = await context.GetCurrentAsync();
        var panel = layout.Tabs[0].Panels[0];

        Assert.True((await context.Service.RemovePanelAsync(panel.Id)).IsSuccess);

        Assert.Empty((await context.GetCurrentAsync()).Tabs.Single(tab => tab.Id == layout.Tabs[0].Id).Panels);
    }

    [Fact]
    public async Task Размер_ПанелиОграниченПределами()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var layout = await context.GetCurrentAsync();
        var panel = layout.Tabs[0].Panels[0];

        Assert.True((await context.Service.ResizePanelAsync(panel.Id, 2)).IsSuccess);
        Assert.Equal(2, Width(await context.GetCurrentAsync(), panel.Id));

        // Доля вне пределов приводится к границе: панель нулевой ширины
        // была бы неотличима от убранной.
        Assert.True((await context.Service.ResizePanelAsync(panel.Id, 0)).IsSuccess);
        Assert.Equal(LayoutService.MinimumWidth, Width(await context.GetCurrentAsync(), panel.Id));

        Assert.True((await context.Service.ResizePanelAsync(panel.Id, 99)).IsSuccess);
        Assert.Equal(LayoutService.MaximumWidth, Width(await context.GetCurrentAsync(), panel.Id));
    }

    [Fact]
    public async Task Удаление_Вкладки_УбираетЕёПанели()
    {
        await using var context = await LayoutTestContext.CreateAsync();

        var layout = await context.GetCurrentAsync();
        var tab = layout.Tabs[0];

        Assert.True((await context.Service.DeleteTabAsync(tab.Id)).IsSuccess);

        var updated = await context.GetCurrentAsync();

        Assert.DoesNotContain(updated.Tabs, item => item.Id == tab.Id);
        Assert.Equal(layout.Tabs.Count - 1, updated.Tabs.Count);
    }

    /// <summary>
    /// Возвращает долю ширины панели.
    /// </summary>
    /// <param name="layout">Макет.</param>
    /// <param name="panelId">Идентификатор записи макета.</param>
    /// <returns>Доля ширины.</returns>
    private static double Width(Core.Abstractions.Layouts.Layout layout, Guid panelId) =>
        layout.Tabs.SelectMany(tab => tab.Panels).Single(panel => panel.Id == panelId).Width;
}
