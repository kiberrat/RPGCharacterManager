using System.Text;

namespace RPGCharacterManager.Tests.Import;

/// <summary>
/// Импорт документов: чтение каждого поддерживаемого формата.
///
/// Проверяется главное свойство подсистемы: файл любого формата превращается
/// в текст, по которому дальше распознаются игровые объекты. Поэтому каждый
/// разбор проверяется одинаково — по содержимому, которое должно уцелеть.
/// </summary>
public sealed class ImportServiceTests
{
    private const string Weapon = "Моно-катана";
    private const string Damage = "урон 2d6";

    [Fact]
    public void Форматы_ПокрываютСоставЭтапа()
    {
        var service = ImportTestFactory.CreateService();

        // Состав задан ROADMAP: PDF, DOCX, TXT, HTML, Markdown, JSON и SQLite.
        string[] required = [".pdf", ".docx", ".txt", ".html", ".md", ".json", ".sqlite"];

        Assert.All(required, extension => Assert.Contains(extension, service.SupportedExtensions));
        Assert.All(required, extension => Assert.True(service.CanRead("книга" + extension), extension));

        Assert.NotEmpty(service.Formats);
        Assert.All(service.Formats, format => Assert.NotEmpty(format.Extensions));
    }

    [Fact]
    public async Task Чтение_Markdown_СохраняетТекстИРазметку()
    {
        using var directory = ImportTestFactory.CreateDirectory();
        var path = directory.File("правила.md");

        await File.WriteAllTextAsync(path, $"# Оружие\n\n{Weapon}. {Damage}.", new UTF8Encoding(false));

        var document = await ImportTestFactory.CreateService().ReadAsync(path);

        Assert.True(document.IsSuccess, document.Error);
        Assert.Equal("правила", document.Value.Name);
        Assert.Contains(Weapon, document.Value.Text, StringComparison.Ordinal);

        // Заголовки Markdown помогают распознаванию и потому сохраняются.
        Assert.Contains("# Оружие", document.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Чтение_Html_УбираетРазметкуИСкрипты()
    {
        using var directory = ImportTestFactory.CreateDirectory();
        var path = directory.File("правила.html");

        await File.WriteAllTextAsync(
            path,
            "<html><head><title>Не показывать</title><style>.a{color:red}</style></head>" +
            $"<body><h1>Оружие</h1><p>{Weapon}.</p><p>{Damage}.</p>" +
            "<script>alert('не текст')</script></body></html>",
            new UTF8Encoding(false));

        var document = await ImportTestFactory.CreateService().ReadAsync(path);

        Assert.True(document.IsSuccess, document.Error);
        Assert.Contains(Weapon, document.Value.Text, StringComparison.Ordinal);
        Assert.Contains(Damage, document.Value.Text, StringComparison.Ordinal);

        // Содержимое скриптов и стилей не относится к игре и только мешает разбору.
        Assert.DoesNotContain("alert", document.Value.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("color:red", document.Value.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("<p>", document.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Чтение_Html_РаскрываетЗамещающиеПоследовательности()
    {
        using var directory = ImportTestFactory.CreateDirectory();
        var path = directory.File("правила.html");

        await File.WriteAllTextAsync(path, "<p>&laquo;Меч&raquo; &mdash; 1d8 &amp; щит</p>", new UTF8Encoding(false));

        var document = await ImportTestFactory.CreateService().ReadAsync(path);

        Assert.True(document.IsSuccess, document.Error);
        Assert.Contains("«Меч» — 1d8 & щит", document.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Чтение_Docx_ВозвращаетАбзацы()
    {
        using var directory = ImportTestFactory.CreateDirectory();
        var path = directory.File("правила.docx");

        ImportTestFactory.WriteDocx(path, "Оружие ближнего боя", $"{Weapon}. {Damage}.");

        var document = await ImportTestFactory.CreateService().ReadAsync(path);

        Assert.True(document.IsSuccess, document.Error);
        Assert.Equal("DOCX", document.Value.Format);
        Assert.Contains(Weapon, document.Value.Text, StringComparison.Ordinal);
        Assert.Contains("Оружие ближнего боя", document.Value.Text, StringComparison.Ordinal);
        Assert.Contains(document.Value.Notes, note => note.Contains("абзацев", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Чтение_Pdf_ВозвращаетТекстСтраницы()
    {
        using var directory = ImportTestFactory.CreateDirectory();
        var path = directory.File("kniga.pdf");

        // Встроенные шрифты PDF не содержат кириллицы, поэтому образец на латинице:
        // проверяется извлечение текста, а не поддержка шрифтов сторонней книги.
        ImportTestFactory.WritePdf(path, "Mono-katana", "damage 2d6");

        var document = await ImportTestFactory.CreateService().ReadAsync(path);

        Assert.True(document.IsSuccess, document.Error);
        Assert.Equal("PDF", document.Value.Format);
        Assert.Contains("Mono-katana", document.Value.Text, StringComparison.Ordinal);
        Assert.Contains("2d6", document.Value.Text, StringComparison.Ordinal);
        Assert.Contains(document.Value.Notes, note => note.Contains("страниц: 1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Чтение_Json_ПревращаетЗаписиВПеречниПолей()
    {
        using var directory = ImportTestFactory.CreateDirectory();
        var path = directory.File("оружие.json");

        await File.WriteAllTextAsync(
            path,
            $$"""
              [
                { "name": "{{Weapon}}", "damage": "2d6", "twoHanded": false },
                { "name": "Кастет", "damage": "1d4", "twoHanded": true }
              ]
              """,
            new UTF8Encoding(false));

        var document = await ImportTestFactory.CreateService().ReadAsync(path);

        Assert.True(document.IsSuccess, document.Error);
        Assert.Contains($"name: {Weapon}", document.Value.Text, StringComparison.Ordinal);
        Assert.Contains("damage: 1d4", document.Value.Text, StringComparison.Ordinal);
        Assert.Contains("twoHanded: да", document.Value.Text, StringComparison.Ordinal);

        // Скобки и кавычки занимали бы место и мешали распознаванию.
        Assert.DoesNotContain("{", document.Value.Text, StringComparison.Ordinal);
        Assert.Contains(document.Value.Notes, note => note.Contains("записей: 2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Чтение_Json_РазворачиваетВложенныеОбъекты()
    {
        using var directory = ImportTestFactory.CreateDirectory();
        var path = directory.File("заклинание.json");

        await File.WriteAllTextAsync(
            path,
            """{ "name": "Огненный шар", "effect": { "damage": "8d6", "type": "огонь" } }""",
            new UTF8Encoding(false));

        var document = await ImportTestFactory.CreateService().ReadAsync(path);

        Assert.True(document.IsSuccess, document.Error);
        Assert.Contains("effect:", document.Value.Text, StringComparison.Ordinal);
        Assert.Contains("damage: 8d6", document.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Чтение_Sqlite_ВыкладываетСтрокиТаблиц()
    {
        using var directory = ImportTestFactory.CreateDirectory();
        var path = directory.File("чужая.sqlite");

        ImportTestFactory.WriteSqlite(path, "weapons", (Weapon, "2d6"), ("Кастет", "1d4"));

        var document = await ImportTestFactory.CreateService().ReadAsync(path);

        Assert.True(document.IsSuccess, document.Error);
        Assert.Equal("SQLite", document.Value.Format);
        Assert.Contains("Таблица weapons", document.Value.Text, StringComparison.Ordinal);
        Assert.Contains($"name: {Weapon}", document.Value.Text, StringComparison.Ordinal);
        Assert.Contains("damage: 1d4", document.Value.Text, StringComparison.Ordinal);
        Assert.Contains(document.Value.Notes, note => note.Contains("weapons (2)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Чтение_ОднобайтовойКириллицы_РаспознаётКодировку()
    {
        using var directory = ImportTestFactory.CreateDirectory();
        var path = directory.File("старый.txt");

        // «Правила» в кодировке Windows-1251.
        await File.WriteAllBytesAsync(path, [0xCF, 0xF0, 0xE0, 0xE2, 0xE8, 0xEB, 0xE0]);

        var document = await ImportTestFactory.CreateService().ReadAsync(path);

        Assert.True(document.IsSuccess, document.Error);
        Assert.Equal("Правила", document.Value.Text);
        Assert.Contains(document.Value.Notes, note => note.Contains("1251", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Чтение_НеизвестногоФормата_ПеречисляетДоступные()
    {
        using var directory = ImportTestFactory.CreateDirectory();
        var path = directory.File("обложка.png");

        await File.WriteAllBytesAsync(path, [1, 2, 3]);

        var document = await ImportTestFactory.CreateService().ReadAsync(path);

        Assert.True(document.IsFailure);
        Assert.Contains(".pdf", document.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Чтение_ИспорченногоФайла_СообщаетОбОшибкеАНеПадает()
    {
        using var directory = ImportTestFactory.CreateDirectory();
        var path = directory.File("битая.docx");

        await File.WriteAllBytesAsync(path, [0x50, 0x4B, 3, 4, 9, 9, 9, 9]);

        var document = await ImportTestFactory.CreateService().ReadAsync(path);

        Assert.True(document.IsFailure);
        Assert.Contains("битая.docx", document.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Чтение_ПустогоДокумента_СообщаетЧтоТекстаНет()
    {
        using var directory = ImportTestFactory.CreateDirectory();
        var path = directory.File("пустой.txt");

        await File.WriteAllTextAsync(path, "   ", new UTF8Encoding(false));

        var document = await ImportTestFactory.CreateService().ReadAsync(path);

        Assert.True(document.IsFailure);
        Assert.Contains("не нашлось текста", document.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Чтение_ОтсутствующегоФайла_СообщаетОбЭтом()
    {
        using var directory = ImportTestFactory.CreateDirectory();

        var document = await ImportTestFactory.CreateService().ReadAsync(directory.File("нет.md"));

        Assert.True(document.IsFailure);
        Assert.Contains("не найден", document.Error!, StringComparison.Ordinal);
    }
}
