using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Models.Shell;
using RPGCharacterManager.UI.Documents;

namespace RPGCharacterManager.UI.Shell;

/// <summary>
/// Элементы оболочки подсистемы расширений.
/// </summary>
public sealed class ExtensionShellContributor : IShellContributor
{
    /// <summary>Идентификатор документа со списком расширений.</summary>
    public const string ListDocumentId = DocumentIds.Extensions;

    /// <inheritdoc />
    public int Order => 20;

    /// <summary>
    /// Возвращает описания документов подсистемы расширений.
    /// </summary>
    /// <returns>Последовательность описаний документов.</returns>
    public static IEnumerable<IDocumentDescriptor> GetDocumentDescriptors()
    {
        yield return new DocumentDescriptor<ViewModels.Documents.ExtensionsViewModel>(
            ListDocumentId,
            "Расширения");
    }

    /// <inheritdoc />
    public IEnumerable<NavigationItemContribution> GetNavigationItems()
    {
        // Расширения стоят рядом с контентом: они и приносят его в приложение.
        yield return new NavigationItemContribution("nav.extensions", "Расширения", ListDocumentId)
        {
            Order = 60,
            IconKey = "ЗначокРасширений",
        };
    }
}
