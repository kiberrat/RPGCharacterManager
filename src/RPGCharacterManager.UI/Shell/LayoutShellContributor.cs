using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Models.Shell;
using RPGCharacterManager.UI.Documents;

namespace RPGCharacterManager.UI.Shell;

/// <summary>
/// Элементы оболочки редактора интерфейса.
/// </summary>
public sealed class LayoutShellContributor : IShellContributor
{
    /// <summary>Идентификатор документа редактора макетов.</summary>
    public const string EditorDocumentId = DocumentIds.Layouts;

    /// <inheritdoc />
    public int Order => 17;

    /// <summary>
    /// Возвращает описания документов редактора интерфейса.
    /// </summary>
    /// <returns>Последовательность описаний документов.</returns>
    public static IEnumerable<IDocumentDescriptor> GetDocumentDescriptors()
    {
        yield return new DocumentDescriptor<ViewModels.Documents.LayoutEditorViewModel>(
            EditorDocumentId,
            "Интерфейс");
    }

    /// <inheritdoc />
    public IEnumerable<NavigationItemContribution> GetNavigationItems()
    {
        yield return new NavigationItemContribution("nav.layouts", "Интерфейс", EditorDocumentId)
        {
            Order = 75,
            IconKey = "ЗначокИнтерфейса",
        };
    }
}
