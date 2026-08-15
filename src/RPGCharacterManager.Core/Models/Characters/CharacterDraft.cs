using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Core.Models.Characters;

/// <summary>
/// Создаваемый персонаж со всеми выборами мастера.
///
/// Черновик существует только в памяти до нажатия кнопки создания: пока пользователь
/// перемещается по шагам, база данных не изменяется, поэтому отказ от создания
/// не оставляет следов.
/// </summary>
public sealed class CharacterDraft
{
    /// <summary>
    /// Создаёт черновик нового персонажа.
    /// </summary>
    public CharacterDraft() => Character = new Character();

    /// <summary>
    /// Создаёт черновик по существующему персонажу.
    /// Применяется при пересчёте сохранённого персонажа и при создании копии.
    /// </summary>
    /// <param name="character">Персонаж, ставший основой черновика.</param>
    public CharacterDraft(Character character) =>
        Character = character ?? throw new ArgumentNullException(nameof(character));

    /// <summary>Персонаж, который будет создан. Поля основной информации записываются прямо в него.</summary>
    public Character Character { get; }

    /// <summary>Идентификатор выбранной игровой системы.</summary>
    public Guid? GameSystemId
    {
        get => Character.GameSystemId;
        set => Character.GameSystemId = value;
    }

    /// <summary>Уровень создаваемого персонажа.</summary>
    public int Level
    {
        get => Character.Level;
        set => Character.Level = value;
    }

    /// <summary>
    /// Использовать весь доступный контент.
    /// Когда признак снят, допускаются только объекты источников из <see cref="EnabledSourceIds"/>.
    /// </summary>
    public bool UseAllSources { get; set; } = true;

    /// <summary>Идентификаторы разрешённых контент-паков.</summary>
    public HashSet<Guid> EnabledSourceIds { get; } = [];

    /// <summary>Выбор на шагах одиночного выбора, сопоставленный идентификатору шага.</summary>
    public Dictionary<string, Guid> Selections { get; } = new(StringComparer.Ordinal);

    /// <summary>Выбор на шагах множественного выбора, сопоставленный идентификатору шага.</summary>
    public Dictionary<string, HashSet<Guid>> MultipleSelections { get; } = new(StringComparer.Ordinal);

    /// <summary>Базовые значения характеристик, сопоставленные идентификатору характеристики.</summary>
    public Dictionary<Guid, double> AttributeBaseValues { get; } = [];

    /// <summary>
    /// Пользовательские значения вычисляемых характеристик.
    /// Отсутствие записи означает использование формулы игровой системы.
    /// </summary>
    public Dictionary<Guid, double> AttributeOverrides { get; } = [];

    /// <summary>Выбранный способ распределения характеристик.</summary>
    public AttributeAssignmentMethod AttributeMethod { get; set; } = AttributeAssignmentMethod.Manual;

    /// <summary>
    /// Возвращает выбор шага одиночного выбора.
    /// </summary>
    /// <param name="stepId">Идентификатор шага.</param>
    /// <returns>Идентификатор выбранного объекта или <see langword="null"/>.</returns>
    public Guid? GetSelection(string stepId) =>
        Selections.TryGetValue(stepId, out var value) ? value : null;

    /// <summary>
    /// Возвращает выбор шага множественного выбора.
    /// </summary>
    /// <param name="stepId">Идентификатор шага.</param>
    /// <returns>Идентификаторы выбранных объектов.</returns>
    public HashSet<Guid> GetSelections(string stepId)
    {
        if (!MultipleSelections.TryGetValue(stepId, out var selected))
        {
            selected = [];
            MultipleSelections[stepId] = selected;
        }

        return selected;
    }
}
