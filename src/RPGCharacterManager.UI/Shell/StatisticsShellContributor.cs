using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Models.Shell;
using RPGCharacterManager.UI.Documents;

namespace RPGCharacterManager.UI.Shell;

/// <summary>
/// Элементы оболочки подсистемы статистики.
/// </summary>
public sealed class StatisticsShellContributor : IShellContributor
{
    /// <summary>Идентификатор документа статистики.</summary>
    public const string ReportDocumentId = DocumentIds.Statistics;

    /// <inheritdoc />
    public int Order => 19;

    /// <summary>
    /// Возвращает описания документов подсистемы статистики.
    /// </summary>
    /// <returns>Последовательность описаний документов.</returns>
    public static IEnumerable<IDocumentDescriptor> GetDocumentDescriptors()
    {
        yield return new DocumentDescriptor<ViewModels.Documents.StatisticsViewModel>(
            ReportDocumentId,
            "Статистика");
    }

    /// <inheritdoc />
    public IEnumerable<NavigationItemContribution> GetNavigationItems()
    {
        // Статистика стоит сразу за журналом: она считает то же, что журнал
        // показывает по одному событию.
        yield return new NavigationItemContribution("nav.statistics", "Статистика", ReportDocumentId)
        {
            Order = 160,
            IconKey = "ЗначокСтатистики",
        };
    }
}
