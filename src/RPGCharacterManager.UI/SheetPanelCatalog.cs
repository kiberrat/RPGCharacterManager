using Avalonia.Controls;
using RPGCharacterManager.Core.Abstractions.Layouts;
using RPGCharacterManager.UI.Views.Characters;

namespace RPGCharacterManager.UI;

/// <summary>
/// Перечень панелей листа персонажа.
///
/// Панель — готовая часть интерфейса вместе со своим представлением. Каталог —
/// единственное место, где ключ панели связан с её разметкой: макеты хранят
/// только ключи, поэтому подсистема макетов о представлениях не знает.
/// </summary>
public sealed class SheetPanelCatalog : ISheetPanelCatalog
{
    /// <summary>Ключ панели характеристик.</summary>
    public const string Attributes = "характеристики";

    /// <summary>Ключ панели навыков.</summary>
    public const string Skills = "навыки";

    /// <summary>Ключ панели эффектов.</summary>
    public const string Effects = "эффекты";

    /// <summary>Ключ панели отдыха.</summary>
    public const string Rest = "отдых";

    /// <summary>Ключ панели заклинаний.</summary>
    public const string Spells = "заклинания";

    /// <summary>Ключ панели инвентаря.</summary>
    public const string Inventory = "инвентарь";

    /// <summary>Ключ панели экипировки.</summary>
    public const string Equipment = "экипировка";

    /// <summary>Ключ панели оружия.</summary>
    public const string Weapons = "оружие";

    /// <summary>Ключ панели черт и способностей.</summary>
    public const string Traits = "черты";

    /// <summary>Ключ панели описания.</summary>
    public const string Description = "описание";

    /// <summary>Ключ панели пользовательских полей.</summary>
    public const string CustomFields = "свои-поля";

    private static readonly SheetPanelDescriptor[] Descriptors =
    [
        new(Attributes, "Параметры", "Характеристики и производные значения.", 10),
        new(Skills, "Навыки", "Навыки персонажа и их значения.", 20),
        new(Effects, "Эффекты", "Действующие эффекты и их таймеры.", 30),
        new(Rest, "Отдых", "Виды отдыха и восстановление ресурсов.", 40),
        new(Spells, "Заклинания", "Книга заклинаний и их применение.", 50),
        new(Inventory, "Инвентарь", "Предметы, вместилища и вес.", 60),
        new(Equipment, "Экипировка", "Надетые предметы по слотам.", 70),
        new(Weapons, "Оружие", "Выданное оружие и броски атаки.", 80),
        new(Traits, "Черты и способности", "Полученные черты и доступные способности.", 90),
        new(Description, "Описание", "Имя, биография и прочие описательные поля.", 100),
        new(CustomFields, "Свои поля", "Поля, добавленные пользователем.", 110),
    ];

    /// <inheritdoc />
    public IReadOnlyList<SheetPanelDescriptor> Panels { get; } = Descriptors;

    /// <inheritdoc />
    public SheetPanelDescriptor? Find(string panelId) =>
        Descriptors.FirstOrDefault(panel => string.Equals(panel.Id, panelId, StringComparison.Ordinal));

    /// <summary>
    /// Создаёт представление панели по её ключу.
    /// </summary>
    /// <param name="panelId">Ключ панели.</param>
    /// <returns>Представление панели.</returns>
    public static Control CreateView(string panelId) => panelId switch
    {
        Attributes => new AttributesPanelView(),
        Skills => new SkillsPanelView(),
        Effects => new EffectsPanelView(),
        Rest => new RestPanelView(),
        Spells => new SpellsPanelView(),
        Inventory => new InventoryPanelView(),
        Equipment => new EquipmentPanelView(),
        Weapons => new WeaponsPanelView(),
        Traits => new TraitsPanelView(),
        Description => new DescriptionPanelView(),
        CustomFields => new CustomFieldsPanelView(),

        // Панель, которой больше нет: макет ссылается на ключ, исчезнувший
        // из приложения. Пустая панель молчала бы об этом.
        _ => new MissingPanelView(),
    };
}
