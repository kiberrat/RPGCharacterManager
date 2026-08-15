using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Import;

/// <summary>
/// Прочитанный документ, готовый к разбору помощником.
///
/// Все поддерживаемые форматы приводятся к тексту: и книга правил, и таблица
/// базы данных, и набор записей JSON. Дальше с ними работает один и тот же
/// разбор, поэтому распознавание игровых объектов не зависит от того, откуда
/// эти сведения пришли.
/// </summary>
/// <param name="Name">Название документа: имя файла без расширения.</param>
/// <param name="Format">Название формата, из которого прочитан документ.</param>
/// <param name="Text">Текст документа.</param>
/// <param name="Notes">Что найдено внутри: страницы, таблицы, количество записей.</param>
public sealed record ImportedDocument(
    string Name,
    string Format,
    string Text,
    IReadOnlyList<string> Notes)
{
    /// <summary>Документ не содержит текста.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
}

/// <summary>
/// Формат, доступный для импорта.
/// </summary>
/// <param name="Title">Название формата для пользователя.</param>
/// <param name="Extensions">Расширения файлов этого формата.</param>
public sealed record ImportFormat(string Title, IReadOnlyList<string> Extensions);

/// <summary>
/// Чтение документов одного формата.
///
/// Новый формат подключается регистрацией ещё одного чтения и не требует
/// изменения ни службы импорта, ни разбора: она лишь выбирает подходящее
/// чтение по расширению файла.
/// </summary>
public interface IDocumentReader
{
    /// <summary>Название формата для пользователя.</summary>
    string Format { get; }

    /// <summary>Расширения файлов, которые читает это чтение. С точкой и в нижнем регистре.</summary>
    IReadOnlyList<string> Extensions { get; }

    /// <summary>
    /// Читает документ.
    /// </summary>
    /// <param name="path">Полный путь к файлу.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Прочитанный документ либо описание ошибки.</returns>
    Task<Result<ImportedDocument>> ReadAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Импорт внешних документов: приведение файла любого поддерживаемого формата
/// к тексту, пригодному для распознавания игровых объектов.
/// </summary>
public interface IImportService
{
    /// <summary>Форматы, доступные для импорта, в порядке показа.</summary>
    IReadOnlyList<ImportFormat> Formats { get; }

    /// <summary>Расширения всех поддерживаемых форматов.</summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Определяет, умеет ли приложение читать этот файл.
    /// </summary>
    /// <param name="path">Путь к файлу.</param>
    /// <returns><see langword="true"/>, если формат поддерживается.</returns>
    bool CanRead(string path);

    /// <summary>
    /// Читает документ, выбирая чтение по расширению файла.
    /// </summary>
    /// <param name="path">Полный путь к файлу.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Прочитанный документ либо описание ошибки.</returns>
    Task<Result<ImportedDocument>> ReadAsync(string path, CancellationToken cancellationToken = default);
}
