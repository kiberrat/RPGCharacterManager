using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Layouts;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Layouts;

/// <summary>
/// Макеты интерфейса: расположение панелей по вкладкам листа персонажа.
///
/// Служба не знает ни одной панели по имени. Встроенный макет она собирает
/// из каталога — по вкладке на панель, что и даёт привычный лист, — а дальше
/// хранит то, что расставил пользователь.
/// </summary>
public sealed class LayoutService : ILayoutService
{
    /// <summary>Название макета, создаваемого при первом запуске.</summary>
    public const string DefaultLayoutName = "Обычный лист";

    /// <summary>Заголовок вкладки, создаваемой без имени.</summary>
    public const string NewTabTitle = "Новая вкладка";

    /// <summary>Наименьшая доля ширины панели.</summary>
    public const double MinimumWidth = 0.25;

    /// <summary>Наибольшая доля ширины панели.</summary>
    public const double MaximumWidth = 4;

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly ISheetPanelCatalog _catalog;
    private readonly ILogger<LayoutService> _logger;

    /// <summary>
    /// Создаёт службу макетов.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="catalog">Каталог доступных панелей.</param>
    /// <param name="logger">Журналировщик.</param>
    public LayoutService(
        IDbContextFactory<RpgDbContext> contextFactory,
        ISheetPanelCatalog catalog,
        ILogger<LayoutService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _catalog = Guard.NotNull(catalog);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<LayoutListItem>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            await EnsureDefaultAsync(context, cancellationToken).ConfigureAwait(false);

            var layouts = await context.SheetLayouts.AsNoTracking()
                .OrderBy(layout => layout.Name)
                .Select(layout => new LayoutListItem(
                    layout.Id,
                    layout.Name,
                    layout.IsDefault,
                    layout.Tabs.Count,
                    layout.Tabs.Sum(tab => tab.Panels.Count)))
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success<IReadOnlyList<LayoutListItem>>(layouts);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LayoutLog.ActionFailed(_logger, exception, "чтение списка макетов");
            return Result.Failure<IReadOnlyList<LayoutListItem>>("Не удалось прочитать макеты.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Layout>> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var layout = await EnsureDefaultAsync(context, cancellationToken).ConfigureAwait(false);

            return Result.Success(Describe(layout));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LayoutLog.ActionFailed(_logger, exception, "чтение применяемого макета");
            return Result.Failure<Layout>("Не удалось прочитать макет листа персонажа.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Layout>> GetAsync(Guid layoutId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var layout = await LoadAsync(context, layoutId, tracked: false, cancellationToken)
                .ConfigureAwait(false);

            return layout is null
                ? Result.Failure<Layout>("Макет не найден: возможно, он был удалён.")
                : Result.Success(Describe(layout));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LayoutLog.ActionFailed(_logger, exception, "чтение макета");
            return Result.Failure<Layout>("Не удалось прочитать макет.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(
        string name,
        Guid? sourceLayoutId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Guid>("Не задано название макета.");
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var source = sourceLayoutId is { } id
                ? await LoadAsync(context, id, tracked: false, cancellationToken).ConfigureAwait(false)
                : null;

            if (sourceLayoutId is not null && source is null)
            {
                return Result.Failure<Guid>("Макет-образец не найден.");
            }

            var created = new SheetLayout { Name = name.Trim() };

            // Копия образца либо встроенный набор вкладок: пустой макет
            // оставил бы лист персонажа без единой панели.
            foreach (var tab in source is null ? BuildDefaultTabs() : CopyTabs(source))
            {
                created.Tabs.Add(tab);
            }

            context.Add(created);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            LayoutLog.LayoutCreated(_logger, created.Name, created.Id);

            return Result.Success(created.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LayoutLog.ActionFailed(_logger, exception, "создание макета");
            return Result.Failure<Guid>("Не удалось создать макет.");
        }
    }

    /// <inheritdoc />
    public Task<Result> RenameAsync(Guid layoutId, string name, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(name)
            ? Task.FromResult(Result.Failure("Не задано название макета."))
            : ChangeAsync("переименование макета", async (context, token) =>
            {
                var layout = await context.SheetLayouts
                    .FirstOrDefaultAsync(item => item.Id == layoutId, token).ConfigureAwait(false);

                if (layout is null)
                {
                    return Result.Failure("Макет не найден.");
                }

                layout.Name = name.Trim();
                return Result.Success();
            }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> ApplyAsync(Guid layoutId, CancellationToken cancellationToken = default) =>
        ChangeAsync("применение макета", async (context, token) =>
        {
            var layouts = await context.SheetLayouts.ToListAsync(token).ConfigureAwait(false);
            var target = layouts.FirstOrDefault(item => item.Id == layoutId);

            if (target is null)
            {
                return Result.Failure("Макет не найден.");
            }

            // Применяемый макет ровно один: иначе лист не знал бы, какой открыть.
            foreach (var layout in layouts)
            {
                layout.IsDefault = layout.Id == layoutId;
            }

            return Result.Success();
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> DeleteAsync(Guid layoutId, CancellationToken cancellationToken = default) =>
        ChangeAsync("удаление макета", async (context, token) =>
        {
            var layout = await context.SheetLayouts
                .FirstOrDefaultAsync(item => item.Id == layoutId, token).ConfigureAwait(false);

            if (layout is null)
            {
                return Result.Failure("Макет не найден.");
            }

            if (layout.IsDefault)
            {
                return Result.Failure(
                    "Этот макет применён к листу персонажа. Примените другой, а затем удалите этот.");
            }

            context.Remove(layout);
            return Result.Success();
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<Guid>> AddTabAsync(
        Guid layoutId,
        string title,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var layout = await LoadAsync(context, layoutId, tracked: true, cancellationToken)
                .ConfigureAwait(false);

            if (layout is null)
            {
                return Result.Failure<Guid>("Макет не найден.");
            }

            var tab = new SheetLayoutTab
            {
                LayoutId = layout.Id,
                Title = string.IsNullOrWhiteSpace(title) ? NewTabTitle : title.Trim(),
                SortOrder = layout.Tabs.Count,
            };

            context.Add(tab);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success(tab.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LayoutLog.ActionFailed(_logger, exception, "добавление вкладки");
            return Result.Failure<Guid>("Не удалось добавить вкладку.");
        }
    }

    /// <inheritdoc />
    public Task<Result> RenameTabAsync(Guid tabId, string title, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(title)
            ? Task.FromResult(Result.Failure("Не задан заголовок вкладки."))
            : ChangeAsync("переименование вкладки", async (context, token) =>
            {
                var tab = await context.SheetLayoutTabs
                    .FirstOrDefaultAsync(item => item.Id == tabId, token).ConfigureAwait(false);

                if (tab is null)
                {
                    return Result.Failure("Вкладка не найдена.");
                }

                tab.Title = title.Trim();
                return Result.Success();
            }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> DeleteTabAsync(Guid tabId, CancellationToken cancellationToken = default) =>
        ChangeAsync("удаление вкладки", async (context, token) =>
        {
            var tab = await context.SheetLayoutTabs
                .Include(item => item.Layout)
                    .ThenInclude(layout => layout!.Tabs)
                .FirstOrDefaultAsync(item => item.Id == tabId, token).ConfigureAwait(false);

            if (tab is null)
            {
                return Result.Failure("Вкладка не найдена.");
            }

            if (tab.Layout is { Tabs.Count: <= 1 })
            {
                return Result.Failure("Это последняя вкладка макета: лист не может остаться без вкладок.");
            }

            context.Remove(tab);
            return Result.Success();
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> MoveTabAsync(Guid tabId, int position, CancellationToken cancellationToken = default) =>
        ChangeAsync("перестановка вкладки", async (context, token) =>
        {
            var tab = await context.SheetLayoutTabs
                .FirstOrDefaultAsync(item => item.Id == tabId, token).ConfigureAwait(false);

            if (tab is null)
            {
                return Result.Failure("Вкладка не найдена.");
            }

            var tabs = await context.SheetLayoutTabs
                .Where(item => item.LayoutId == tab.LayoutId)
                .OrderBy(item => item.SortOrder)
                .ToListAsync(token).ConfigureAwait(false);

            Reorder(tabs, tab, position, item => item.SortOrder, (item, order) => item.SortOrder = order);

            return Result.Success();
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> AddPanelAsync(Guid tabId, string panelId, CancellationToken cancellationToken = default) =>
        ChangeAsync("добавление панели", async (context, token) =>
        {
            if (_catalog.Find(panelId) is null)
            {
                return Result.Failure($"Панель «{panelId}» не объявлена ни одной подсистемой.");
            }

            var tab = await context.SheetLayoutTabs
                .Include(item => item.Panels)
                .FirstOrDefaultAsync(item => item.Id == tabId, token).ConfigureAwait(false);

            if (tab is null)
            {
                return Result.Failure("Вкладка не найдена.");
            }

            if (tab.Panels.Any(panel => string.Equals(panel.PanelId, panelId, StringComparison.Ordinal)))
            {
                return Result.Failure("Эта панель уже стоит на вкладке.");
            }

            context.Add(new SheetLayoutPanel
            {
                TabId = tab.Id,
                PanelId = panelId,
                SortOrder = tab.Panels.Count,
            });

            return Result.Success();
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> RemovePanelAsync(Guid panelId, CancellationToken cancellationToken = default) =>
        ChangeAsync("удаление панели", async (context, token) =>
        {
            var panel = await context.SheetLayoutPanels
                .FirstOrDefaultAsync(item => item.Id == panelId, token).ConfigureAwait(false);

            if (panel is null)
            {
                return Result.Failure("Панель не найдена.");
            }

            context.Remove(panel);
            return Result.Success();
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> MovePanelAsync(
        Guid panelId,
        Guid targetTabId,
        int position,
        CancellationToken cancellationToken = default) =>
        ChangeAsync("перенос панели", async (context, token) =>
        {
            var panel = await context.SheetLayoutPanels
                .FirstOrDefaultAsync(item => item.Id == panelId, token).ConfigureAwait(false);

            if (panel is null)
            {
                return Result.Failure("Панель не найдена.");
            }

            var target = await context.SheetLayoutTabs
                .Include(item => item.Panels)
                .FirstOrDefaultAsync(item => item.Id == targetTabId, token).ConfigureAwait(false);

            if (target is null)
            {
                return Result.Failure("Вкладка назначения не найдена.");
            }

            var moved = panel.TabId != targetTabId;

            if (moved && target.Panels.Any(item =>
                string.Equals(item.PanelId, panel.PanelId, StringComparison.Ordinal)))
            {
                return Result.Failure("На вкладке назначения эта панель уже есть.");
            }

            var sourceTabId = panel.TabId;
            panel.TabId = targetTabId;

            var neighbours = await context.SheetLayoutPanels
                .Where(item => item.TabId == targetTabId || item.Id == panel.Id)
                .OrderBy(item => item.SortOrder)
                .ToListAsync(token).ConfigureAwait(false);

            Reorder(neighbours, panel, position, item => item.SortOrder, (item, order) => item.SortOrder = order);

            if (moved)
            {
                // Вкладка, из которой панель ушла, перенумеровывается отдельно:
                // иначе в её порядке остался бы пропуск.
                var source = await context.SheetLayoutPanels
                    .Where(item => item.TabId == sourceTabId)
                    .OrderBy(item => item.SortOrder)
                    .ToListAsync(token).ConfigureAwait(false);

                for (var index = 0; index < source.Count; index++)
                {
                    source[index].SortOrder = index;
                }
            }

            return Result.Success();
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> ResizePanelAsync(
        Guid panelId,
        double width,
        CancellationToken cancellationToken = default) =>
        ChangeAsync("изменение размера панели", async (context, token) =>
        {
            var panel = await context.SheetLayoutPanels
                .FirstOrDefaultAsync(item => item.Id == panelId, token).ConfigureAwait(false);

            if (panel is null)
            {
                return Result.Failure("Панель не найдена.");
            }

            panel.Width = Math.Clamp(width, MinimumWidth, MaximumWidth);
            return Result.Success();
        }, cancellationToken);

    /// <summary>
    /// Возвращает применяемый макет, создавая встроенный при первом обращении.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Применяемый макет.</returns>
    private async Task<SheetLayout> EnsureDefaultAsync(
        RpgDbContext context,
        CancellationToken cancellationToken)
    {
        var existing = await Query(context, tracked: false)
            .FirstOrDefaultAsync(layout => layout.IsDefault, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        // Встроенный макет повторяет привычный лист: вкладка на каждую панель
        // в том порядке, в каком панели объявлены подсистемами.
        var created = new SheetLayout { Name = DefaultLayoutName, IsDefault = true };

        foreach (var tab in BuildDefaultTabs())
        {
            created.Tabs.Add(tab);
        }

        context.Add(created);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LayoutLog.DefaultLayoutCreated(_logger, created.Tabs.Count);

        return created;
    }

    /// <summary>
    /// Собирает вкладки встроенного макета: по вкладке на каждую панель каталога.
    /// </summary>
    /// <returns>Вкладки в порядке каталога.</returns>
    private List<SheetLayoutTab> BuildDefaultTabs() =>
        [.. _catalog.Panels.Select((panel, index) =>
        {
            var tab = new SheetLayoutTab { Title = panel.Title, SortOrder = index };

            tab.Panels.Add(new SheetLayoutPanel { TabId = tab.Id, PanelId = panel.Id });

            return tab;
        })];

    /// <summary>
    /// Копирует вкладки макета-образца.
    /// </summary>
    /// <param name="source">Макет-образец.</param>
    /// <returns>Копии вкладок.</returns>
    private static List<SheetLayoutTab> CopyTabs(SheetLayout source) =>
        [.. source.Tabs.OrderBy(tab => tab.SortOrder).Select(tab =>
        {
            var copy = new SheetLayoutTab { Title = tab.Title, SortOrder = tab.SortOrder };

            foreach (var panel in tab.Panels.OrderBy(panel => panel.SortOrder))
            {
                copy.Panels.Add(new SheetLayoutPanel
                {
                    TabId = copy.Id,
                    PanelId = panel.PanelId,
                    SortOrder = panel.SortOrder,
                    Width = panel.Width,
                });
            }

            return copy;
        })];

    /// <summary>
    /// Переводит макет в описание для интерфейса.
    /// </summary>
    /// <param name="layout">Макет из базы данных.</param>
    /// <returns>Описание макета.</returns>
    private Layout Describe(SheetLayout layout) => new(
        layout.Id,
        layout.Name,
        layout.IsDefault,
        [.. layout.Tabs.OrderBy(tab => tab.SortOrder).Select(tab => new LayoutTab(
            tab.Id,
            tab.Title,
            [.. tab.Panels.OrderBy(panel => panel.SortOrder).Select(panel =>
            {
                var descriptor = _catalog.Find(panel.PanelId);

                // Панель, которой больше нет в приложении, показывается явно:
                // молча пропасть она не должна, иначе пользователь решит, что
                // потерял её сам.
                return new LayoutPanel(
                    panel.Id,
                    panel.PanelId,
                    descriptor?.Title ?? $"Панель удалена ({panel.PanelId})",
                    panel.Width,
                    descriptor is null);
            })]))]);

    /// <summary>
    /// Загружает макет со вкладками и панелями.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="layoutId">Идентификатор макета.</param>
    /// <param name="tracked">Загружать для изменения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Макет или <see langword="null"/>.</returns>
    private static Task<SheetLayout?> LoadAsync(
        RpgDbContext context,
        Guid layoutId,
        bool tracked,
        CancellationToken cancellationToken) =>
        Query(context, tracked).FirstOrDefaultAsync(layout => layout.Id == layoutId, cancellationToken);

    /// <summary>
    /// Строит запрос макета со вкладками и панелями.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="tracked">Загружать для изменения.</param>
    /// <returns>Запрос.</returns>
    private static IQueryable<SheetLayout> Query(RpgDbContext context, bool tracked)
    {
        IQueryable<SheetLayout> query = context.SheetLayouts
            .Include(layout => layout.Tabs)
                .ThenInclude(tab => tab.Panels);

        return tracked ? query : query.AsNoTracking();
    }

    /// <summary>
    /// Переставляет элемент списка на новое место и перенумеровывает порядок.
    /// </summary>
    /// <typeparam name="TItem">Тип элемента.</typeparam>
    /// <param name="items">Элементы в текущем порядке.</param>
    /// <param name="moved">Переставляемый элемент.</param>
    /// <param name="position">Новая позиция.</param>
    /// <param name="get">Чтение порядка.</param>
    /// <param name="set">Запись порядка.</param>
    private static void Reorder<TItem>(
        List<TItem> items,
        TItem moved,
        int position,
        Func<TItem, int> get,
        Action<TItem, int> set)
        where TItem : class
    {
        var ordered = items.OrderBy(get).ToList();

        ordered.Remove(moved);
        ordered.Insert(Math.Clamp(position, 0, ordered.Count), moved);

        for (var index = 0; index < ordered.Count; index++)
        {
            set(ordered[index], index);
        }
    }

    /// <summary>
    /// Выполняет изменение макета в одной транзакции сохранения.
    /// </summary>
    /// <param name="action">Название действия для журнала.</param>
    /// <param name="change">Изменение.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат изменения.</returns>
    private async Task<Result> ChangeAsync(
        string action,
        Func<RpgDbContext, CancellationToken, Task<Result>> change,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var result = await change(context, cancellationToken).ConfigureAwait(false);

            if (result.IsFailure)
            {
                return result;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LayoutLog.ActionFailed(_logger, exception, action);
            return Result.Failure($"Не удалось выполнить действие: {action}.");
        }
    }
}
