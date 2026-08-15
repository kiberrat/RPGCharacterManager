using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Characters;

/// <summary>
/// Мастер создания персонажа.
///
/// Служба не знает состава своих страниц: он собирается из описаний, полученных
/// от всех зарегистрированных <see cref="ICharacterStepProvider"/>. Правила создания
/// персонажа полностью определяются выбранной игровой системой, как требует
/// документ 006_Конструктор_персонажа.md.
/// </summary>
public interface ICharacterBuilderService
{
    /// <summary>Шаги мастера в порядке прохождения.</summary>
    IReadOnlyList<CharacterStepDefinition> Steps { get; }

    /// <summary>
    /// Возвращает игровые системы, доступные для выбора.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список игровых систем.</returns>
    Task<IReadOnlyList<GameSystemOption>> GetGameSystemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает источники контента выбранной игровой системы.
    /// </summary>
    /// <param name="gameSystemId">Идентификатор игровой системы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список контент-паков.</returns>
    Task<IReadOnlyList<ContentSourceOption>> GetSourcesAsync(
        Guid? gameSystemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает варианты выбора шага с уже выполненной проверкой требований.
    /// </summary>
    /// <param name="step">Описание шага.</param>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="search">Строка поиска по названию.</param>
    /// <param name="includeUnavailable">Показывать варианты, требования которых не выполнены.</param>
    /// <param name="limit">Наибольшее количество возвращаемых вариантов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница вариантов и общее количество подходящих объектов.</returns>
    Task<CharacterOptionPage> GetOptionsAsync(
        CharacterStepDefinition step,
        CharacterDraft draft,
        string? search,
        bool includeUnavailable,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает характеристики, доступные создаваемому персонажу.
    /// </summary>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список характеристик в порядке отображения.</returns>
    Task<IReadOnlyList<AttributeDefinition>> GetAttributesAsync(
        CharacterDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Пересчитывает параметры создаваемого персонажа.
    /// Выполняется после каждого изменения, как требует документ 006.
    /// </summary>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат расчёта.</returns>
    Task<CharacterCalculation> CalculateAsync(
        CharacterDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Применяет правила разового события — создания персонажа или повышения
    /// уровня — к базовым значениям характеристик черновика.
    ///
    /// Полученные изменения постоянны: последующие пересчёты их сохраняют,
    /// а правило события не применяется повторно и не накапливает бонус.
    /// </summary>
    /// <param name="draft">Изменяемый персонаж.</param>
    /// <param name="trigger">Ключ события.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Названия применённых правил.</returns>
    Task<IReadOnlyList<string>> ApplyEventAsync(
        CharacterDraft draft,
        string trigger,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Применяет к базовым значениям характеристик заданный набор правил.
    ///
    /// От <see cref="ApplyEventAsync"/> отличается только тем, откуда берутся
    /// правила: там их даёт событие, здесь — вызывающая сторона. Так макрос
    /// выполняется тем же движком и тем же способом, что и правило события
    /// (решение Р-97).
    /// </summary>
    /// <param name="draft">Изменяемый персонаж.</param>
    /// <param name="trigger">Ключ события, записываемый в отчёт.</param>
    /// <param name="rules">Применяемые правила.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Названия применённых правил.</returns>
    Task<IReadOnlyList<string>> ApplyRulesAsync(
        CharacterDraft draft,
        string trigger,
        IReadOnlyList<Rules.RuleDefinition> rules,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт черновик по сохранённому персонажу: переносит выбор шагов,
    /// базовые значения характеристик и источники контента.
    /// </summary>
    /// <param name="character">Сохранённый персонаж со связанными данными.</param>
    /// <returns>Черновик, пригодный для пересчёта и проверки.</returns>
    CharacterDraft CreateDraft(Character character);

    /// <summary>
    /// Создаёт источник значений переменных персонажа.
    ///
    /// Доступны значения характеристик по внутренним именам, уровень персонажа
    /// и внутренние имена выбранных расы, класса, подкласса и происхождения.
    /// Тот же источник используют требования объектов, поэтому проверка требования
    /// на листе персонажа и в мастере даёт одинаковый результат.
    /// </summary>
    /// <param name="draft">Персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Объект правил персонажа, пригодный и как источник значений переменных.</returns>
    Task<IRuleTarget> CreateContextAsync(
        CharacterDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет требование объекта.
    /// </summary>
    /// <param name="requirement">Выражение требования. Пустое значение означает отсутствие требований.</param>
    /// <param name="context">Источник значений переменных персонажа.</param>
    /// <returns>Причина невыполнения требования либо <see langword="null"/>.</returns>
    string? CheckRequirement(string? requirement, IFormulaContext context);

    /// <summary>
    /// Проверяет создаваемого персонажа: обязательные поля, требования,
    /// ограничения характеристик и превышение количества выборов.
    /// </summary>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список найденных замечаний.</returns>
    Task<IReadOnlyList<CharacterIssue>> ValidateAsync(
        CharacterDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Записывает выбор шага одиночного выбора и переносит его в персонажа.
    /// Выбор на родительском шаге сбрасывает зависящие от него выборы.
    /// </summary>
    /// <param name="step">Описание шага.</param>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="optionId">Идентификатор выбранного объекта либо <see langword="null"/>.</param>
    void SetSelection(CharacterStepDefinition step, CharacterDraft draft, Guid? optionId);

    /// <summary>
    /// Возвращает количество объектов, которые разрешено выбрать на шаге.
    /// </summary>
    /// <param name="step">Описание шага.</param>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ограничение либо <see langword="null"/>, если оно не задано.</returns>
    Task<int?> GetSelectionLimitAsync(
        CharacterStepDefinition step,
        CharacterDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт персонажа: сохраняет запись, значения характеристик, навыки, черты,
    /// заклинания и ресурсы, применяет правила события создания и записывает
    /// произошедшее в журнал изменений.
    /// </summary>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор созданного персонажа либо описание ошибки.</returns>
    Task<Result<Guid>> CreateAsync(CharacterDraft draft, CancellationToken cancellationToken = default);
}
