using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Core.Abstractions.Characters;

/// <summary>
/// Вид шага мастера создания персонажа.
///
/// Шаг описывается данными, а не отдельным окном: игровая система добавляет
/// собственный шаг регистрацией описания, и он появляется в мастере без изменения кода.
/// </summary>
public enum CharacterStepKind
{
    /// <summary>Выбор игровой системы и источников контента.</summary>
    GameSystem = 0,

    /// <summary>Форма произвольных полей персонажа.</summary>
    Fields = 1,

    /// <summary>Выбор одного объекта игрового контента.</summary>
    SingleChoice = 2,

    /// <summary>Выбор нескольких объектов игрового контента.</summary>
    MultipleChoice = 3,

    /// <summary>Распределение значений характеристик.</summary>
    Attributes = 4,

    /// <summary>Предварительный просмотр и проверка перед созданием.</summary>
    Summary = 5,
}

/// <summary>
/// Способ распределения значений характеристик.
/// Состав способов задаётся шагом, поэтому игровая система может оставить только свои.
/// </summary>
public enum AttributeAssignmentMethod
{
    /// <summary>Ручной ввод значений.</summary>
    Manual = 0,

    /// <summary>Покупка значений за очки.</summary>
    PointBuy = 1,

    /// <summary>Распределение заранее заданного набора значений.</summary>
    StandardArray = 2,

    /// <summary>Случайный бросок для каждой характеристики.</summary>
    RandomRoll = 3,
}

/// <summary>
/// Важность замечания, найденного при проверке персонажа.
/// </summary>
public enum CharacterIssueSeverity
{
    /// <summary>Предупреждение: персонаж может быть создан.</summary>
    Warning = 0,

    /// <summary>Ошибка: создание невозможно, пока замечание не устранено.</summary>
    Error = 1,
}

/// <summary>
/// Замечание, найденное при проверке персонажа.
/// </summary>
/// <param name="Severity">Важность замечания.</param>
/// <param name="StepId">Шаг мастера, на котором замечание устраняется.</param>
/// <param name="Message">Описание замечания для пользователя.</param>
public sealed record CharacterIssue(CharacterIssueSeverity Severity, string StepId, string Message);

/// <summary>
/// Дополнительная строка карточки объекта: бонус, ограничение или требование.
/// </summary>
/// <param name="Label">Название сведения.</param>
/// <param name="Value">Значение сведения.</param>
public sealed record CharacterOptionDetail(string Label, string Value);

/// <summary>
/// Вариант выбора на шаге мастера: раса, класс, черта, заклинание и любой другой объект.
/// </summary>
/// <param name="Id">Идентификатор объекта.</param>
/// <param name="Name">Название объекта.</param>
/// <param name="Description">Описание объекта.</param>
/// <param name="IsAvailable">Объект доступен для выбора.</param>
/// <param name="UnavailableReason">Причина недоступности объекта.</param>
/// <param name="Details">Дополнительные сведения карточки.</param>
/// <param name="Image">Путь к изображению объекта.</param>
public sealed record CharacterOption(
    Guid Id,
    string Name,
    string? Description,
    bool IsAvailable,
    string? UnavailableReason,
    IReadOnlyList<CharacterOptionDetail> Details,
    string? Image);

/// <summary>
/// Страница вариантов выбора вместе с общим количеством найденных объектов.
/// </summary>
/// <param name="Options">Варианты текущей страницы.</param>
/// <param name="TotalCount">Общее количество подходящих объектов.</param>
public sealed record CharacterOptionPage(IReadOnlyList<CharacterOption> Options, int TotalCount);

/// <summary>
/// Игровая система, доступная для выбора в мастере.
/// </summary>
/// <param name="Id">Идентификатор игровой системы.</param>
/// <param name="Name">Название игровой системы.</param>
/// <param name="Description">Описание игровой системы.</param>
/// <param name="Version">Версия игровой системы.</param>
public sealed record GameSystemOption(Guid Id, string Name, string? Description, string Version);

/// <summary>
/// Источник контента: контент-пак, объекты которого допускаются к выбору.
/// </summary>
/// <param name="Id">Идентификатор контент-пака.</param>
/// <param name="Name">Название контент-пака.</param>
/// <param name="Description">Описание контент-пака.</param>
/// <param name="Version">Версия контент-пака.</param>
public sealed record ContentSourceOption(Guid Id, string Name, string? Description, string Version);

/// <summary>
/// Параметры шага распределения характеристик.
///
/// Приложение не содержит правил конкретной игры, поэтому бюджет очков, набор
/// значений и формула броска задаются игровой системой либо самим пользователем
/// прямо в мастере.
/// </summary>
public sealed record AttributeStepOptions
{
    /// <summary>Способы распределения, доступные на шаге.</summary>
    public IReadOnlyList<AttributeAssignmentMethod> Methods { get; init; } =
    [
        AttributeAssignmentMethod.Manual,
        AttributeAssignmentMethod.PointBuy,
        AttributeAssignmentMethod.StandardArray,
        AttributeAssignmentMethod.RandomRoll,
    ];

    /// <summary>Бюджет очков при покупке. Нулевое значение означает, что бюджет не задан.</summary>
    public int PointBudget { get; init; }

    /// <summary>Набор значений для распределения, перечисленных через запятую.</summary>
    public string StandardArray { get; init; } = string.Empty;

    /// <summary>Формула случайного броска для одной характеристики.</summary>
    public string RollFormula { get; init; } = "3к6";
}

/// <summary>
/// Описание шага мастера создания персонажа.
///
/// Мастер строится по набору таких описаний: он не содержит перечня своих страниц,
/// поэтому игровая система, контент-пак или плагин добавляет собственный шаг
/// регистрацией <see cref="ICharacterStepProvider"/>, не изменяя ни мастер, ни ядро.
/// </summary>
public sealed record CharacterStepDefinition
{
    /// <summary>Внутренний идентификатор шага.</summary>
    public required string Id { get; init; }

    /// <summary>Название шага, отображаемое в списке страниц.</summary>
    public required string Title { get; init; }

    /// <summary>Пояснение к шагу.</summary>
    public required string Description { get; init; }

    /// <summary>Вид шага.</summary>
    public required CharacterStepKind Kind { get; init; }

    /// <summary>Порядок шага в мастере.</summary>
    public int Order { get; init; }

    /// <summary>Шаг обязателен: без выбора мастер не пропускает дальше.</summary>
    public bool IsRequired { get; init; }

    /// <summary>Поля формы для шага вида <see cref="CharacterStepKind.Fields"/>.</summary>
    public IReadOnlyList<IContentField> Fields { get; init; } = [];

    /// <summary>
    /// Тип сущности, объекты которой предлагаются на шагах выбора.
    /// Все такие сущности наследуют <see cref="ContentEntity"/>, поэтому отбор
    /// по игровой системе и источникам выполняется единообразно.
    /// </summary>
    public Type? OptionEntityType { get; init; }

    /// <summary>Идентификатор шага, от выбора на котором зависит состав вариантов.</summary>
    public string? ParentStepId { get; init; }

    /// <summary>
    /// Имя свойства объекта, хранящего ссылку на выбор родительского шага.
    /// Отбор выполняется запросом к базе данных, поэтому список подклассов
    /// не требует загрузки подклассов остальных классов.
    /// </summary>
    public string? ParentPropertyName { get; init; }

    /// <summary>
    /// Связанные данные, загружаемые вместе с объектами шага.
    /// Позволяют показать в карточке название связанного объекта, например
    /// основную характеристику класса.
    /// </summary>
    public IReadOnlyList<string> IncludePaths { get; init; } = [];

    /// <summary>Чтение выражения требований объекта.</summary>
    public Func<ContentEntity, string?>? ReadRequirements { get; init; }

    /// <summary>
    /// Чтение объекта того же вида, который должен быть выбран раньше данного.
    /// Позволяет выстраивать цепочки: черта доступна только после получения другой черты.
    /// </summary>
    public Func<ContentEntity, Guid?>? ReadRequiredOption { get; init; }

    /// <summary>Сведения объекта, отображаемые в карточке варианта.</summary>
    public Func<ContentEntity, IReadOnlyList<CharacterOptionDetail>>? ReadDetails { get; init; }

    /// <summary>Запись выбора шага <see cref="CharacterStepKind.SingleChoice"/> в персонажа.</summary>
    public Action<Character, Guid?>? WriteSelection { get; init; }

    /// <summary>Чтение выбора шага <see cref="CharacterStepKind.SingleChoice"/> из персонажа.</summary>
    public Func<Character, Guid?>? ReadSelection { get; init; }

    /// <summary>
    /// Имя переменной, под которым внутреннее имя выбранного объекта доступно
    /// формулам и требованиям, например «раса» или «класс».
    /// </summary>
    public string? VariableName { get; init; }

    /// <summary>
    /// Запись выбора шага <see cref="CharacterStepKind.MultipleChoice"/> в персонажа.
    ///
    /// Запись обязана только добавлять недостающие записи и не удалять существующие:
    /// в один список персонажа могут писать несколько шагов, поэтому удаление чужих
    /// записей одним шагом уничтожило бы выбор, сделанный на другом.
    /// </summary>
    public Action<Character, IReadOnlyCollection<Guid>>? WriteSelections { get; init; }

    /// <summary>Чтение выбора шага <see cref="CharacterStepKind.MultipleChoice"/> из персонажа.</summary>
    public Func<Character, IEnumerable<Guid>>? ReadSelections { get; init; }

    /// <summary>
    /// Формула количества объектов, которые разрешено выбрать на шаге множественного выбора.
    /// Пустое значение снимает ограничение.
    /// </summary>
    public string? SelectionLimitFormula { get; init; }

    /// <summary>Параметры шага распределения характеристик.</summary>
    public AttributeStepOptions? AttributeOptions { get; init; }
}

/// <summary>
/// Поставщик шагов мастера создания персонажа.
///
/// Подсистема, добавляющая собственные шаги, регистрирует свою реализацию
/// в контейнере зависимостей; мастер объединяет шаги всех поставщиков по порядку.
/// </summary>
public interface ICharacterStepProvider
{
    /// <summary>
    /// Возвращает предоставляемые шаги.
    /// </summary>
    /// <returns>Последовательность описаний шагов.</returns>
    IEnumerable<CharacterStepDefinition> GetSteps();
}
