namespace RPGCharacterManager.Core.Models.Entities;

/// <summary>
/// Персонаж — центральный объект приложения.
/// </summary>
public class Character : EntityBase
{
    /// <summary>Имя персонажа.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Полное имя персонажа.</summary>
    public string? FullName { get; set; }

    /// <summary>Путь к изображению персонажа.</summary>
    public string? Portrait { get; set; }

    /// <summary>Краткое описание персонажа.</summary>
    public string? Description { get; set; }

    /// <summary>Текущий уровень персонажа.</summary>
    public int Level { get; set; } = 1;

    /// <summary>Накопленный опыт.</summary>
    public double Experience { get; set; }

    /// <summary>Идентификатор расы.</summary>
    public Guid? RaceId { get; set; }

    /// <summary>Раса персонажа.</summary>
    public Race? Race { get; set; }

    /// <summary>Идентификатор класса.</summary>
    public Guid? ClassId { get; set; }

    /// <summary>Класс персонажа.</summary>
    public CharacterClass? Class { get; set; }

    /// <summary>Идентификатор подкласса.</summary>
    public Guid? SubclassId { get; set; }

    /// <summary>Подкласс персонажа.</summary>
    public Subclass? Subclass { get; set; }

    /// <summary>Идентификатор происхождения.</summary>
    public Guid? BackgroundId { get; set; }

    /// <summary>Происхождение персонажа.</summary>
    public Background? Background { get; set; }

    /// <summary>Мировоззрение или его аналог в игровой системе.</summary>
    public string? Alignment { get; set; }

    /// <summary>Возраст персонажа.</summary>
    public string? Age { get; set; }

    /// <summary>Рост персонажа.</summary>
    public string? Height { get; set; }

    /// <summary>Вес персонажа.</summary>
    public string? Weight { get; set; }

    /// <summary>Пол персонажа.</summary>
    public string? Gender { get; set; }

    /// <summary>Известные языки.</summary>
    public string? Languages { get; set; }

    /// <summary>Биография персонажа.</summary>
    public string? Biography { get; set; }

    /// <summary>Свободные заметки игрока.</summary>
    public string? Notes { get; set; }

    /// <summary>Идентификатор игровой системы персонажа.</summary>
    public Guid? GameSystemId { get; set; }

    /// <summary>Игровая система персонажа.</summary>
    public GameSystem? GameSystem { get; set; }

    /// <summary>Персонаж является шаблоном для создания новых персонажей.</summary>
    public bool IsTemplate { get; set; }

    /// <summary>Текущее пользовательское значение маны.</summary>
    public decimal Mana { get; set; }

    /// <summary>Необязательный пользовательский максимум маны.</summary>
    public decimal? ManaMaximum { get; set; }

    /// <summary>Значения характеристик персонажа.</summary>
    public ICollection<CharacterAttributeValue> Attributes { get; set; } = [];

    /// <summary>Владение навыками.</summary>
    public ICollection<CharacterSkill> Skills { get; set; } = [];

    /// <summary>Полученные черты.</summary>
    public ICollection<CharacterTrait> Traits { get; set; } = [];

    /// <summary>Авторские способности, принадлежащие только этому персонажу.</summary>
    public ICollection<CharacterCustomAbility> CustomAbilities { get; set; } = [];

    /// <summary>Деньги и другие валюты персонажа.</summary>
    public ICollection<CharacterCurrency> Currencies { get; set; } = [];

    /// <summary>Ресурсы персонажа.</summary>
    public ICollection<CharacterResource> Resources { get; set; } = [];

    /// <summary>Изученные и подготовленные заклинания.</summary>
    public ICollection<CharacterSpell> Spells { get; set; } = [];

    /// <summary>Предметы в инвентаре.</summary>
    public ICollection<InventoryItem> Inventory { get; set; } = [];

    /// <summary>Экипированные предметы.</summary>
    public ICollection<CharacterEquipment> Equipment { get; set; } = [];

    /// <summary>Действующие эффекты.</summary>
    public ICollection<CharacterEffect> Effects { get; set; } = [];
}

/// <summary>
/// Авторская способность конкретного персонажа.
/// В отличие от справочной <see cref="Ability"/>, она не появляется у других
/// персонажей и может иметь собственное условие доступности.
/// </summary>
public class CharacterCustomAbility : EntityBase
{
    /// <summary>Идентификатор владельца.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Владелец способности.</summary>
    public Character? Character { get; set; }

    /// <summary>Название способности.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Подробное описание.</summary>
    public string? Description { get; set; }

    /// <summary>Раздел на листе.</summary>
    public string? Category { get; set; }

    /// <summary>Необязательная формула результата или эффекта.</summary>
    public string? Formula { get; set; }

    /// <summary>Условие доступности в синтаксисе движка формул.</summary>
    public string? Requirements { get; set; }

    /// <summary>Понятное пользователю описание выбранной зависимости.</summary>
    public string? DependencyDescription { get; set; }
}

/// <summary>Одна разновидность денег персонажа.</summary>
public class CharacterCurrency : EntityBase
{
    /// <summary>Идентификатор владельца.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Владелец денег.</summary>
    public Character? Character { get; set; }

    /// <summary>Название валюты, например «Золотые монеты».</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Количество монет или единиц валюты.</summary>
    public decimal Amount { get; set; }
}

/// <summary>
/// Неигровой персонаж: житель мира, которым управляет мастер.
///
/// Принадлежность кампании задаётся составом кампании, а не полем: один и тот же
/// торговец встречается сразу в нескольких играх, оставаясь одной записью.
/// </summary>
public class Npc : ContentEntity
{
    /// <summary>Роль персонажа в мире: торговец, наставник, правитель.</summary>
    public string? Role { get; set; }

    /// <summary>Отношение персонажа к игрокам.</summary>
    public string? Attitude { get; set; }

    /// <summary>Идентификатор локации, в которой находится персонаж.</summary>
    public Guid? LocationId { get; set; }

    /// <summary>Локация, в которой находится персонаж.</summary>
    public Location? Location { get; set; }
}

/// <summary>
/// Монстр или противник.
/// </summary>
public class Monster : ContentEntity
{
    /// <summary>Уровень опасности, определяемый игровой системой.</summary>
    public string? Challenge { get; set; }

    /// <summary>Тип существа.</summary>
    public string? CreatureType { get; set; }

    /// <summary>Характеристики монстра в формате JSON.</summary>
    public string? StatBlockJson { get; set; }
}
