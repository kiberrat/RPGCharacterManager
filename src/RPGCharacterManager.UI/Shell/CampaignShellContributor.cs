using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Models.Shell;
using RPGCharacterManager.UI.Documents;

namespace RPGCharacterManager.UI.Shell;

/// <summary>
/// Элементы оболочки подсистемы кампаний.
///
/// Раздел «Кампании» подключается собственным поставщиком, как и предполагалось
/// решением Р-02: главное окно и остальные разделы для этого не изменялись.
/// </summary>
public sealed class CampaignShellContributor : IShellContributor
{
    /// <summary>Идентификатор документа со списком кампаний.</summary>
    public const string ListDocumentId = DocumentIds.Campaigns;

    /// <inheritdoc />
    public int Order => 15;

    /// <summary>
    /// Возвращает описания документов подсистемы кампаний.
    /// </summary>
    /// <returns>Последовательность описаний документов.</returns>
    public static IEnumerable<IDocumentDescriptor> GetDocumentDescriptors()
    {
        yield return new DocumentDescriptor<ViewModels.Documents.CampaignsViewModel>(
            ListDocumentId,
            "Кампании");
    }

    /// <inheritdoc />
    public IEnumerable<NavigationItemContribution> GetNavigationItems()
    {
        yield return new NavigationItemContribution("nav.campaigns", "Кампании", ListDocumentId)
        {
            Order = 20,
            IconKey = "ЗначокКампании",
        };
    }
}
