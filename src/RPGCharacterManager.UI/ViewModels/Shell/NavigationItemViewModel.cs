using Avalonia;
using Avalonia.Media;
using RPGCharacterManager.Core.Models.Shell;

namespace RPGCharacterManager.UI.ViewModels.Shell;

/// <summary>
/// Модель представления раздела панели навигации.
/// </summary>
public sealed class NavigationItemViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт модель представления раздела навигации.
    /// </summary>
    /// <param name="contribution">Описание раздела.</param>
    public NavigationItemViewModel(NavigationItemContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);

        Id = contribution.Id;
        Title = contribution.Title;
        DocumentId = contribution.DocumentId;
        IconKey = contribution.IconKey;
        Icon = FindIcon(contribution.IconKey);
    }

    /// <summary>Идентификатор раздела.</summary>
    public string Id { get; }

    /// <summary>Отображаемое название раздела.</summary>
    public string Title { get; }

    /// <summary>Идентификатор открываемого документа.</summary>
    public string DocumentId { get; }

    /// <summary>Ключ ресурса значка.</summary>
    public string? IconKey { get; }

    /// <summary>Контур значка раздела.</summary>
    public Geometry? Icon { get; }

    /// <summary>Значок задан и найден среди ресурсов оформления.</summary>
    public bool HasIcon => Icon is not null;

    /// <summary>
    /// Находит контур значка среди ресурсов приложения.
    ///
    /// Подсистема указывает только ключ ресурса, поэтому её код не зависит
    /// ни от конкретного оформления, ни от способа рисования значка. Отсутствие
    /// ресурса не является ошибкой: раздел просто отображается без значка.
    /// </summary>
    /// <param name="iconKey">Ключ ресурса значка.</param>
    /// <returns>Контур значка либо <see langword="null"/>.</returns>
    private static Geometry? FindIcon(string? iconKey)
    {
        if (string.IsNullOrWhiteSpace(iconKey) || Application.Current is not { } application)
        {
            return null;
        }

        return application.Resources.TryGetResource(iconKey, application.ActualThemeVariant, out var resource)
            ? resource as Geometry
            : null;
    }
}
