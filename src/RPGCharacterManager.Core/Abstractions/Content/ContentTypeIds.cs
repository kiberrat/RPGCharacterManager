namespace RPGCharacterManager.Core.Abstractions.Content;

/// <summary>
/// Идентификаторы встроенных видов контента.
///
/// Используются полями-ссылками и сохраняются в описаниях пользовательских свойств.
/// Перечень объявлен в слое контрактов, потому что на виды контента ссылаются и
/// другие подсистемы: состав кампании, например, хранит вид объекта именно так.
/// </summary>
public static class ContentTypeIds
{
    /// <summary>Игровые системы.</summary>
    public const string GameSystems = "gameSystems";

    /// <summary>Контент-паки.</summary>
    public const string ContentPacks = "contentPacks";

    /// <summary>Характеристики.</summary>
    public const string Attributes = "attributes";

    /// <summary>Навыки.</summary>
    public const string Skills = "skills";

    /// <summary>Расы.</summary>
    public const string Races = "races";

    /// <summary>Происхождения.</summary>
    public const string Backgrounds = "backgrounds";

    /// <summary>Классы.</summary>
    public const string Classes = "classes";

    /// <summary>Подклассы.</summary>
    public const string Subclasses = "subclasses";

    /// <summary>Черты.</summary>
    public const string Traits = "traits";

    /// <summary>Способности.</summary>
    public const string Abilities = "abilities";

    /// <summary>Заклинания.</summary>
    public const string Spells = "spells";

    /// <summary>Ресурсы.</summary>
    public const string Resources = "resources";

    /// <summary>Эффекты.</summary>
    public const string Effects = "effects";

    /// <summary>Предметы.</summary>
    public const string Items = "items";

    /// <summary>Категории предметов.</summary>
    public const string ItemCategories = "itemCategories";

    /// <summary>Оружие.</summary>
    public const string Weapons = "weapons";

    /// <summary>Слоты экипировки.</summary>
    public const string EquipmentSlots = "equipmentSlots";

    /// <summary>Пользовательские кубики.</summary>
    public const string DieTypes = "dieTypes";

    /// <summary>Виды отдыха.</summary>
    public const string RestTypes = "restTypes";

    /// <summary>Монстры.</summary>
    public const string Monsters = "monsters";

    /// <summary>Локации.</summary>
    public const string Locations = "locations";

    /// <summary>Неигровые персонажи.</summary>
    public const string Npcs = "npcs";

    /// <summary>Квесты.</summary>
    public const string Quests = "quests";
}
