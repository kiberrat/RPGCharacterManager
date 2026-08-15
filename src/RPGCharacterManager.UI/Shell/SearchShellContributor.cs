using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Models.Shell;
using RPGCharacterManager.UI.Documents;

namespace RPGCharacterManager.UI.Shell;

/// <summary>
/// Элементы оболочки глобального поиска.
/// </summary>
public sealed class SearchShellContributor : IShellContributor
{
    /// <summary>Идентификатор документа поиска.</summary>
    public const string ResultsDocumentId = DocumentIds.Search;

    /// <inheritdoc />
    public int Order => 5;

    /// <summary>
    /// Возвращает описания документов поиска.
    /// </summary>
    /// <returns>Последовательность описаний документов.</returns>
    public static IEnumerable<IDocumentDescriptor> GetDocumentDescriptors()
    {
        yield return new DocumentDescriptor<ViewModels.Documents.SearchViewModel>(
            ResultsDocumentId,
            "Поиск");
    }

    /// <inheritdoc />
    public IEnumerable<NavigationItemContribution> GetNavigationItems()
    {
        // Поиск стоит первым: к нему обращаются чаще, чем к любому разделу.
        yield return new NavigationItemContribution("nav.search", "Поиск", ResultsDocumentId)
        {
            Order = 5,
            IconKey = "ЗначокПоиска",
        };
    }
}
