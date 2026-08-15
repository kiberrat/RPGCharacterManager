using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Core.Abstractions.Import;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Ai;

/// <summary>
/// Документы, доступные помощнику для разбора.
///
/// Библиотека не знает ни одного формата: чтение файла выполняет подсистема
/// импорта, а помощник получает готовый текст. Поэтому книга правил в PDF,
/// выгрузка в JSON и чужая база SQLite разбираются одинаково, и новый формат
/// становится доступен помощнику в тот же момент, что и импорту.
///
/// Файлы лежат в каталоге пользовательских данных: положенный туда документ
/// появляется в списке сам. Выбрать файл в другом месте позволяет обзор файлов.
/// </summary>
public sealed class AiLibrary : IAiLibrary
{
    /// <summary>Имя каталога документов внутри каталога пользовательского контента.</summary>
    public const string FolderName = "books";

    private readonly IAppPathService _paths;
    private readonly IImportService _import;

    /// <summary>
    /// Создаёт библиотеку документов.
    /// </summary>
    /// <param name="paths">Служба путей пользовательских данных.</param>
    /// <param name="import">Служба импорта документов.</param>
    public AiLibrary(IAppPathService paths, IImportService import)
    {
        _paths = Guard.NotNull(paths);
        _import = Guard.NotNull(import);
    }

    /// <inheritdoc />
    public string Directory => Path.Combine(_paths.ContentDirectory, FolderName);

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => _import.SupportedExtensions;

    /// <inheritdoc />
    public IReadOnlyList<AiBook> GetBooks()
    {
        var directory = new DirectoryInfo(Directory);

        if (!directory.Exists)
        {
            directory.Create();

            return [];
        }

        return directory
            .EnumerateFiles()
            .Where(file => _import.CanRead(file.FullName))
            .OrderBy(file => file.Name, StringComparer.CurrentCulture)
            .Select(file => new AiBook(Path.GetFileNameWithoutExtension(file.Name), file.FullName, file.Length))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<Result<AiSource>> ReadAsync(AiBook book, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(book);

        var document = await _import.ReadAsync(book.Path, cancellationToken).ConfigureAwait(false);

        return document.IsFailure
            ? Result.Failure<AiSource>(document.Error!)
            : Result.Success(new AiSource(document.Value.Name, document.Value.Text));
    }

    /// <inheritdoc />
    public Task<Result<ImportedDocument>> InspectAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        _import.ReadAsync(path, cancellationToken);
}
