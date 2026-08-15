using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Models.Shell;
using RPGCharacterManager.UI.Documents;

namespace RPGCharacterManager.UI.Shell;

/// <summary>
/// Элементы оболочки режима мастера.
///
/// Раздел «Мастер» подключается собственным поставщиком, как и остальные разделы
/// (решение Р-02): ни главное окно, ни другие разделы для этого не изменялись.
/// </summary>
public sealed class MasterShellContributor : IShellContributor
{
    /// <summary>Идентификатор документа режима мастера.</summary>
    public const string BoardDocumentId = DocumentIds.Master;

    /// <inheritdoc />
    public int Order => 16;

    /// <summary>
    /// Возвращает описания документов режима мастера.
    /// </summary>
    /// <returns>Последовательность описаний документов.</returns>
    public static IEnumerable<IDocumentDescriptor> GetDocumentDescriptors()
    {
        yield return new DocumentDescriptor<ViewModels.Documents.MasterViewModel>(
            BoardDocumentId,
            "Мастер");
    }

    /// <inheritdoc />
    public IEnumerable<NavigationItemContribution> GetNavigationItems()
    {
        yield return new NavigationItemContribution("nav.master", "Мастер", BoardDocumentId)
        {
            Order = 25,
            IconKey = "ЗначокМастера",
        };
    }
}
