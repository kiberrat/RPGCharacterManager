using Microsoft.EntityFrameworkCore;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Abstractions.Extensions;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Tests.Extensions;

/// <summary>
/// Проверка расширений: выгрузка игровой системы в файл, установка, обновление,
/// удаление и проверки совместимости, зависимостей и столкновений имён.
/// </summary>
public sealed class ExtensionServiceTests
{
    private static ExtensionExportRequest Export(
        ExtensionTestContext context,
        string fileName,
        Guid gameSystemId,
        ExtensionManifest? manifest = null) =>
        new(
            context.PathFor(fileName),
            manifest ?? new ExtensionManifest("Тёмное фэнтези", "1.0", "Автор", "Мрачный мир."),
            gameSystemId);

    /// <summary>
    /// Создаёт игровую систему с расой, заклинанием, правилом и макросом.
    /// </summary>
    /// <param name="context">Окружение теста.</param>
    /// <returns>Созданная игровая система.</returns>
    private static async Task<GameSystem> FillAsync(ExtensionTestContext context)
    {
        var system = ExtensionTestContext.System("Тьма");
        await context.AddAsync(system);

        await context.AddAsync(ExtensionTestContext.Race("Дроу", system.Id));
        await context.AddAsync(ExtensionTestContext.Spell("Тёмное пламя", system.Id, level: 2));
        await context.AddAsync(ExtensionTestContext.Rule("Кровавая жатва", system.Id));
        await context.AddAsync(ExtensionTestContext.Macro("Призыв тьмы", system.Id));

        return system;
    }

    // ---------- Выгрузка ----------

    [Fact]
    public async Task Выгрузка_СобираетВсёСодержимоеИгровойСистемы()
    {
        await using var context = await ExtensionTestContext.CreateAsync();

        var system = await FillAsync(context);
        var result = await context.Service.ExportAsync(Export(context, "тьма", system.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(File.Exists(result.Value.Path));
        Assert.True(result.Value.SizeInBytes > 0);

        // По одному объекту каждого вида: раса, заклинание, правило, макрос.
        Assert.Equal(4, result.Value.ObjectCount);

        Assert.Contains(result.Value.Sections, section => section.TypeId == ContentTypeIds.Races);
        Assert.Contains(result.Value.Sections, section => section.TypeId == ContentTypeIds.Spells);
        Assert.Contains(result.Value.Sections, section => section.TypeId == ExtensionSections.Rules);
        Assert.Contains(result.Value.Sections, section => section.TypeId == ExtensionSections.Macros);
    }

    [Fact]
    public async Task Выгрузка_БезИсточника_НеВыполняется()
    {
        await using var context = await ExtensionTestContext.CreateAsync();

        var request = new ExtensionExportRequest(
            context.PathFor("пусто"), new ExtensionManifest("Ничто"));

        Assert.True((await context.Service.ExportAsync(request)).IsFailure);
    }

    // ---------- Разбор ----------

    [Fact]
    public async Task Разбор_ПоказываетСоставДоУстановки()
    {
        await using var context = await ExtensionTestContext.CreateAsync();

        var system = await FillAsync(context);
        var exported = await context.Service.ExportAsync(Export(context, "тьма", system.Id));

        Assert.True(exported.IsSuccess, exported.Error);

        var preview = await context.Service.InspectAsync(exported.Value.Path);

        Assert.True(preview.IsSuccess, preview.Error);
        Assert.True(preview.Value.CanInstall);
        Assert.False(preview.Value.IsUpdate);
        Assert.Equal("Тёмное фэнтези", preview.Value.Manifest.Name);
        Assert.Equal(4, preview.Value.ObjectCount);
    }

    [Fact]
    public async Task Разбор_ЧужогоФайла_СообщаетЧтоЭтоНеРасширение()
    {
        await using var context = await ExtensionTestContext.CreateAsync();

        var path = context.PathFor("не-расширение");
        await File.WriteAllTextAsync(path, "просто текст");

        var preview = await context.Service.InspectAsync(path);

        Assert.True(preview.IsFailure);
        Assert.Contains("не является расширением", preview.Error!, StringComparison.CurrentCulture);
    }

    // ---------- Установка ----------

    [Fact]
    public async Task Установка_ПереноситСодержимоеВДругуюБазу()
    {
        await using var source = await ExtensionTestContext.CreateAsync();
        await using var target = await ExtensionTestContext.CreateAsync();

        var system = await FillAsync(source);
        var exported = await source.Service.ExportAsync(Export(source, "тьма", system.Id));

        Assert.True(exported.IsSuccess, exported.Error);

        var installed = await target.Service.InstallAsync(exported.Value.Path);

        Assert.True(installed.IsSuccess, installed.Error);
        Assert.Equal(4, installed.Value.ObjectCount);
        Assert.Equal(ExtensionState.Active, installed.Value.State);

        await using var database = await target.CreateContextAsync();

        // Игровая система переехала вместе с содержимым: без неё формулы
        // и правила ссылались бы в пустоту.
        var moved = await database.GameSystems.SingleAsync();

        Assert.Equal("Тьма", moved.Name);
        Assert.Equal("Сила * 10", moved.CarryCapacityFormula);

        var race = await database.Races.SingleAsync();

        Assert.Equal("Дроу", race.Name);
        Assert.Equal(moved.Id, race.GameSystemId);
        Assert.Equal(installed.Value.Id, race.ContentPackId);

        Assert.Equal(2, (await database.Spells.SingleAsync()).Level);
        Assert.Equal("Кровавая жатва", (await database.Rules.SingleAsync()).Name);
        Assert.Equal("Призыв тьмы", (await database.Macros.SingleAsync()).Name);
    }

    [Fact]
    public async Task Установка_ПоверхПрежнейВерсии_ЗаменяетСодержимое()
    {
        await using var source = await ExtensionTestContext.CreateAsync();
        await using var target = await ExtensionTestContext.CreateAsync();

        var system = await FillAsync(source);

        var first = await source.Service.ExportAsync(Export(source, "первая", system.Id));
        Assert.True(first.IsSuccess, first.Error);
        Assert.True((await target.Service.InstallAsync(first.Value.Path)).IsSuccess);

        // Вторая версия расширения: раса переименована, заклинание убрано.
        await using (var database = await source.CreateContextAsync())
        {
            var race = await database.Races.SingleAsync();
            race.Name = "Дуэргар";

            database.Spells.RemoveRange(database.Spells);

            await database.SaveChangesAsync();
        }

        var second = await source.Service.ExportAsync(Export(
            source,
            "вторая",
            system.Id,
            new ExtensionManifest("Тёмное фэнтези", "2.0")));

        Assert.True(second.IsSuccess, second.Error);

        var preview = await target.Service.InspectAsync(second.Value.Path);

        Assert.True(preview.IsSuccess, preview.Error);
        Assert.True(preview.Value.IsUpdate);
        Assert.Equal("1.0", preview.Value.ReplacesVersion);

        var updated = await target.Service.InstallAsync(second.Value.Path);

        Assert.True(updated.IsSuccess, updated.Error);
        Assert.Equal("2.0", updated.Value.Manifest.Version);

        await using var check = await target.CreateContextAsync();

        // Прежнее содержимое убрано целиком: два набора одного расширения
        // в базе остаться не должны.
        Assert.Single(await check.ContentPacks.ToListAsync());
        Assert.Equal("Дуэргар", (await check.Races.SingleAsync()).Name);
        Assert.Empty(await check.Spells.ToListAsync());
    }

    [Fact]
    public async Task Установка_ТребующаяНовойВерсииПриложения_НеВыполняется()
    {
        await using var source = await ExtensionTestContext.CreateAsync();
        await using var target = await ExtensionTestContext.CreateAsync();

        var system = await FillAsync(source);

        var exported = await source.Service.ExportAsync(Export(
            source,
            "будущее",
            system.Id,
            new ExtensionManifest("Из будущего", "1.0", RequiredVersion: "99.0")));

        Assert.True(exported.IsSuccess, exported.Error);

        var preview = await target.Service.InspectAsync(exported.Value.Path);

        Assert.True(preview.IsSuccess, preview.Error);
        Assert.False(preview.Value.CanInstall);
        Assert.Contains(preview.Value.Problems, problem =>
            problem.Contains("99.0", StringComparison.Ordinal));

        Assert.True((await target.Service.InstallAsync(exported.Value.Path)).IsFailure);
    }

    [Fact]
    public async Task Установка_БезТребуемогоРасширения_НеВыполняется()
    {
        await using var source = await ExtensionTestContext.CreateAsync();
        await using var target = await ExtensionTestContext.CreateAsync();

        var system = await FillAsync(source);

        var manifest = new ExtensionManifest("Оружие киберпанка", "1.0")
        {
            Dependencies = [new ExtensionDependency("Киберпанк: ядро", "1.2")],
        };

        var exported = await source.Service.ExportAsync(Export(source, "оружие", system.Id, manifest));
        Assert.True(exported.IsSuccess, exported.Error);

        var preview = await target.Service.InspectAsync(exported.Value.Path);

        Assert.True(preview.IsSuccess, preview.Error);
        Assert.False(preview.Value.CanInstall);
        Assert.Contains(preview.Value.Problems, problem =>
            problem.Contains("Киберпанк: ядро", StringComparison.CurrentCulture));
    }

    [Fact]
    public async Task Разбор_СтолкновениеИмён_Предупреждает()
    {
        await using var source = await ExtensionTestContext.CreateAsync();
        await using var target = await ExtensionTestContext.CreateAsync();

        var system = await FillAsync(source);
        var exported = await source.Service.ExportAsync(Export(source, "тьма", system.Id));

        Assert.True(exported.IsSuccess, exported.Error);

        // В принимающей базе уже есть своя раса с тем же внутренним именем
        // в той же игровой системе: формулы не смогли бы различить их.
        var existing = ExtensionTestContext.System("Тьма");
        existing.Id = system.Id;

        await target.AddAsync(existing);
        await target.AddAsync(new Race
        {
            Name = "Тёмный эльф",
            SystemName = "дроу",
            GameSystemId = existing.Id,
        });

        var preview = await target.Service.InspectAsync(exported.Value.Path);

        Assert.True(preview.IsSuccess, preview.Error);
        Assert.NotEmpty(preview.Value.Warnings);
    }

    [Fact]
    public async Task Псевдонимы_ПереносятсяПакетомИНаходятРусскоеЗаклинание()
    {
        await using var source = await ExtensionTestContext.CreateAsync();
        await using var target = await ExtensionTestContext.CreateAsync();

        var system = ExtensionTestContext.System("D&D 5e");
        var sourcePack = new ContentPack
        {
            Name = "Английские названия",
            Version = "1.0",
            GameSystemId = system.Id,
            Enabled = true,
        };
        var spell = new Spell
        {
            Name = "Узилище",
            SystemName = "узилище",
            GameSystemId = system.Id,
            Level = 7,
        };
        var alias = new ContentAlias
        {
            ContentTypeId = ContentTypeIds.Spells,
            TargetSystemName = spell.SystemName,
            Alias = "Forcecage",
            GameSystemId = system.Id,
            ContentPackId = sourcePack.Id,
        };

        await source.AddAsync(system);
        await source.AddAsync(sourcePack);
        await source.AddAsync(spell);
        await source.AddAsync(alias);

        var exported = await source.Service.ExportAsync(Export(source, "english-names", system.Id));
        Assert.True(exported.IsSuccess, exported.Error);
        Assert.Contains(exported.Value.Sections, section => section.TypeId == ExtensionSections.Aliases);

        var installed = await target.Service.InstallAsync(exported.Value.Path);
        Assert.True(installed.IsSuccess, installed.Error);

        var found = await target.Content.SearchAsync(ContentTypeIds.Spells, "forceCAGE", 0, 20);
        Assert.Equal("Узилище", Assert.Single(found.Items).Name);

        var disabled = await target.Service.SetEnabledAsync(installed.Value.Id, false);
        Assert.True(disabled.IsSuccess, disabled.Error);
        Assert.Empty((await target.Content.SearchAsync(ContentTypeIds.Spells, "Forcecage", 0, 20)).Items);
    }

    // ---------- Состояние и удаление ----------

    [Fact]
    public async Task Отключение_МеняетСостояниеРасширения()
    {
        await using var source = await ExtensionTestContext.CreateAsync();
        await using var target = await ExtensionTestContext.CreateAsync();

        var system = await FillAsync(source);
        var exported = await source.Service.ExportAsync(Export(source, "тьма", system.Id));

        Assert.True(exported.IsSuccess, exported.Error);

        var installed = await target.Service.InstallAsync(exported.Value.Path);
        Assert.True(installed.IsSuccess, installed.Error);

        var disabled = await target.Service.SetEnabledAsync(installed.Value.Id, false);

        Assert.True(disabled.IsSuccess, disabled.Error);
        Assert.Equal(ExtensionState.Disabled, disabled.Value.State);
        Assert.False(disabled.Value.IsEnabled);

        // Содержимое остаётся на месте: отключение — не удаление.
        Assert.Equal(4, disabled.Value.ObjectCount);

        var enabled = await target.Service.SetEnabledAsync(installed.Value.Id, true);

        Assert.True(enabled.IsSuccess, enabled.Error);
        Assert.Equal(ExtensionState.Active, enabled.Value.State);
    }

    [Fact]
    public async Task Удаление_УбираетТолькоСодержимоеРасширения()
    {
        await using var source = await ExtensionTestContext.CreateAsync();
        await using var target = await ExtensionTestContext.CreateAsync();

        var system = await FillAsync(source);
        var exported = await source.Service.ExportAsync(Export(source, "тьма", system.Id));

        Assert.True(exported.IsSuccess, exported.Error);

        var installed = await target.Service.InstallAsync(exported.Value.Path);
        Assert.True(installed.IsSuccess, installed.Error);

        // Своя раса пользователя расширению не принадлежит и остаться должна.
        await target.AddAsync(ExtensionTestContext.Race("Своя раса"));

        var removed = await target.Service.RemoveAsync(installed.Value.Id);

        Assert.True(removed.IsSuccess, removed.Error);
        Assert.Equal(4, removed.Value);

        await using var database = await target.CreateContextAsync();

        Assert.Empty(await database.ContentPacks.ToListAsync());
        Assert.Equal("Своя раса", (await database.Races.SingleAsync()).Name);
        Assert.Empty(await database.Rules.ToListAsync());
        Assert.Empty(await database.Macros.ToListAsync());

        // Игровая система остаётся: на неё могут ссылаться персонажи,
        // созданные до удаления расширения.
        Assert.Single(await database.GameSystems.ToListAsync());
    }

    [Fact]
    public async Task Список_ПоказываетУстановленноеРасширение()
    {
        await using var source = await ExtensionTestContext.CreateAsync();
        await using var target = await ExtensionTestContext.CreateAsync();

        var system = await FillAsync(source);

        var exported = await source.Service.ExportAsync(Export(
            source,
            "тьма",
            system.Id,
            new ExtensionManifest("Тёмное фэнтези", "1.4", "Автор", "Мрачный мир.", "Общественное достояние")));

        Assert.True(exported.IsSuccess, exported.Error);
        Assert.True((await target.Service.InstallAsync(exported.Value.Path)).IsSuccess);

        var all = await target.Service.GetAllAsync();

        Assert.True(all.IsSuccess, all.Error);

        var item = Assert.Single(all.Value);

        Assert.Equal("Тёмное фэнтези", item.Manifest.Name);
        Assert.Equal("1.4", item.Manifest.Version);
        Assert.Equal("Общественное достояние", item.Manifest.License);
        Assert.Equal("Активно", item.StateText);
        Assert.False(item.HasProblems);
    }

    [Fact]
    public async Task Источники_ПоказываютИгровыеСистемыИРасширения()
    {
        await using var context = await ExtensionTestContext.CreateAsync();

        var system = await FillAsync(context);
        var exported = await context.Service.ExportAsync(Export(context, "тьма", system.Id));

        Assert.True(exported.IsSuccess, exported.Error);
        Assert.True((await context.Service.InstallAsync(exported.Value.Path)).IsSuccess);

        var sources = await context.Service.GetSourcesAsync();

        Assert.True(sources.IsSuccess, sources.Error);
        Assert.Contains(sources.Value, source => source.IsGameSystem && source.Name == "Тьма");
        Assert.Contains(sources.Value, source => !source.IsGameSystem && source.Name == "Тёмное фэнтези");
    }

    // ---------- Формат ----------

    [Theory]
    [InlineData("1.0", "1.0", true)]
    [InlineData("1.0.0", "1.0", true)]
    [InlineData("1.0.0", "2.0", false)]
    [InlineData("2.1", "2.0+", true)]
    [InlineData("1.0", null, true)]
    [InlineData("1.0", "чепуха", true)]
    public void Версия_СравниваетсяКакНеНиже(string available, string? required, bool expected) =>
        Assert.Equal(expected, ExtensionPackage.Satisfies(available, required));
}
