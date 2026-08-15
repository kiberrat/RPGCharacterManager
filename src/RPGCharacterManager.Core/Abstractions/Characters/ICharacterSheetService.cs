using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Characters;

/// <summary>
/// Названия разделов листа персонажа, имеющих особое значение.
/// </summary>
public static class SheetCategories
{
    /// <summary>
    /// Категория навыков, которые игровая система считает спасбросками.
    ///
    /// Спасбросок — та же проверка, что и навык, поэтому отдельной сущности он
    /// не требует: достаточно отнести навык к этой категории, и лист покажет его
    /// в собственном разделе перед остальными навыками.
    /// </summary>
    public const string SavingThrows = "Спасброски";

    /// <summary>Раздел, в который попадают объекты без указанной категории.</summary>
    public const string Other = "Прочее";
}

/// <summary>
/// Характеристика персонажа на листе.
/// </summary>
/// <param name="Id">Идентификатор характеристики.</param>
/// <param name="Name">Название характеристики.</param>
/// <param name="SystemName">Внутреннее имя, используемое формулами.</param>
/// <param name="Category">Раздел листа.</param>
/// <param name="BaseValue">Базовое значение, заданное пользователем.</param>
/// <param name="Value">Итоговое значение с учётом формул и правил.</param>
/// <param name="Modifier">Модификатор характеристики.</param>
/// <param name="IsDerived">Значение вычисляется формулой и не редактируется вручную.</param>
/// <param name="IsHidden">Служебная характеристика скрыта из обычного списка листа.</param>
/// <param name="Formula">Формула вычисления значения.</param>
/// <param name="Minimum">Наименьшее допустимое значение.</param>
/// <param name="Maximum">Наибольшее допустимое значение.</param>
public sealed record SheetAttributeValue(
    Guid Id,
    string Name,
    string SystemName,
    string Category,
    double BaseValue,
    double Value,
    double Modifier,
    bool IsDerived,
    bool IsHidden,
    string? Formula,
    double? Minimum,
    double? Maximum);

/// <summary>
/// Навык персонажа на листе.
/// </summary>
/// <param name="Id">Идентификатор навыка.</param>
/// <param name="Name">Название навыка.</param>
/// <param name="Category">Раздел листа.</param>
/// <param name="ProficiencyLevel">Уровень владения навыком.</param>
/// <param name="Bonus">Дополнительный бонус, заданный пользователем.</param>
/// <param name="Value">Итоговое значение навыка.</param>
/// <param name="LinkedAttributeName">Название связанной характеристики.</param>
/// <param name="Formula">Формула вычисления значения навыка.</param>
/// <param name="MaximumLevel">Наибольший уровень владения.</param>
public sealed record SheetSkill(
    Guid Id,
    string Name,
    string Category,
    int ProficiencyLevel,
    double Bonus,
    double Value,
    string? LinkedAttributeName,
    string? Formula,
    int? MaximumLevel);

/// <summary>
/// Ресурс персонажа на листе.
/// </summary>
/// <param name="Id">Идентификатор ресурса.</param>
/// <param name="Name">Название ресурса.</param>
/// <param name="Category">Раздел листа.</param>
/// <param name="Current">Текущее значение.</param>
/// <param name="Maximum">Максимальное значение.</param>
/// <param name="RestoreRule">Правило восстановления.</param>
public sealed record SheetResource(
    Guid Id,
    string Name,
    string Category,
    double Current,
    double Maximum,
    string? RestoreRule)
{
    /// <summary>Максимум, полученный из формулы до авторского переопределения.</summary>
    public double CalculatedMaximum { get; init; } = Maximum;

    /// <summary>Максимум задан вручную для этого персонажа.</summary>
    public bool IsMaximumOverridden { get; init; }
}

/// <summary>
/// Черта, полученная персонажем.
/// </summary>
/// <param name="Id">Идентификатор записи о полученной черте.</param>
/// <param name="TraitId">Идентификатор черты.</param>
/// <param name="Name">Название черты.</param>
/// <param name="Description">Описание черты.</param>
/// <param name="Category">Раздел листа.</param>
/// <param name="Source">Источник получения черты.</param>
/// <param name="Formula">Формула эффекта черты.</param>
/// <param name="RemainingUses">Оставшееся количество использований.</param>
/// <param name="IsActive">Черта действует.</param>
/// <param name="IsAvailable">Требования черты по-прежнему выполняются.</param>
/// <param name="UnavailableReason">Причина, по которой требования не выполняются.</param>
public sealed record SheetTrait(
    Guid Id,
    Guid TraitId,
    string Name,
    string? Description,
    string Category,
    string? Source,
    string? Formula,
    int RemainingUses,
    bool IsActive,
    bool IsAvailable,
    string? UnavailableReason);

/// <summary>
/// Способность, доступная персонажу.
/// </summary>
/// <param name="Id">Идентификатор способности.</param>
/// <param name="Name">Название способности.</param>
/// <param name="Description">Описание способности.</param>
/// <param name="Category">Раздел листа.</param>
/// <param name="Formula">Формула результата способности.</param>
/// <param name="ResourceName">Название расходуемого ресурса.</param>
/// <param name="ResourceCostFormula">Формула количества расходуемого ресурса.</param>
/// <param name="RechargeRule">Условие восстановления использований.</param>
/// <param name="Requirements">Требование, по которому способность получена.</param>
/// <param name="IsCustom">Способность создана пользователем для конкретного персонажа.</param>
/// <param name="IsAvailable">Условие способности сейчас выполнено.</param>
/// <param name="UnavailableReason">Причина недоступности авторской способности.</param>
/// <param name="DependencyDescription">Понятное описание зависимости.</param>
public sealed record SheetAbility(
    Guid Id,
    string Name,
    string? Description,
    string Category,
    string? Formula,
    string? ResourceName,
    string? ResourceCostFormula,
    string? RechargeRule,
    string? Requirements,
    bool IsCustom = false,
    bool IsAvailable = true,
    string? UnavailableReason = null,
    string? DependencyDescription = null);

/// <summary>
/// Пользовательское поле персонажа.
/// </summary>
/// <param name="DefinitionId">Идентификатор описания свойства.</param>
/// <param name="DisplayName">Отображаемое название поля.</param>
/// <param name="Description">Пояснение к полю.</param>
/// <param name="Category">Раздел листа.</param>
/// <param name="DataType">Тип данных поля.</param>
/// <param name="Value">Значение поля.</param>
public sealed record SheetCustomField(
    Guid DefinitionId,
    string DisplayName,
    string? Description,
    string Category,
    GameValueType DataType,
    string? Value);

/// <summary>
/// Лист персонажа: сам персонаж и все его вычисленные параметры.
/// </summary>
/// <param name="Character">Персонаж со связанными данными.</param>
/// <param name="Attributes">Характеристики.</param>
/// <param name="Skills">Навыки.</param>
/// <param name="Resources">Ресурсы.</param>
/// <param name="Traits">Полученные черты.</param>
/// <param name="Abilities">Доступные способности.</param>
/// <param name="CustomFields">Пользовательские поля.</param>
/// <param name="Issues">Замечания, найденные при расчёте.</param>
public sealed record CharacterSheet(
    Character Character,
    IReadOnlyList<SheetAttributeValue> Attributes,
    IReadOnlyList<SheetSkill> Skills,
    IReadOnlyList<SheetResource> Resources,
    IReadOnlyList<SheetTrait> Traits,
    IReadOnlyList<SheetAbility> Abilities,
    IReadOnlyList<SheetCustomField> CustomFields,
    IReadOnlyList<CharacterIssue> Issues);

/// <summary>
/// Лист персонажа: чтение вычисленных параметров и сохранение изменений.
///
/// Любое изменение листа немедленно приводит к полному пересчёту: значения
/// характеристик, навыков и ресурсов вычисляются формулами и правилами
/// игровой системы, а не редактируются напрямую.
/// </summary>
public interface ICharacterSheetService
{
    /// <summary>
    /// Загружает лист персонажа со всеми вычисленными значениями.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Лист персонажа либо описание ошибки.</returns>
    Task<Result<CharacterSheet>> LoadAsync(Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет изменения листа и пересчитывает персонажа.
    /// </summary>
    /// <param name="character">Изменённый персонаж, полученный при загрузке листа.</param>
    /// <param name="customFieldValues">Значения пользовательских полей.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пересчитанный лист персонажа либо описание ошибки.</returns>
    Task<Result<CharacterSheet>> SaveAsync(
        Character character,
        IReadOnlyDictionary<Guid, string?> customFieldValues,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает черты, которые персонаж может получить.
    /// Требования проверяются так же, как в мастере создания.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="search">Строка поиска по названию.</param>
    /// <param name="includeUnavailable">Показывать черты с невыполненными требованиями.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Доступные черты.</returns>
    Task<CharacterOptionPage> GetAvailableTraitsAsync(
        Character character,
        string? search,
        bool includeUnavailable,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает навыки, которыми персонаж может овладеть.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="search">Строка поиска по названию.</param>
    /// <param name="includeUnavailable">Показывать навыки с невыполненными требованиями.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Доступные навыки.</returns>
    Task<CharacterOptionPage> GetAvailableSkillsAsync(
        Character character,
        string? search,
        bool includeUnavailable,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт или изменяет описание пользовательского поля персонажей.
    /// </summary>
    /// <param name="definition">Описание поля.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат сохранения.</returns>
    Task<Result> SaveCustomFieldAsync(
        PropertyDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет описание пользовательского поля вместе со значениями всех персонажей.
    /// </summary>
    /// <param name="definitionId">Идентификатор описания поля.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат удаления.</returns>
    Task<Result> DeleteCustomFieldAsync(Guid definitionId, CancellationToken cancellationToken = default);

    /// <summary>Сохраняет авторскую способность конкретного персонажа.</summary>
    Task<Result> SaveCustomAbilityAsync(Guid characterId, CharacterCustomAbility ability,
        CancellationToken cancellationToken = default);

    /// <summary>Удаляет авторскую способность конкретного персонажа.</summary>
    Task<Result> DeleteCustomAbilityAsync(Guid characterId, Guid abilityId,
        CancellationToken cancellationToken = default);

    /// <summary>Создаёт или изменяет валюту конкретного персонажа.</summary>
    Task<Result> SaveCurrencyAsync(Guid characterId, CharacterCurrency currency,
        CancellationToken cancellationToken = default);

    /// <summary>Удаляет валюту конкретного персонажа.</summary>
    Task<Result> DeleteCurrencyAsync(Guid characterId, Guid currencyId,
        CancellationToken cancellationToken = default);

    /// <summary>Сохраняет текущее значение и необязательный максимум маны персонажа.</summary>
    Task<Result> SaveManaAsync(Guid characterId, decimal current, decimal? maximum,
        CancellationToken cancellationToken = default);
}
