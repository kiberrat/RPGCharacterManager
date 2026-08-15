using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Models.Shell;
using RPGCharacterManager.UI.Documents;

namespace RPGCharacterManager.UI.Shell;

/// <summary>
/// Элементы оболочки подсистемы помощника.
///
/// Раздел «AI» подключается собственным поставщиком, как и предполагалось
/// решением Р-02: ни главное окно, ни поставщик ядра для этого не изменялись.
/// </summary>
public sealed class AiShellContributor : IShellContributor
{
    /// <summary>Идентификатор документа помощника.</summary>
    public const string AssistantDocumentId = DocumentIds.Ai;

    /// <inheritdoc />
    public int Order => 20;

    /// <summary>
    /// Возвращает описания документов подсистемы помощника.
    /// </summary>
    /// <returns>Последовательность описаний документов.</returns>
    public static IEnumerable<IDocumentDescriptor> GetDocumentDescriptors()
    {
        yield return new DocumentDescriptor<ViewModels.Documents.AiViewModel>(
            AssistantDocumentId,
            "AI");
    }

    /// <inheritdoc />
    public IEnumerable<NavigationItemContribution> GetNavigationItems()
    {
        yield return new NavigationItemContribution("nav.ai", "AI", AssistantDocumentId)
        {
            Order = 200,
            IconKey = "ЗначокAI",
        };
    }
}
