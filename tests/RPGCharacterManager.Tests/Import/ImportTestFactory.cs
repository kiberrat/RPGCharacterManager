using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Import;
using RPGCharacterManager.Import;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace RPGCharacterManager.Tests.Import;

/// <summary>
/// Сборка подсистемы импорта и создание образцов файлов каждого формата.
///
/// Образцы создаются на месте, а не хранятся в репозитории: так проверяется
/// именно чтение, а не сохранность заранее заготовленных файлов.
/// </summary>
internal static class ImportTestFactory
{
    /// <summary>
    /// Собирает службу импорта со всеми зарегистрированными чтениями.
    /// </summary>
    /// <returns>Служба импорта.</returns>
    public static IImportService CreateService()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddImport();

        return services.BuildServiceProvider().GetRequiredService<IImportService>();
    }

    /// <summary>
    /// Создаёт временный каталог, удаляемый вместе с содержимым.
    /// </summary>
    /// <returns>Временный каталог.</returns>
    public static TemporaryDirectory CreateDirectory() => new();

    /// <summary>
    /// Создаёт файл PDF с одной страницей текста.
    /// </summary>
    /// <param name="path">Путь к создаваемому файлу.</param>
    /// <param name="lines">Строки текста.</param>
    public static void WritePdf(string path, params string[] lines)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        var top = 780.0;

        foreach (var line in lines)
        {
            page.AddText(line, 12, new PdfPoint(50, top), font);
            top -= 20;
        }

        File.WriteAllBytes(path, builder.Build());
    }

    /// <summary>
    /// Создаёт файл DOCX с заданными абзацами.
    /// </summary>
    /// <param name="path">Путь к создаваемому файлу.</param>
    /// <param name="paragraphs">Абзацы документа.</param>
    public static void WriteDocx(string path, params string[] paragraphs)
    {
        const string wordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        var body = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            body.Append("<w:p><w:r><w:t xml:space=\"preserve\">")
                .Append(System.Security.SecurityElement.Escape(paragraph))
                .Append("</w:t></w:r></w:p>");
        }

        using var file = File.Create(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);

        var entry = archive.CreateEntry("word/document.xml");

        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));

        writer.Write($"<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document xmlns:w=\"{wordNamespace}\"><w:body>{body}</w:body></w:document>");
    }

    /// <summary>
    /// Создаёт базу SQLite с одной таблицей.
    /// </summary>
    /// <param name="path">Путь к создаваемому файлу.</param>
    /// <param name="table">Имя таблицы.</param>
    /// <param name="rows">Строки таблицы: название и урон.</param>
    public static void WriteSqlite(string path, string table, params (string Name, string Damage)[] rows)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using (var create = connection.CreateCommand())
        {
            create.CommandText = $"CREATE TABLE {table} (name TEXT, damage TEXT)";
            create.ExecuteNonQuery();
        }

        foreach (var row in rows)
        {
            using var insert = connection.CreateCommand();

            insert.CommandText = $"INSERT INTO {table} (name, damage) VALUES ($name, $damage)";
            insert.Parameters.AddWithValue("$name", row.Name);
            insert.Parameters.AddWithValue("$damage", row.Damage);
            insert.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();
    }
}

/// <summary>
/// Временный каталог теста, удаляемый вместе с содержимым.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "rpgcm-import-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path);
    }

    /// <summary>Полный путь к каталогу.</summary>
    public string Path { get; }

    /// <summary>
    /// Возвращает путь к файлу внутри каталога.
    /// </summary>
    /// <param name="name">Имя файла.</param>
    /// <returns>Полный путь к файлу.</returns>
    public string File(string name) => System.IO.Path.Combine(Path, name);

    /// <inheritdoc />
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
