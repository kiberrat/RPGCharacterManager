using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Abstractions.Extensions;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Extensions;

/// <summary>
/// Расширения приложения: установка, включение, удаление и выгрузка.
///
/// Служба не загружает и не выполняет чужой код (решение Р-102): расширение —
/// это набор объектов самого приложения. Состав расширения задан перечнем видов
/// контента, а не списком в коде, поэтому вид контента, добавленный подсистемой
/// будущего этапа, попадает в расширения сам (решение Р-103).
/// </summary>
public sealed class ExtensionService : IExtensionService
{
    /// <summary>
    /// Виды контента, которые не входят в состав расширения как обычные объекты.
    ///
    /// Игровая система записана в пакет отдельно — она его основа, а не одно
    /// из содержимого. Расширения же в расширение не вкладываются: зависимости
    /// описаны в манифесте и устанавливаются отдельными файлами.
    /// </summary>
    private static readonly string[] SkippedTypes = [ContentTypeIds.GameSystems, ContentTypeIds.ContentPacks];

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly IContentService _content;
    private readonly ILogger<ExtensionService> _logger;

    /// <summary>
    /// Создаёт службу расширений.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="content">Служба контента: она знает все виды игровых объектов.</param>
    /// <param name="logger">Журналировщик.</param>
    public ExtensionService(
        IDbContextFactory<RpgDbContext> contextFactory,
        IContentService content,
        ILogger<ExtensionService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _content = Guard.NotNull(content);
        _logger = Guard.NotNull(logger);
    }

    /// <summary>Виды контента, входящие в состав расширения.</summary>
    private IEnumerable<IContentTypeDescriptor> PackedTypes =>
        _content.Types.Where(type => !SkippedTypes.Contains(type.Id, StringComparer.Ordinal));

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ExtensionItem>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var packs = await context.ContentPacks
                .AsNoTracking()
                .OrderBy(pack => pack.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var systems = await context.GameSystems
                .AsNoTracking()
                .ToDictionaryAsync(system => system.Id, system => system.Name, cancellationToken)
                .ConfigureAwait(false);

            var items = new List<ExtensionItem>(packs.Count);

            foreach (var pack in packs)
            {
                items.Add(await BuildItemAsync(context, pack, packs, systems, cancellationToken)
                    .ConfigureAwait(false));
            }

            return Result.Success<IReadOnlyList<ExtensionItem>>(items);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ExtensionLog.OperationFailed(_logger, exception);

            return Result.Failure<IReadOnlyList<ExtensionItem>>(
                "Не удалось прочитать список расширений. Подробности записаны в журнал.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<ExtensionPreview>> InspectAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var file = await ExtensionPackageFile.ReadAsync(path, cancellationToken).ConfigureAwait(false);

        if (file.IsFailure)
        {
            return Result.Failure<ExtensionPreview>(file.Error!);
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success(await BuildPreviewAsync(context, file.Value, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ExtensionLog.OperationFailed(_logger, exception);

            return Result.Failure<ExtensionPreview>(
                "Не удалось проверить расширение. Подробности записаны в журнал.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<ExtensionItem>> InstallAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var file = await ExtensionPackageFile.ReadAsync(path, cancellationToken).ConfigureAwait(false);

        if (file.IsFailure)
        {
            return Result.Failure<ExtensionItem>(file.Error!);
        }

        var manifest = file.Value.Manifest;
        Guid packId;

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var preview = await BuildPreviewAsync(context, file.Value, cancellationToken).ConfigureAwait(false);

            if (!preview.CanInstall)
            {
                return Result.Failure<ExtensionItem>(string.Join(" ", preview.Problems));
            }

            packId = await PrepareAsync(context, manifest, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ExtensionLog.OperationFailed(_logger, exception);

            return Result.Failure<ExtensionItem>(
                "Не удалось подготовить установку. Подробности записаны в журнал.");
        }

        try
        {
            var installed = await WriteContentAsync(packId, file.Value, cancellationToken).ConfigureAwait(false);

            if (installed.IsFailure)
            {
                await RollbackAsync(packId, cancellationToken).ConfigureAwait(false);

                return Result.Failure<ExtensionItem>(installed.Error!);
            }

            ExtensionLog.Installed(_logger, manifest.Name, manifest.Version, installed.Value);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Наполовину установленное расширение хуже неустановленного: оно
            // выглядит рабочим, а половины его содержимого нет. Поэтому неудача
            // откатывается до состояния «расширение не установлено».
            ExtensionLog.InstallRolledBack(_logger, exception, manifest.Name);

            await RollbackAsync(packId, cancellationToken).ConfigureAwait(false);

            return Result.Failure<ExtensionItem>(
                $"Не удалось установить расширение: {exception.Message}");
        }

        return await LoadItemAsync(packId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<ExtensionItem>> SetEnabledAsync(
        Guid id,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var pack = await context.ContentPacks
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (pack is null)
            {
                return Result.Failure<ExtensionItem>("Расширение не найдено: возможно, оно уже удалено.");
            }

            pack.Enabled = enabled;

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ExtensionLog.OperationFailed(_logger, exception);

            return Result.Failure<ExtensionItem>(
                "Не удалось изменить состояние расширения. Подробности записаны в журнал.");
        }

        return await LoadItemAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<int>> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var pack = await context.ContentPacks
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (pack is null)
            {
                return Result.Failure<int>("Расширение не найдено: возможно, оно уже удалено.");
            }

            var name = pack.Name;
            var removed = await ClearContentAsync(id, cancellationToken).ConfigureAwait(false);

            context.ContentPacks.Remove(pack);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            ExtensionLog.Removed(_logger, name, removed);

            return Result.Success(removed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ExtensionLog.OperationFailed(_logger, exception);

            return Result.Failure<int>("Не удалось удалить расширение. Подробности записаны в журнал.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ExtensionSource>>> GetSourcesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var systems = await context.GameSystems
                .AsNoTracking()
                .OrderBy(system => system.Name)
                .Select(system => new ExtensionSource(system.Id, system.Name, true))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var packs = await context.ContentPacks
                .AsNoTracking()
                .OrderBy(pack => pack.Name)
                .Select(pack => new ExtensionSource(pack.Id, pack.Name, false))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return Result.Success<IReadOnlyList<ExtensionSource>>([.. systems, .. packs]);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ExtensionLog.OperationFailed(_logger, exception);

            return Result.Failure<IReadOnlyList<ExtensionSource>>(
                "Не удалось прочитать список источников. Подробности записаны в журнал.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<ExtensionExportResult>> ExportAsync(
        ExtensionExportRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        var owner = new ContentOwner(request.GameSystemId, request.ContentPackId);

        if (!owner.IsSpecified)
        {
            return Result.Failure<ExtensionExportResult>(
                "Не указано, что выгружать: выберите игровую систему или установленное расширение.");
        }

        if (string.IsNullOrWhiteSpace(request.Manifest.Name))
        {
            return Result.Failure<ExtensionExportResult>("У расширения должно быть название.");
        }

        try
        {
            var content = new PackageContent();
            var sections = new List<ExtensionSection>();

            await using (var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
            {
                content.GameSystem = await ReadGameSystemAsync(context, request.GameSystemId, cancellationToken)
                    .ConfigureAwait(false);

                content.Rules = await ReadRulesAsync(context, owner, cancellationToken).ConfigureAwait(false);
                content.Macros = await ReadMacrosAsync(context, owner, cancellationToken).ConfigureAwait(false);
                content.Aliases = await ReadAliasesAsync(context, owner, cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var type in PackedTypes)
            {
                var entities = await _content
                    .GetOwnedAsync(type.Id, owner, cancellationToken).ConfigureAwait(false);

                if (entities.Count == 0)
                {
                    continue;
                }

                content.Objects[type.Id] =
                [
                    .. entities.Select(entity =>
                        JsonSerializer.SerializeToNode(entity, entity.GetType(), ExtensionPackageFile.Options)!),
                ];

                sections.Add(new ExtensionSection(type.Id, type.DisplayName, entities.Count));
            }

            AddSection(sections, ExtensionSections.Aliases, "Псевдонимы", content.Aliases.Count);
            AddSection(sections, ExtensionSections.Rules, "Правила", content.Rules.Count);
            AddSection(sections, ExtensionSections.Macros, "Макросы", content.Macros.Count);

            var written = await ExtensionPackageFile
                .WriteAsync(request.Path, request.Manifest, content, cancellationToken).ConfigureAwait(false);

            if (written.IsFailure)
            {
                return Result.Failure<ExtensionExportResult>(written.Error!);
            }

            var result = new ExtensionExportResult(request.Path, sections, written.Value);

            ExtensionLog.Exported(_logger, request.Manifest.Name, result.ObjectCount, request.Path);

            return Result.Success(result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ExtensionLog.OperationFailed(_logger, exception);

            return Result.Failure<ExtensionExportResult>(
                "Не удалось выгрузить расширение. Подробности записаны в журнал.");
        }
    }

    /// <summary>
    /// Добавляет раздел состава, если в нём что-то есть.
    /// </summary>
    /// <param name="sections">Собираемые разделы.</param>
    /// <param name="typeId">Идентификатор раздела.</param>
    /// <param name="title">Название раздела.</param>
    /// <param name="count">Количество объектов.</param>
    private static void AddSection(List<ExtensionSection> sections, string typeId, string title, int count)
    {
        if (count > 0)
        {
            sections.Add(new ExtensionSection(typeId, title, count));
        }
    }

    /// <summary>
    /// Читает игровую систему для выгрузки.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="gameSystemId">Игровая система.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Записанная система либо <see langword="null"/>.</returns>
    private static async Task<JsonNode?> ReadGameSystemAsync(
        RpgDbContext context,
        Guid? gameSystemId,
        CancellationToken cancellationToken)
    {
        if (gameSystemId is not { } id)
        {
            return null;
        }

        var system = await context.GameSystems
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return system is null ? null : JsonSerializer.SerializeToNode(system, ExtensionPackageFile.Options);
    }

    /// <summary>
    /// Читает правила владельца для выгрузки.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="owner">Владелец объектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Записанные правила.</returns>
    private static async Task<List<JsonNode>> ReadRulesAsync(
        RpgDbContext context,
        ContentOwner owner,
        CancellationToken cancellationToken)
    {
        // Правила персонажа и кампании не выгружаются: они принадлежат игре
        // конкретного человека, а не игровой системе, которой делятся.
        var query = context.Rules
            .AsNoTracking()
            .Where(rule => rule.CharacterId == null && rule.CampaignId == null);

        query = Filter(query, owner);

        var rules = await query.OrderBy(rule => rule.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rules.Select(rule => JsonSerializer.SerializeToNode(rule, ExtensionPackageFile.Options)!)];
    }

    /// <summary>
    /// Читает макросы владельца для выгрузки.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="owner">Владелец объектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Записанные макросы.</returns>
    private static async Task<List<JsonNode>> ReadMacrosAsync(
        RpgDbContext context,
        ContentOwner owner,
        CancellationToken cancellationToken)
    {
        var query = context.Macros.AsNoTracking().Where(macro => macro.CharacterId == null);

        query = Filter(query, owner);

        var macros = await query.OrderBy(macro => macro.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. macros.Select(macro => JsonSerializer.SerializeToNode(macro, ExtensionPackageFile.Options)!)];
    }

    /// <summary>Читает псевдонимы выбранного владельца для выгрузки.</summary>
    private static async Task<List<PackageAlias>> ReadAliasesAsync(
        RpgDbContext context,
        ContentOwner owner,
        CancellationToken cancellationToken)
    {
        var query = context.ContentAliases.AsNoTracking().AsQueryable();

        if (owner.GameSystemId is { } gameSystemId)
        {
            query = query.Where(alias => alias.GameSystemId == gameSystemId);
        }

        if (owner.ContentPackId is { } contentPackId)
        {
            query = query.Where(alias => alias.ContentPackId == contentPackId);
        }

        return await query
            .OrderBy(alias => alias.ContentTypeId)
            .ThenBy(alias => alias.TargetSystemName)
            .ThenBy(alias => alias.Alias)
            .Select(alias => new PackageAlias
            {
                ContentTypeId = alias.ContentTypeId,
                TargetSystemName = alias.TargetSystemName,
                Alias = alias.Alias,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Отбирает игровые объекты по владельцу.
    /// </summary>
    /// <typeparam name="TEntity">Тип игрового объекта.</typeparam>
    /// <param name="query">Исходный запрос.</param>
    /// <param name="owner">Владелец объектов.</param>
    /// <returns>Отобранный запрос.</returns>
    private static IQueryable<TEntity> Filter<TEntity>(IQueryable<TEntity> query, ContentOwner owner)
        where TEntity : ContentEntity
    {
        if (owner.GameSystemId is { } gameSystemId)
        {
            query = query.Where(entity => entity.GameSystemId == gameSystemId);
        }

        if (owner.ContentPackId is { } contentPackId)
        {
            query = query.Where(entity => entity.ContentPackId == contentPackId);
        }

        return query;
    }

    /// <summary>
    /// Разбирает прочитанный файл и проверяет возможность установки.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="file">Прочитанный файл.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Разбор файла.</returns>
    private async Task<ExtensionPreview> BuildPreviewAsync(
        RpgDbContext context,
        PackageFile file,
        CancellationToken cancellationToken)
    {
        var manifest = file.Manifest;
        var problems = new List<string>();
        var warnings = new List<string>();
        var sections = new List<ExtensionSection>();

        if (ExtensionPackage.ParseVersion(manifest.FormatVersion) is { } format
            && format > ExtensionPackage.ParseVersion(ExtensionPackage.FormatVersion))
        {
            problems.Add(
                $"Расширение записано в формате {manifest.FormatVersion}, "
                + $"а приложение понимает {ExtensionPackage.FormatVersion}. Обновите приложение.");
        }

        if (!ExtensionPackage.Satisfies(ApplicationConstants.Version, manifest.RequiredVersion))
        {
            problems.Add(
                $"Расширению нужна версия приложения не ниже {manifest.RequiredVersion}, "
                + $"а установлена {ApplicationConstants.Version}.");
        }

        var installed = await context.ContentPacks
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        problems.AddRange(FindMissingDependencies(manifest, installed));

        var replaced = installed.FirstOrDefault(pack =>
            string.Equals(pack.Name, manifest.Name, StringComparison.OrdinalIgnoreCase));

        foreach (var (typeId, nodes) in file.Content.Objects)
        {
            if (_content.FindType(typeId) is not { } type)
            {
                warnings.Add($"Вид объектов «{typeId}» приложению неизвестен — {nodes.Count} шт. будут пропущены.");
                continue;
            }

            sections.Add(new ExtensionSection(typeId, type.DisplayName, nodes.Count));
        }

        AddSection(sections, ExtensionSections.Aliases, "Псевдонимы", file.Content.Aliases.Count);
        AddSection(sections, ExtensionSections.Rules, "Правила", file.Content.Rules.Count);
        AddSection(sections, ExtensionSections.Macros, "Макросы", file.Content.Macros.Count);

        if (sections.Count == 0 && file.Content.GameSystem is null)
        {
            problems.Add("Расширение пустое: устанавливать нечего.");
        }

        warnings.AddRange(await FindConflictsAsync(context, file, replaced?.Id, cancellationToken)
            .ConfigureAwait(false));

        return new ExtensionPreview(manifest, sections, problems, warnings, replaced?.Version);
    }

    /// <summary>
    /// Ищет расширения, без которых устанавливаемое не работает.
    /// </summary>
    /// <param name="manifest">Описание расширения.</param>
    /// <param name="installed">Установленные расширения.</param>
    /// <returns>Описания недостающих зависимостей.</returns>
    private static IEnumerable<string> FindMissingDependencies(
        ExtensionManifest manifest,
        IReadOnlyList<ContentPack> installed)
    {
        foreach (var dependency in manifest.Dependencies)
        {
            var found = installed.FirstOrDefault(pack =>
                string.Equals(pack.Name, dependency.Name, StringComparison.OrdinalIgnoreCase));

            if (found is null)
            {
                yield return $"Нужно расширение «{dependency.Summary}», а оно не установлено.";
                continue;
            }

            if (!ExtensionPackage.Satisfies(found.Version, dependency.Version))
            {
                yield return
                    $"Расширение «{dependency.Name}» установлено версии {found.Version}, "
                    + $"а нужна не ниже {dependency.Version}.";
            }
        }
    }

    /// <summary>
    /// Ищет столкновения внутренних имён с уже имеющимися объектами.
    ///
    /// Внутреннее имя — то, которым объект называют формулы и правила, и оно
    /// уникально в пределах игровой системы. Столкновение означает, что одно
    /// из двух определений придётся выбросить, и знать об этом нужно заранее.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="file">Прочитанный файл.</param>
    /// <param name="replacedPackId">Заменяемое расширение, объекты которого будут убраны.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Описания столкновений.</returns>
    private async Task<IReadOnlyList<string>> FindConflictsAsync(
        RpgDbContext context,
        PackageFile file,
        Guid? replacedPackId,
        CancellationToken cancellationToken)
    {
        var conflicts = new List<string>();

        if (ReadGameSystemId(file.Content.GameSystem) is not { } gameSystemId)
        {
            return conflicts;
        }

        foreach (var (typeId, nodes) in file.Content.Objects)
        {
            if (_content.FindType(typeId) is not { } type)
            {
                continue;
            }

            var names = nodes
                .Select(node => node?[nameof(ContentEntity.SystemName)]?.GetValue<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList();

            if (names.Count == 0)
            {
                continue;
            }

            var existing = await _content
                .GetOwnedAsync(typeId, new ContentOwner(gameSystemId), cancellationToken)
                .ConfigureAwait(false);

            var taken = existing
                .OfType<ContentEntity>()

                // Объекты заменяемого расширения столкновением не считаются:
                // установка уберёт их прежде, чем положит новые. Когда заменять
                // нечего, из проверки не исключается ничего.
                .Where(entity => replacedPackId is null || entity.ContentPackId != replacedPackId)
                .Where(entity => names.Contains(entity.SystemName, StringComparer.OrdinalIgnoreCase))
                .Select(entity => entity.Name)
                .ToList();

            if (taken.Count > 0)
            {
                conflicts.Add(
                    $"{type.DisplayName}: имена уже заняты — {string.Join(", ", taken.Take(5))}"
                    + (taken.Count > 5 ? $" и ещё {taken.Count - 5}." : "."));
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);

        return conflicts;
    }

    /// <summary>
    /// Создаёт или обновляет запись расширения и убирает прежнее содержимое.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="manifest">Описание расширения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор записи расширения.</returns>
    private async Task<Guid> PrepareAsync(
        RpgDbContext context,
        ExtensionManifest manifest,
        CancellationToken cancellationToken)
    {
        // Сравнение названий выполняется в памяти: установленных расширений
        // единицы, а сопоставление без учёта регистра база данных проводит
        // по своим правилам, отличным от правил приложения.
        var packs = await context.ContentPacks
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var pack = packs.FirstOrDefault(item =>
            string.Equals(item.Name, manifest.Name, StringComparison.OrdinalIgnoreCase));

        if (pack is null)
        {
            pack = new ContentPack { Name = manifest.Name };
            context.ContentPacks.Add(pack);
        }
        else
        {
            // Установка поверх прежней версии: старое содержимое убирается целиком,
            // иначе в базе остались бы два набора одного и того же расширения.
            await ClearContentAsync(pack.Id, cancellationToken).ConfigureAwait(false);
        }

        pack.Version = manifest.Version;
        pack.Author = manifest.Author;
        pack.Description = manifest.Description;
        pack.License = manifest.License;
        pack.RequiredVersion = manifest.RequiredVersion;
        pack.Enabled = true;
        pack.DependenciesJson = manifest.Dependencies.Count == 0
            ? null
            : JsonSerializer.Serialize(manifest.Dependencies, ExtensionPackageFile.Options);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return pack.Id;
    }

    /// <summary>
    /// Записывает содержимое расширения в базу данных.
    /// </summary>
    /// <param name="packId">Идентификатор записи расширения.</param>
    /// <param name="file">Прочитанный файл.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество установленных объектов либо описание ошибки.</returns>
    private async Task<Result<int>> WriteContentAsync(
        Guid packId,
        PackageFile file,
        CancellationToken cancellationToken)
    {
        var gameSystemId = await InstallGameSystemAsync(file.Content.GameSystem, cancellationToken)
            .ConfigureAwait(false);

        var installed = 0;

        foreach (var (typeId, nodes) in file.Content.Objects)
        {
            if (_content.FindType(typeId) is not { } type)
            {
                continue;
            }

            var entities = new List<EntityBase>(nodes.Count);

            foreach (var node in nodes)
            {
                if (node.Deserialize(type.EntityType, ExtensionPackageFile.Options) is not EntityBase entity)
                {
                    continue;
                }

                Attach(entity, packId, gameSystemId);
                entities.Add(entity);
            }

            var saved = await _content.SaveManyAsync(typeId, entities, cancellationToken).ConfigureAwait(false);

            if (saved.IsFailure)
            {
                return Result.Failure<int>(saved.Error!);
            }

            installed += entities.Count;
        }

        installed += await InstallAliasesAsync(packId, gameSystemId, file.Content.Aliases, cancellationToken)
            .ConfigureAwait(false);

        installed += await InstallRulesAndMacrosAsync(packId, gameSystemId, file, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(installed);
    }

    /// <summary>
    /// Устанавливает игровую систему расширения.
    /// </summary>
    /// <param name="node">Записанная система.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор системы либо <see langword="null"/>.</returns>
    private async Task<Guid?> InstallGameSystemAsync(JsonNode? node, CancellationToken cancellationToken)
    {
        if (node.Deserialize<GameSystem>(ExtensionPackageFile.Options) is not { } system)
        {
            return null;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Система разыскивается и по идентификатору, и по внутреннему имени:
        // повторная установка должна попасть в ту же запись, а не создать вторую
        // систему с тем же именем, которое база всё равно не примет дважды.
        var existing = await context.GameSystems
            .FirstOrDefaultAsync(
                item => item.Id == system.Id || item.SystemName == system.SystemName,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            context.GameSystems.Add(system);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return system.Id;
        }

        existing.Name = system.Name;
        existing.Version = system.Version;
        existing.Author = system.Author;
        existing.Description = system.Description;
        existing.Icon = system.Icon;
        existing.CarryCapacityFormula = system.CarryCapacityFormula;
        existing.WeightUnit = system.WeightUnit;
        existing.KnownSpellsFormula = system.KnownSpellsFormula;
        existing.PreparedSpellsFormula = system.PreparedSpellsFormula;
        existing.InitiativeFormula = system.InitiativeFormula;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return existing.Id;
    }

    /// <summary>Устанавливает дополнительные имена без копирования целевых объектов.</summary>
    private async Task<int> InstallAliasesAsync(
        Guid packId,
        Guid? gameSystemId,
        IReadOnlyCollection<PackageAlias> packageAliases,
        CancellationToken cancellationToken)
    {
        var aliases = packageAliases
            .Where(alias =>
                !string.IsNullOrWhiteSpace(alias.ContentTypeId)
                && !string.IsNullOrWhiteSpace(alias.TargetSystemName)
                && !string.IsNullOrWhiteSpace(alias.Alias)
                && _content.FindType(alias.ContentTypeId.Trim()) is not null)
            .Select(alias => new ContentAlias
            {
                ContentTypeId = alias.ContentTypeId!.Trim(),
                TargetSystemName = alias.TargetSystemName!.Trim(),
                Alias = alias.Alias!.Trim(),
                ContentPackId = packId,
                GameSystemId = gameSystemId,
            })
            .GroupBy(alias => new
            {
                alias.ContentTypeId,
                alias.TargetSystemName,
                NormalizedAlias = alias.Alias.ToUpperInvariant(),
            })
            .Select(group => group.First())
            .ToList();

        if (aliases.Count == 0)
        {
            return 0;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        context.ContentAliases.AddRange(aliases);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return aliases.Count;
    }

    /// <summary>
    /// Устанавливает правила и макросы расширения.
    /// </summary>
    /// <param name="packId">Идентификатор записи расширения.</param>
    /// <param name="gameSystemId">Игровая система расширения.</param>
    /// <param name="file">Прочитанный файл.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество установленных объектов.</returns>
    private async Task<int> InstallRulesAndMacrosAsync(
        Guid packId,
        Guid? gameSystemId,
        PackageFile file,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var rules = Read<GameRule>(file.Content.Rules);
        var macros = Read<Macro>(file.Content.Macros);

        // Объект, который в базе уже есть, остаётся как есть: его идентификатор
        // занят чем-то другим — прежнее содержимое этого расширения к этому
        // времени уже убрано.
        var takenRules = await ExistingAsync(context.Rules, rules, cancellationToken).ConfigureAwait(false);
        var takenMacros = await ExistingAsync(context.Macros, macros, cancellationToken).ConfigureAwait(false);

        var installed = 0;

        foreach (var rule in rules.Where(rule => !takenRules.Contains(rule.Id)))
        {
            Attach(rule, packId, gameSystemId);

            // Правило расширения принадлежит игровой системе, а не персонажу
            // автора и не его кампании.
            rule.CharacterId = null;
            rule.CampaignId = null;

            context.Rules.Add(rule);
            installed++;
        }

        foreach (var macro in macros.Where(macro => !takenMacros.Contains(macro.Id)))
        {
            Attach(macro, packId, gameSystemId);
            macro.CharacterId = null;

            context.Macros.Add(macro);
            installed++;
        }

        if (installed > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return installed;
    }

    /// <summary>
    /// Восстанавливает объекты из записей пакета, пропуская нечитаемые.
    /// </summary>
    /// <typeparam name="TEntity">Тип объекта.</typeparam>
    /// <param name="nodes">Записи пакета.</param>
    /// <returns>Восстановленные объекты.</returns>
    private static List<TEntity> Read<TEntity>(IReadOnlyList<JsonNode> nodes)
        where TEntity : EntityBase =>
    [
        .. nodes
            .Select(node => node.Deserialize<TEntity>(ExtensionPackageFile.Options))
            .Where(entity => entity is not null)
            .Select(entity => entity!),
    ];

    /// <summary>
    /// Возвращает идентификаторы объектов, которые в базе данных уже есть.
    /// </summary>
    /// <typeparam name="TEntity">Тип объекта.</typeparam>
    /// <param name="set">Набор объектов базы данных.</param>
    /// <param name="entities">Устанавливаемые объекты.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Занятые идентификаторы.</returns>
    private static async Task<HashSet<Guid>> ExistingAsync<TEntity>(
        DbSet<TEntity> set,
        IReadOnlyList<TEntity> entities,
        CancellationToken cancellationToken)
        where TEntity : EntityBase
    {
        if (entities.Count == 0)
        {
            return [];
        }

        var keys = entities.Select(entity => entity.Id).ToList();

        var found = await set
            .AsNoTracking()
            .Where(entity => keys.Contains(entity.Id))
            .Select(entity => entity.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. found];
    }

    /// <summary>
    /// Отмечает принадлежность объекта расширению и его игровой системе.
    /// </summary>
    /// <param name="entity">Устанавливаемый объект.</param>
    /// <param name="packId">Идентификатор записи расширения.</param>
    /// <param name="gameSystemId">Игровая система расширения.</param>
    private static void Attach(EntityBase entity, Guid packId, Guid? gameSystemId)
    {
        if (entity is not ContentEntity content)
        {
            return;
        }

        content.ContentPackId = packId;

        // Связи заполняются идентификаторами: сами объекты в пакете не записаны,
        // и оставленная ссылка заставила бы базу создать их заново.
        content.ContentPack = null;
        content.GameSystem = null;

        if (gameSystemId is { } id)
        {
            content.GameSystemId = id;
        }
    }

    /// <summary>
    /// Убирает содержимое расширения, оставляя саму запись.
    /// </summary>
    /// <param name="packId">Идентификатор записи расширения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество удалённых объектов.</returns>
    private async Task<int> ClearContentAsync(Guid packId, CancellationToken cancellationToken)
    {
        var owner = new ContentOwner(ContentPackId: packId);
        var removed = 0;

        foreach (var type in PackedTypes)
        {
            removed += await _content.DeleteOwnedAsync(type.Id, owner, cancellationToken).ConfigureAwait(false);
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        removed += await context.ContentAliases
            .Where(alias => alias.ContentPackId == packId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        removed += await context.Rules
            .Where(rule => rule.ContentPackId == packId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        removed += await context.Macros
            .Where(macro => macro.ContentPackId == packId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return removed;
    }

    /// <summary>
    /// Отменяет неудавшуюся установку.
    /// </summary>
    /// <param name="packId">Идентификатор записи расширения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после отмены.</returns>
    private async Task RollbackAsync(Guid packId, CancellationToken cancellationToken)
    {
        await ClearContentAsync(packId, cancellationToken).ConfigureAwait(false);

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await context.ContentPacks
            .Where(pack => pack.Id == packId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Читает установленное расширение.
    /// </summary>
    /// <param name="packId">Идентификатор записи расширения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Расширение либо описание ошибки.</returns>
    private async Task<Result<ExtensionItem>> LoadItemAsync(Guid packId, CancellationToken cancellationToken)
    {
        var all = await GetAllAsync(cancellationToken).ConfigureAwait(false);

        if (all.IsFailure)
        {
            return Result.Failure<ExtensionItem>(all.Error!);
        }

        return all.Value.FirstOrDefault(item => item.Id == packId) is { } found
            ? Result.Success(found)
            : Result.Failure<ExtensionItem>("Расширение не найдено: возможно, оно уже удалено.");
    }

    /// <summary>
    /// Собирает сведения об установленном расширении.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="pack">Запись расширения.</param>
    /// <param name="installed">Все установленные расширения.</param>
    /// <param name="systems">Названия игровых систем.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Установленное расширение.</returns>
    private async Task<ExtensionItem> BuildItemAsync(
        RpgDbContext context,
        ContentPack pack,
        IReadOnlyList<ContentPack> installed,
        Dictionary<Guid, string> systems,
        CancellationToken cancellationToken)
    {
        var dependencies = ReadDependencies(pack.DependenciesJson);

        var manifest = new ExtensionManifest(
            pack.Name,
            pack.Version,
            pack.Author,
            pack.Description,
            pack.License,
            pack.GameSystemId is { } systemId && systems.TryGetValue(systemId, out var name) ? name : null,
            pack.RequiredVersion,
            pack.CreatedAt)
        {
            Dependencies = dependencies,
        };

        var owner = new ContentOwner(ContentPackId: pack.Id);
        var count = 0;

        foreach (var type in PackedTypes)
        {
            count += await _content.CountOwnedAsync(type.Id, owner, cancellationToken).ConfigureAwait(false);
        }

        count += await context.Rules
            .CountAsync(rule => rule.ContentPackId == pack.Id, cancellationToken)
            .ConfigureAwait(false);

        count += await context.Macros
            .CountAsync(macro => macro.ContentPackId == pack.Id, cancellationToken)
            .ConfigureAwait(false);

        var problems = new List<string>();

        if (!ExtensionPackage.Satisfies(ApplicationConstants.Version, pack.RequiredVersion))
        {
            problems.Add(
                $"Нужна версия приложения не ниже {pack.RequiredVersion}, "
                + $"а установлена {ApplicationConstants.Version}.");
        }

        problems.AddRange(FindMissingDependencies(manifest, installed));

        problems.AddRange(installed
            .Where(other => other.Id != pack.Id && !other.Enabled)
            .Where(other => dependencies.Any(dependency =>
                string.Equals(dependency.Name, other.Name, StringComparison.OrdinalIgnoreCase)))
            .Select(other => $"Расширение «{other.Name}», без которого это не работает, отключено."));

        var state = !pack.Enabled
            ? ExtensionState.Disabled
            : problems.Count == 0
                ? ExtensionState.Active
                : ExtensionPackage.Satisfies(ApplicationConstants.Version, pack.RequiredVersion)
                    ? ExtensionState.MissingDependency
                    : ExtensionState.Incompatible;

        return new ExtensionItem(pack.Id, manifest, state, pack.CreatedAt, count, problems);
    }

    /// <summary>
    /// Читает зависимости расширения из записи базы данных.
    /// </summary>
    /// <param name="json">Записанные зависимости.</param>
    /// <returns>Зависимости расширения.</returns>
    private static List<ExtensionDependency> ReadDependencies(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ExtensionDependency>>(json, ExtensionPackageFile.Options) ?? [];
        }
        catch (JsonException)
        {
            // Испорченный список зависимостей не должен прятать само расширение:
            // без него оно просто считается ни от чего не зависящим.
            return [];
        }
    }

    /// <summary>
    /// Читает идентификатор игровой системы из записанного объекта.
    /// </summary>
    /// <param name="node">Записанная система.</param>
    /// <returns>Идентификатор либо <see langword="null"/>.</returns>
    private static Guid? ReadGameSystemId(JsonNode? node) =>
        node?[nameof(EntityBase.Id)]?.GetValue<Guid>();
}
