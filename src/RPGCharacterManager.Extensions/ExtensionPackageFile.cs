using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using RPGCharacterManager.Core.Abstractions.Extensions;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Extensions;

/// <summary>
/// Прочитанный файл расширения.
/// </summary>
/// <param name="Manifest">Описание расширения.</param>
/// <param name="Content">Содержимое расширения.</param>
internal sealed record PackageFile(ExtensionManifest Manifest, PackageContent Content);

/// <summary>
/// Чтение и запись файла расширения.
///
/// Файл — обычный zip-архив с двумя документами: описанием и содержимым.
/// Формат выбран не ради сжатия, а ради того, чтобы расширение можно было
/// открыть, посмотреть и поправить любым архиватором и текстовым редактором.
/// </summary>
internal static class ExtensionPackageFile
{
    /// <summary>Настройки чтения и записи документов пакета.</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,

        // Русские названия и имена полей попадают в файл как есть: расширение
        // читается человеком без словаря экранированных последовательностей.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

        // Объекты загружаются вместе с вложенными списками, а у записи списка
        // есть обратная ссылка на владельца. Без этого запись зациклилась бы.
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Читает файл расширения.
    /// </summary>
    /// <param name="path">Полный путь к файлу.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Прочитанный файл либо описание ошибки.</returns>
    public static async Task<Result<PackageFile>> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return Result.Failure<PackageFile>($"Файл «{path}» не найден.");
        }

        try
        {
            using var archive = ZipFile.OpenRead(path);

            var manifest = await ReadEntryAsync<PackageManifest>(
                archive, ExtensionPackage.ManifestEntry, cancellationToken).ConfigureAwait(false);

            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Name))
            {
                return Result.Failure<PackageFile>(
                    $"Это не расширение: в файле нет описания «{ExtensionPackage.ManifestEntry}» "
                    + "или в нём не указано название.");
            }

            var content = await ReadEntryAsync<PackageContent>(
                archive, ExtensionPackage.ContentEntry, cancellationToken).ConfigureAwait(false);

            return Result.Success(new PackageFile(ToManifest(manifest), content ?? new PackageContent()));
        }
        catch (InvalidDataException)
        {
            return Result.Failure<PackageFile>(
                "Файл повреждён или не является расширением: его не удалось открыть как архив.");
        }
        catch (JsonException exception)
        {
            return Result.Failure<PackageFile>($"Описание расширения испорчено: {exception.Message}");
        }
        catch (IOException exception)
        {
            return Result.Failure<PackageFile>($"Не удалось прочитать файл: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result.Failure<PackageFile>($"Нет доступа к файлу: {exception.Message}");
        }
    }

    /// <summary>
    /// Записывает файл расширения.
    /// </summary>
    /// <param name="path">Полный путь создаваемого файла.</param>
    /// <param name="manifest">Описание расширения.</param>
    /// <param name="content">Содержимое расширения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Размер созданного файла либо описание ошибки.</returns>
    public static async Task<Result<long>> WriteAsync(
        string path,
        ExtensionManifest manifest,
        PackageContent content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }

            // Архив собирается целиком и лишь затем заменяет прежний файл: прерванная
            // выгрузка не должна оставить на месте готового расширения обрубок.
            var temporary = path + ".часть";

            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                await WriteEntryAsync(archive, ExtensionPackage.ManifestEntry, ToDocument(manifest), cancellationToken)
                    .ConfigureAwait(false);

                await WriteEntryAsync(archive, ExtensionPackage.ContentEntry, content, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(temporary, path, overwrite: true);

            return Result.Success(new FileInfo(path).Length);
        }
        catch (IOException exception)
        {
            return Result.Failure<long>($"Не удалось записать файл: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result.Failure<long>($"Нет доступа к файлу: {exception.Message}");
        }
    }

    /// <summary>
    /// Читает документ из записи архива.
    /// </summary>
    /// <typeparam name="TDocument">Тип документа.</typeparam>
    /// <param name="archive">Архив расширения.</param>
    /// <param name="entryName">Имя записи.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Документ либо <see langword="null"/>, если записи нет.</returns>
    private static async Task<TDocument?> ReadEntryAsync<TDocument>(
        ZipArchive archive,
        string entryName,
        CancellationToken cancellationToken)
        where TDocument : class
    {
        if (archive.GetEntry(entryName) is not { } entry)
        {
            return null;
        }

        await using var stream = entry.Open();

        return await JsonSerializer
            .DeserializeAsync<TDocument>(stream, Options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Записывает документ в архив.
    /// </summary>
    /// <typeparam name="TDocument">Тип документа.</typeparam>
    /// <param name="archive">Архив расширения.</param>
    /// <param name="entryName">Имя записи.</param>
    /// <param name="document">Записываемый документ.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после записи.</returns>
    private static async Task WriteEntryAsync<TDocument>(
        ZipArchive archive,
        string entryName,
        TDocument document,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

        await using var stream = entry.Open();

        await JsonSerializer
            .SerializeAsync(stream, document, Options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Преобразует прочитанное описание в контракт приложения.
    /// </summary>
    /// <param name="manifest">Прочитанное описание.</param>
    /// <returns>Описание расширения.</returns>
    private static ExtensionManifest ToManifest(PackageManifest manifest) => new(
        manifest.Name!.Trim(),
        string.IsNullOrWhiteSpace(manifest.Version) ? "1.0" : manifest.Version.Trim(),
        manifest.Author,
        manifest.Description,
        manifest.License,
        manifest.GameSystem,
        manifest.RequiredVersion,
        manifest.CreatedAt,
        string.IsNullOrWhiteSpace(manifest.Format) ? ExtensionPackage.FormatVersion : manifest.Format.Trim())
    {
        Dependencies =
        [
            .. manifest.Dependencies
                .Where(dependency => !string.IsNullOrWhiteSpace(dependency.Name))
                .Select(dependency => new ExtensionDependency(dependency.Name!.Trim(), dependency.Version)),
        ],
    };

    /// <summary>
    /// Преобразует описание расширения в записываемый документ.
    /// </summary>
    /// <param name="manifest">Описание расширения.</param>
    /// <returns>Документ описания.</returns>
    private static PackageManifest ToDocument(ExtensionManifest manifest) => new()
    {
        Format = manifest.FormatVersion,
        Name = manifest.Name,
        Version = manifest.Version,
        Author = manifest.Author,
        Description = manifest.Description,
        License = manifest.License,
        GameSystem = manifest.GameSystem,
        RequiredVersion = manifest.RequiredVersion,
        CreatedAt = manifest.CreatedAt ?? DateTimeOffset.UtcNow,
        Dependencies =
        [
            .. manifest.Dependencies.Select(dependency => new PackageDependency
            {
                Name = dependency.Name,
                Version = dependency.Version,
            }),
        ],
    };
}
