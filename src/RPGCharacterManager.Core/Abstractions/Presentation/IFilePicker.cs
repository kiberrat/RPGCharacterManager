namespace RPGCharacterManager.Core.Abstractions.Presentation;

/// <summary>
/// Выбор файла средствами операционной системы.
///
/// Документ 003_UI_UX.md запрещает устаревшие окна Windows, но не обзор файлов:
/// без него импорт потребовал бы вручную копировать книгу в папку приложения.
/// Используется современный обзор, оформленный самой системой, — свой такой же
/// приложение написать не может, потому что доступ к диску за пределами своих
/// каталогов даёт именно он.
/// </summary>
public interface IFilePicker
{
    /// <summary>
    /// Показывает обзор файлов и возвращает выбранный файл.
    /// </summary>
    /// <param name="title">Заголовок окна обзора.</param>
    /// <param name="description">Название набора форматов: «Документы».</param>
    /// <param name="extensions">Расширения выбираемых файлов, с точкой.</param>
    /// <returns>Полный путь к файлу либо <see langword="null"/>, если выбор отменён.</returns>
    Task<string?> PickAsync(string title, string description, IReadOnlyList<string> extensions);

    /// <summary>
    /// Показывает окно сохранения файла и возвращает выбранный путь.
    /// </summary>
    /// <param name="title">Заголовок окна.</param>
    /// <param name="description">Название формата: «Расширение».</param>
    /// <param name="extension">Расширение имени файла, с точкой.</param>
    /// <param name="suggestedName">Предлагаемое имя файла без расширения.</param>
    /// <returns>Полный путь к файлу либо <see langword="null"/>, если выбор отменён.</returns>
    Task<string?> SaveAsync(string title, string description, string extension, string? suggestedName = null);
}
