using RPGCharacterManager.Core.Abstractions.Import;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Ai;

/// <summary>
/// Книга, доступная помощнику для разбора.
/// </summary>
/// <param name="Name">Название книги: имя файла без расширения.</param>
/// <param name="Path">Полный путь к файлу.</param>
/// <param name="Size">Размер файла в байтах.</param>
public sealed record AiBook(string Name, string Path, long Size)
{
    /// <summary>Размер книги в понятном человеку виде.</summary>
    public string SizeText => Size < 1024
        ? $"{Size} Б"
        : Size < 1024 * 1024
            ? $"{Size / 1024.0:0.#} КБ"
            : $"{Size / (1024.0 * 1024.0):0.#} МБ";
}

/// <summary>
/// Местная библиотека документов помощника.
///
/// Документ 024_AI_Помощник.md требует поддержки локальных книг. Документы лежат
/// в каталоге пользовательских данных: положенный туда файл появляется в списке
/// сам. Читать их библиотека не умеет — это делает подсистема импорта, поэтому
/// перечень доступных форматов задаётся ею, а не здесь.
/// </summary>
public interface IAiLibrary
{
    /// <summary>Каталог, в котором приложение ищет документы.</summary>
    string Directory { get; }

    /// <summary>Расширения файлов, доступных для разбора.</summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Возвращает документы, найденные в каталоге.
    /// </summary>
    /// <returns>Документы в порядке имён.</returns>
    IReadOnlyList<AiBook> GetBooks();

    /// <summary>
    /// Читает текст документа.
    /// </summary>
    /// <param name="book">Документ.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Источник текста либо описание ошибки.</returns>
    Task<Result<AiSource>> ReadAsync(AiBook book, CancellationToken cancellationToken = default);

    /// <summary>
    /// Читает документ по произвольному пути и возвращает сведения о его содержимом.
    ///
    /// Позволяет показать пользователю, что найдено в файле, прежде чем тратить
    /// обращения к языковой модели на его разбор.
    /// </summary>
    /// <param name="path">Полный путь к файлу.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Прочитанный документ либо описание ошибки.</returns>
    Task<Result<ImportedDocument>> InspectAsync(string path, CancellationToken cancellationToken = default);
}
