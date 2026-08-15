using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using RPGCharacterManager.Core.Abstractions.Master;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Строка сводки мастера: персонаж с отметкой выбора.
///
/// Отметка живёт в модели представления, а не в базе: она означает «сейчас этот
/// персонаж под действием», и переживать закрытие приложения ей незачем.
/// </summary>
public sealed partial class MasterRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _initiativeText = string.Empty;

    /// <summary>
    /// Создаёт строку сводки.
    /// </summary>
    /// <param name="character">Персонаж со сводными сведениями.</param>
    public MasterRowViewModel(MasterCharacter character)
    {
        Character = Guard.NotNull(character);

        _initiativeText = character.Initiative is { } value
            ? value.ToString("0.##", CultureInfo.CurrentCulture)
            : string.Empty;
    }

    /// <summary>Персонаж со всеми сведениями сводки.</summary>
    public MasterCharacter Character { get; }

    /// <summary>Идентификатор персонажа.</summary>
    public Guid Id => Character.Id;

    /// <summary>Имя персонажа.</summary>
    public string Name => Character.Name;

    /// <summary>
    /// Читает значение инициативы, введённое мастером.
    /// </summary>
    /// <param name="value">Разобранное значение.</param>
    /// <returns><see langword="true"/>, если введено число.</returns>
    public bool TryReadInitiative(out double value) =>
        double.TryParse(
            InitiativeText,
            NumberStyles.Float,
            CultureInfo.CurrentCulture,
            out value);
}
