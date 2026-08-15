namespace RPGCharacterManager.Core.Models.Entities;

/// <summary>
/// Кампания: игра, которую ведёт мастер, с собственным составом и хронологией.
///
/// Кампания не хранит игровые объекты, а ссылается на них записями состава
/// <see cref="CampaignMember"/>. Поэтому один и тот же монстр, квест или локация
/// участвуют в нескольких кампаниях, оставаясь единственной записью в базе.
/// </summary>
public class Campaign : EntityBase
{
    /// <summary>Название кампании.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Описание кампании.</summary>
    public string? Description { get; set; }

    /// <summary>Мир или сеттинг кампании.</summary>
    public string? World { get; set; }

    /// <summary>
    /// Игровая дата начала кампании.
    ///
    /// Хранится текстом: календарь игрового мира задаёт сам мастер, и «15 день
    /// месяца Пепла, 1250 год» — такая же законная дата, как обычная.
    /// </summary>
    public string? StartDate { get; set; }

    /// <summary>Заметки мастера по кампании.</summary>
    public string? Notes { get; set; }

    /// <summary>Идентификатор игровой системы кампании.</summary>
    public Guid? GameSystemId { get; set; }

    /// <summary>Игровая система кампании.</summary>
    public GameSystem? GameSystem { get; set; }

    /// <summary>Путь к изображению кампании.</summary>
    public string? Image { get; set; }

    /// <summary>Кампания активна.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Состав кампании: персонажи, NPC, монстры, квесты, локации.</summary>
    public ICollection<CampaignMember> Members { get; set; } = [];

    /// <summary>События кампании в порядке игровой хронологии.</summary>
    public ICollection<CampaignEvent> Events { get; set; } = [];
}

/// <summary>
/// Участник кампании: ссылка на игровой объект любого вида.
///
/// Вид объекта хранится идентификатором, а не отдельной таблицей на каждый вид:
/// так в кампанию входит и монстр, и квест, и предмет, и всё, что появится позже,
/// без изменения схемы базы данных (решение Р-89).
/// </summary>
public class CampaignMember : EntityBase
{
    /// <summary>Идентификатор кампании.</summary>
    public Guid CampaignId { get; set; }

    /// <summary>Кампания.</summary>
    public Campaign? Campaign { get; set; }

    /// <summary>Идентификатор вида объекта: «npcs», «monsters», «characters».</summary>
    public string ObjectKind { get; set; } = string.Empty;

    /// <summary>Идентификатор самого объекта.</summary>
    public Guid ObjectId { get; set; }

    /// <summary>Роль в кампании: имя игрока, роль NPC, назначение локации.</summary>
    public string? Role { get; set; }

    /// <summary>Заметки мастера об участнике.</summary>
    public string? Notes { get; set; }

    /// <summary>Порядок отображения в своей группе.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Очередь хода: порядок, в котором участники действуют в бою.
///
/// Очередь принадлежит кампании, а не бою: мастер закрывает приложение между
/// сессиями и ожидает найти порядок хода там же, где оставил. Очередь без
/// кампании — одна на приложение и обслуживает игру, для которой кампанию
/// не заводили.
/// </summary>
public class InitiativeTracker : EntityBase
{
    /// <summary>Идентификатор кампании; пусто — очередь вне кампаний.</summary>
    public Guid? CampaignId { get; set; }

    /// <summary>Кампания.</summary>
    public Campaign? Campaign { get; set; }

    /// <summary>Номер текущего раунда, начиная с первого.</summary>
    public int Round { get; set; } = 1;

    /// <summary>Участники очереди.</summary>
    public ICollection<InitiativeEntry> Entries { get; set; } = [];
}

/// <summary>
/// Участник очереди хода.
/// </summary>
public class InitiativeEntry : EntityBase
{
    /// <summary>Идентификатор очереди.</summary>
    public Guid TrackerId { get; set; }

    /// <summary>Очередь хода.</summary>
    public InitiativeTracker? Tracker { get; set; }

    /// <summary>Идентификатор персонажа.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Персонаж.</summary>
    public Character? Character { get; set; }

    /// <summary>
    /// Значение инициативы.
    ///
    /// Хранится числом, а не броском: мастер вправе назначить значение вручную,
    /// и приложение не должно отличать назначенное от брошенного.
    /// </summary>
    public double Value { get; set; }

    /// <summary>Порядок в очереди: чем меньше, тем раньше ход.</summary>
    public int SortOrder { get; set; }

    /// <summary>Сейчас ходит этот участник.</summary>
    public bool IsCurrent { get; set; }
}

/// <summary>
/// Событие кампании: точка на игровой хронологии.
/// </summary>
public class CampaignEvent : EntityBase
{
    /// <summary>Идентификатор кампании.</summary>
    public Guid CampaignId { get; set; }

    /// <summary>Кампания.</summary>
    public Campaign? Campaign { get; set; }

    /// <summary>Название события.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Описание события.</summary>
    public string? Description { get; set; }

    /// <summary>Игровая дата события в календаре игрового мира.</summary>
    public string? GameDate { get; set; }

    /// <summary>
    /// Порядок события на хронологии.
    ///
    /// Порядок задаётся отдельно от даты: даты игровых миров записываются
    /// произвольно и сравнению не поддаются.
    /// </summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Локация игрового мира: город, планета, подземелье, станция.
/// </summary>
public class Location : ContentEntity
{
    /// <summary>Вид локации: город, замок, планета, корабль.</summary>
    public string? Kind { get; set; }

    /// <summary>Идентификатор родительской локации.</summary>
    public Guid? ParentLocationId { get; set; }

    /// <summary>Родительская локация.</summary>
    public Location? ParentLocation { get; set; }
}

/// <summary>
/// Квест: задание, которое получают игроки.
/// </summary>
public class Quest : ContentEntity
{
    /// <summary>Состояние выполнения.</summary>
    public QuestStatus Status { get; set; } = QuestStatus.Planned;

    /// <summary>Награда за выполнение.</summary>
    public string? Reward { get; set; }

    /// <summary>Идентификатор выдавшего задание неигрового персонажа.</summary>
    public Guid? GiverId { get; set; }

    /// <summary>Неигровой персонаж, выдавший задание.</summary>
    public Npc? Giver { get; set; }

    /// <summary>Идентификатор локации задания.</summary>
    public Guid? LocationId { get; set; }

    /// <summary>Локация задания.</summary>
    public Location? Location { get; set; }

    /// <summary>Этапы задания в порядке выполнения.</summary>
    public ICollection<QuestStep> Steps { get; set; } = [];
}

/// <summary>
/// Состояние выполнения квеста.
/// </summary>
public enum QuestStatus
{
    /// <summary>Задание ещё не выдано игрокам.</summary>
    Planned = 0,

    /// <summary>Задание выполняется.</summary>
    Active = 1,

    /// <summary>Задание выполнено.</summary>
    Completed = 2,

    /// <summary>Задание провалено.</summary>
    Failed = 3,
}

/// <summary>
/// Этап квеста.
/// </summary>
public class QuestStep : EntityBase
{
    /// <summary>Идентификатор квеста.</summary>
    public Guid QuestId { get; set; }

    /// <summary>Квест.</summary>
    public Quest? Quest { get; set; }

    /// <summary>Название этапа.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Описание этапа.</summary>
    public string? Description { get; set; }

    /// <summary>Этап выполнен.</summary>
    public bool IsDone { get; set; }

    /// <summary>Порядок выполнения.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Заметка пользователя.
/// </summary>
public class Note : EntityBase
{
    /// <summary>Заголовок заметки.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Текст заметки.</summary>
    public string? Text { get; set; }

    /// <summary>Идентификатор персонажа, к которому относится заметка.</summary>
    public Guid? CharacterId { get; set; }

    /// <summary>Идентификатор кампании, к которой относится заметка.</summary>
    public Guid? CampaignId { get; set; }
}

/// <summary>
/// Запись журнала бросков кубиков.
/// </summary>
public class DiceRoll : EntityBase
{
    /// <summary>Идентификатор персонажа, выполнившего бросок.</summary>
    public Guid? CharacterId { get; set; }

    /// <summary>Описание броска, например «Проверка Скрытности».</summary>
    public string? Label { get; set; }

    /// <summary>Формула броска.</summary>
    public string Formula { get; set; } = string.Empty;

    /// <summary>Итоговый результат броска.</summary>
    public double Result { get; set; }

    /// <summary>Подробности броска: выпавшие значения и применённые бонусы.</summary>
    public string? DetailsJson { get; set; }

    /// <summary>Бросок выполнен с преимуществом.</summary>
    public bool HasAdvantage { get; set; }

    /// <summary>Бросок выполнен с помехой.</summary>
    public bool HasDisadvantage { get; set; }

    /// <summary>Бросок отмечен как избранный.</summary>
    public bool IsFavorite { get; set; }
}

/// <summary>
/// Запись журнала действий приложения и игровых событий.
/// </summary>
public class HistoryEntry : EntityBase
{
    /// <summary>Идентификатор персонажа, к которому относится запись.</summary>
    public Guid? CharacterId { get; set; }

    /// <summary>Идентификатор кампании, к которой относится запись.</summary>
    public Guid? CampaignId { get; set; }

    /// <summary>Код действия: изменение здоровья, применение заклинания, смена экипировки.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Описание записи для пользователя.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Название того, чего касается событие: заклинания, ресурса, предмета, оружия.
    ///
    /// Хранится отдельно от описания, потому что описание — предложение для
    /// человека: «Применено „Огненный шар“ (уровень 3)». Сосчитать по нему, сколько
    /// раз применялось заклинание, нельзя — то же заклинание на другом уровне даёт
    /// другое предложение. Название же одинаково у всех своих событий.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Числовая величина события: изменение ресурса, нанесённый урон.
    ///
    /// Значения «было» и «стало» записаны строками, потому что показываются
    /// пользователю в его же виде. Складывать строки нельзя, а величина события —
    /// это число, поэтому она хранится числом.
    /// </summary>
    public double? Amount { get; set; }

    /// <summary>Значение до изменения.</summary>
    public string? OldValue { get; set; }

    /// <summary>Значение после изменения.</summary>
    public string? NewValue { get; set; }
}
