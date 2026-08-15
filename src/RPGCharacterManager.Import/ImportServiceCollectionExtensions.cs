using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.Import;
using RPGCharacterManager.Import.Readers;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Import;

/// <summary>
/// Регистрация подсистемы импорта в контейнере зависимостей.
/// </summary>
public static class ImportServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует импорт документов и чтение поддерживаемых форматов.
    ///
    /// Новый формат добавляется одной строкой: служба импорта находит его
    /// по расширению сама, а всё, что происходит дальше, не меняется.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddImport(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.AddSingleton<IDocumentReader, PlainTextReader>();
        services.AddSingleton<IDocumentReader, HtmlReader>();
        services.AddSingleton<IDocumentReader, DocxReader>();
        services.AddSingleton<IDocumentReader, PdfReader>();
        services.AddSingleton<IDocumentReader, JsonReader>();
        services.AddSingleton<IDocumentReader, SqliteReader>();

        services.TryAddSingleton<IImportService, ImportService>();

        return services;
    }
}
