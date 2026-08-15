using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Models.Shell;
using RPGCharacterManager.UI.Documents;

namespace RPGCharacterManager.UI.Shell;

/// <summary>
/// Элементы оболочки подсистемы персонажей.
///
/// Раздел «Персонажи» подключается к панели навигации собственным поставщиком,
/// поэтому главное окно и остальные разделы не изменяются (см. решение Р-02).
/// </summary>
public sealed class CharacterShellContributor : IShellContributor
{
    /// <summary>Идентификатор документа со списком персонажей.</summary>
    public const string ListDocumentId = DocumentIds.Characters;

    /// <summary>Идентификатор документа мастера создания персонажа.</summary>
    public const string WizardDocumentId = DocumentIds.CharacterWizard;

    /// <summary>Идентификатор документа листа персонажа.</summary>
    public const string SheetDocumentId = DocumentIds.CharacterSheet;

    /// <inheritdoc />
    public int Order => 10;

    /// <summary>
    /// Возвращает описания документов подсистемы персонажей.
    /// </summary>
    /// <returns>Последовательность описаний документов.</returns>
    public static IEnumerable<IDocumentDescriptor> GetDocumentDescriptors()
    {
        yield return new DocumentDescriptor<ViewModels.Documents.CharactersViewModel>(
            ListDocumentId,
            "Персонажи");

        yield return new DocumentDescriptor<ViewModels.Documents.CharacterWizardViewModel>(
            WizardDocumentId,
            "Создание персонажа");

        // Лист открывается для конкретного персонажа, поэтому вкладок может быть
        // несколько — по одной на персонажа.
        yield return new DocumentDescriptor<ViewModels.Documents.CharacterSheetViewModel>(
            SheetDocumentId,
            "Лист персонажа",
            iconKey: null,
            allowMultipleInstances: true);
    }

    /// <inheritdoc />
    public IEnumerable<NavigationItemContribution> GetNavigationItems()
    {
        // Раздел один. Мастер создания открывается кнопкой внутри списка персонажей:
        // это действие над разделом, а не отдельное место в приложении.
        yield return new NavigationItemContribution("nav.characters", "Персонажи", ListDocumentId)
        {
            Order = 10,
            IconKey = "ЗначокПерсонажи",
        };
    }
}
