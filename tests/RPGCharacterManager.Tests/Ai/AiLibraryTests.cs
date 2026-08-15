using System.Text;
using RPGCharacterManager.Ai;
using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Tests.Import;

namespace RPGCharacterManager.Tests.Ai;

/// <summary>
/// Пути пользовательских данных во временном каталоге теста.
/// </summary>
internal sealed class TemporaryPaths : IAppPathService, IDisposable
{
    public TemporaryPaths()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "rpgcm-ai-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(ContentDirectory);
    }

    public string DataDirectory { get; }

    public string LogsDirectory => Path.Combine(DataDirectory, "logs");

    public string BackupsDirectory => Path.Combine(DataDirectory, "backups");

    public string ContentDirectory => Path.Combine(DataDirectory, "content");

    public string DatabaseFilePath => Path.Combine(DataDirectory, "rpgmanager.db");

    public string SettingsFilePath => Path.Combine(DataDirectory, "settings.json");

    public void EnsureDirectoriesExist() => Directory.CreateDirectory(ContentDirectory);

    public void Dispose()
    {
        if (Directory.Exists(DataDirectory))
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
    }
}

/// <summary>
/// Проверка библиотеки книг помощника.
/// </summary>
public sealed class AiLibraryTests
{
    private static string WriteBook(AiLibrary library, string name, string text, Encoding? encoding = null)
    {
        Directory.CreateDirectory(library.Directory);

        var path = Path.Combine(library.Directory, name);

        File.WriteAllText(path, text, encoding ?? new UTF8Encoding(false));

        return path;
    }

    [Fact]
    public void Книги_ПустойКаталог_СоздаётсяИОстаётсяПустым()
    {
        using var paths = new TemporaryPaths();
        var library = new AiLibrary(paths, ImportTestFactory.CreateService());

        Assert.Empty(library.GetBooks());
        Assert.True(Directory.Exists(library.Directory));
    }

    [Fact]
    public void Книги_ФайлыВКаталоге_ПопадаютВСписок()
    {
        using var paths = new TemporaryPaths();
        var library = new AiLibrary(paths, ImportTestFactory.CreateService());

        WriteBook(library, "Правила киберпанка.md", "Моно-катана: урон 2d6.");
        WriteBook(library, "Заметки.txt", "Ничего важного.");

        var books = library.GetBooks();

        Assert.Equal(2, books.Count);
        Assert.Contains(books, book => book.Name == "Правила киберпанка");
        Assert.All(books, book => Assert.True(book.Size > 0));
    }

    [Fact]
    public void Книги_ЧужиеРасширения_НеПопадаютВСписок()
    {
        using var paths = new TemporaryPaths();
        var library = new AiLibrary(paths, ImportTestFactory.CreateService());

        WriteBook(library, "Обложка.png", "не документ");
        WriteBook(library, "Правила.md", "Моно-катана: урон 2d6.");

        var book = Assert.Single(library.GetBooks());

        Assert.Equal("Правила", book.Name);
    }

    [Fact]
    public void Книги_ПоддерживаемыеФорматы_ЗадаютсяИмпортом()
    {
        using var paths = new TemporaryPaths();
        var library = new AiLibrary(paths, ImportTestFactory.CreateService());

        // Библиотека не знает форматов: их перечень целиком приходит из импорта,
        // поэтому новый формат становится доступен помощнику сам собой.
        Assert.Contains(".pdf", library.SupportedExtensions);
        Assert.Contains(".docx", library.SupportedExtensions);
        Assert.Contains(".sqlite", library.SupportedExtensions);
    }

    [Fact]
    public async Task Чтение_Книги_ВозвращаетТекст()
    {
        using var paths = new TemporaryPaths();
        var library = new AiLibrary(paths, ImportTestFactory.CreateService());

        WriteBook(library, "Правила.md", "Моно-катана: урон 2d6.");

        var book = Assert.Single(library.GetBooks());
        var source = await library.ReadAsync(book);

        Assert.True(source.IsSuccess, source.Error);
        Assert.Equal("Правила", source.Value.Name);
        Assert.Contains("Моно-катана", source.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Чтение_ОднобайтовойКириллицы_ДаётЧитаемыйТекст()
    {
        using var paths = new TemporaryPaths();
        var library = new AiLibrary(paths, ImportTestFactory.CreateService());

        // «Правила» в кодировке Windows-1251 — так до сих пор сохраняют текст
        // многие редакторы, и отказываться от таких файлов при импорте нельзя.
        var path = Path.Combine(library.Directory, "Старая книга.txt");

        Directory.CreateDirectory(library.Directory);
        await File.WriteAllBytesAsync(path, [0xCF, 0xF0, 0xE0, 0xE2, 0xE8, 0xEB, 0xE0]);

        var book = Assert.Single(library.GetBooks());
        var source = await library.ReadAsync(book);

        Assert.True(source.IsSuccess, source.Error);
        Assert.Equal("Правила", source.Value.Text);
    }

    [Fact]
    public async Task Чтение_ИсчезнувшегоФайла_СообщаетОбОшибке()
    {
        using var paths = new TemporaryPaths();
        var library = new AiLibrary(paths, ImportTestFactory.CreateService());

        var source = await library.ReadAsync(
            new AiBook("Нет такой", Path.Combine(library.Directory, "нет.md"), 10));

        Assert.True(source.IsFailure);
    }
}
