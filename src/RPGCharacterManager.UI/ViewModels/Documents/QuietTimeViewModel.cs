using RPGCharacterManager.UI.Shell;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>Документ со встроенными мини-играми.</summary>
public sealed class QuietTimeViewModel : DocumentViewModelBase
{
    /// <summary>Создаёт документ мини-игр.</summary>
    public QuietTimeViewModel()
        : base(CoreShellContributor.QuietTimeDocumentId, "Тишину навели", "ЗначокМиниИгры")
    {
    }
}
