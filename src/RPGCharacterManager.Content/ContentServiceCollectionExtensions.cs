using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Content;

/// <summary>
/// Регистрация подсистемы игрового контента в контейнере зависимостей.
/// </summary>
public static class ContentServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует службу контента, пользовательские свойства и описания
    /// встроенных видов контента.
    ///
    /// Новый вид контента подключается регистрацией собственного
    /// <see cref="IContentTypeDescriptor"/> и появляется в редакторе без изменения кода.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddContent(this IServiceCollection services)
    {
        Guard.NotNull(services);

        foreach (var descriptor in StandardContentTypes.Create())
        {
            services.AddSingleton(descriptor);
        }

        services.TryAddSingleton<IContentService, ContentService>();
        services.TryAddSingleton<ICustomPropertyService, CustomPropertyService>();

        return services;
    }
}
