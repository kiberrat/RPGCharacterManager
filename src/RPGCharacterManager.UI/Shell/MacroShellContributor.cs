using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Models.Shell;
using RPGCharacterManager.UI.Documents;

namespace RPGCharacterManager.UI.Shell;

/// <summary>
/// Элементы оболочки подсистемы макросов.
/// </summary>
public sealed class MacroShellContributor : IShellContributor
{
    /// <summary>Идентификатор документа со списком макросов.</summary>
    public const string ListDocumentId = DocumentIds.Macros;

    /// <inheritdoc />
    public int Order => 18;

    /// <summary>
    /// Возвращает описания документов подсистемы макросов.
    /// </summary>
    /// <returns>Последовательность описаний документов.</returns>
    public static IEnumerable<IDocumentDescriptor> GetDocumentDescriptors()
    {
        yield return new DocumentDescriptor<ViewModels.Documents.MacrosViewModel>(
            ListDocumentId,
            "Макросы");
    }

    /// <inheritdoc />
    public IEnumerable<NavigationItemContribution> GetNavigationItems()
    {
        yield return new NavigationItemContribution("nav.macros", "Макросы", ListDocumentId)
        {
            Order = 55,
            IconKey = "ЗначокМакросов",
        };
    }
}
