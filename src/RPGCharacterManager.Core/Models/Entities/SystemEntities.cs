namespace RPGCharacterManager.Core.Models.Entities;

/// <summary>
/// Игровая система: набор правил, характеристик и контента.
/// Каждая система хранится независимо и может быть отключена.
/// </summary>
public class GameSystem : EntityBase
{
    /// <summary>Название игровой системы.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Внутреннее имя игровой системы.</summary>
    public string SystemName { get; set; } = string.Empty;

    /// <summary>Версия игровой системы.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Автор игровой системы.</summary>
    public string? Author { get; set; }

    /// <summary>Описание игровой системы.</summary>
    public string? Description { get; set; }

    /// <summary>Ключ значка игровой системы.</summary>
    public string? Icon { get; set; }


    /// <summary>Система включена и доступна для выбора.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Формула переносимого веса персонажа, например <c>Сила * 10</c>.
    /// Пустое значение означает, что система не ограничивает ношу.
    ///
    /// Одна формула описывает любую систему вместимости: весовую, слотовую
    /// и объёмную. Что именно считается — килограммы, ячейки или литры —
    /// задаёт <see cref="WeightUnit"/>, а «вес» предмета выражен в тех же единицах.
    /// </summary>
    public string? CarryCapacityFormula { get; set; }

    /// <summary>
    /// Единица измерения веса и вместимости: «кг», «ячеек», «л».
    /// Пустое значение означает, что единица не подписывается.
    /// </summary>
    public string? WeightUnit { get; set; }

    /// <summary>
    /// Формула предела известных заклинаний, например <c>Интеллект + Уровень</c>.
    /// Пустое значение означает, что система не ограничивает изучение.
    /// </summary>
    public string? KnownSpellsFormula { get; set; }

    /// <summary>
    /// Формула предела подготовленных заклинаний.
    ///
    /// Заданная формула означает, что система пользуется подготовкой: применить
    /// можно только подготовленное заклинание, а количество подготовленных
    /// ограничено. Пустое значение означает, что подготовка — свободная пометка.
    /// </summary>
    public string? PreparedSpellsFormula { get; set; }

    /// <summary>
    /// Формула инициативы, например <c>1к20 + Ловкость</c>.
    ///
    /// Заданная формула означает, что система определяет порядок хода: режим
    /// мастера получает возможность бросить инициативу и вести очередь. Пустое
    /// значение означает, что порядка хода в системе нет, — а такие системы
    /// существуют, поэтому очередь не может быть встроена в приложение.
    /// </summary>
    public string? InitiativeFormula { get; set; }

    /// <summary>Контент-паки, относящиеся к этой игровой системе.</summary>
    public ICollection<ContentPack> ContentPacks { get; set; } = [];
}

/// <summary>
/// Контент-пак: набор игровых объектов, подключаемый и отключаемый целиком.
/// </summary>
public class ContentPack : EntityBase
{
    /// <summary>Название контент-пака.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Версия контент-пака.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Автор контент-пака.</summary>
    public string? Author { get; set; }

    /// <summary>Описание контент-пака.</summary>
    public string? Description { get; set; }

    /// <summary>Идентификатор игровой системы, к которой относится пак.</summary>
    public Guid? GameSystemId { get; set; }

    /// <summary>Игровая система, к которой относится пак.</summary>
    public GameSystem? GameSystem { get; set; }

    /// <summary>Пак включён, его содержимое доступно в приложении.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Лицензия, на условиях которой распространяется пак.</summary>
    public string? License { get; set; }

    /// <summary>
    /// Наименьшая версия приложения, с которой работает пак.
    /// Пустое значение означает, что пак не предъявляет требований.
    /// </summary>
    public string? RequiredVersion { get; set; }

    /// <summary>
    /// Паки, без которых этот не работает, в формате JSON.
    ///
    /// Хранятся вместе с паком, а не выводятся из содержимого: пак с оружием
    /// киберпанка бесполезен без пака с самим киберпанком, и узнать об этом
    /// приложение может только со слов автора.
    /// </summary>
    public string? DependenciesJson { get; set; }
}

