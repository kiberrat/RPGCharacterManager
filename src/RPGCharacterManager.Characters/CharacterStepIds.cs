namespace RPGCharacterManager.Characters;

/// <summary>
/// Идентификаторы стандартных шагов мастера создания персонажа.
/// Используются для указания зависимостей между шагами и в замечаниях проверки.
/// </summary>
public static class CharacterStepIds
{
    /// <summary>Выбор игровой системы и источников контента.</summary>
    public const string GameSystem = "characters.step.system";

    /// <summary>Основная информация о персонаже.</summary>
    public const string Basics = "characters.step.basics";

    /// <summary>Выбор расы.</summary>
    public const string Race = "characters.step.race";

    /// <summary>Выбор класса.</summary>
    public const string Class = "characters.step.class";

    /// <summary>Выбор подкласса.</summary>
    public const string Subclass = "characters.step.subclass";

    /// <summary>Выбор происхождения.</summary>
    public const string Background = "characters.step.background";

    /// <summary>Распределение характеристик.</summary>
    public const string Attributes = "characters.step.attributes";

    /// <summary>Выбор навыков.</summary>
    public const string Skills = "characters.step.skills";

    /// <summary>Выбор черт.</summary>
    public const string Traits = "characters.step.traits";

    /// <summary>Выбор заклинаний.</summary>
    public const string Spells = "characters.step.spells";

    /// <summary>Проверка и создание персонажа.</summary>
    public const string Summary = "characters.step.summary";
}
