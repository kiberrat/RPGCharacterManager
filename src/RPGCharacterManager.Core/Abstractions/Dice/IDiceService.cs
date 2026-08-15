using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Dice;

/// <summary>
/// Способ выполнения броска.
///
/// Преимущество и помеха описаны как отдельный способ, а не как формула: они
/// применимы к любому броску любой игровой системы и не должны требовать от
/// пользователя переписывать выражение.
/// </summary>
public enum RollMode
{
    /// <summary>Обычный бросок: выражение вычисляется один раз.</summary>
    Normal = 0,

    /// <summary>Преимущество: выражение вычисляется дважды, берётся лучший итог.</summary>
    Advantage = 1,

    /// <summary>Помеха: выражение вычисляется дважды, берётся худший итог.</summary>
    Disadvantage = 2,
}

/// <summary>
/// Кубик, доступный для броска.
/// </summary>
/// <param name="Id">Идентификатор пользовательского кубика; у встроенного — <see langword="null"/>.</param>
/// <param name="Name">Название кубика: «d20» или «Кристалл судьбы».</param>
/// <param name="Sides">Количество граней.</param>
/// <param name="Color">Цвет кубика в интерфейсе.</param>
/// <param name="Description">Описание кубика.</param>
public sealed record DieDefinition(
    Guid? Id,
    string Name,
    int Sides,
    string? Color,
    string? Description)
{
    /// <summary>Кубик создан пользователем.</summary>
    public bool IsCustom => Id.HasValue;

    /// <summary>Обозначение кубика в формуле: <c>d20</c>.</summary>
    public string Notation => DiceNotation.Die(Sides);
}

/// <summary>
/// Один выпавший кубик.
/// </summary>
/// <param name="Sides">Количество граней кубика.</param>
/// <param name="Value">Выпавшее значение.</param>
public sealed record DieCast(int Sides, int Value);

/// <summary>
/// Одна попытка броска. При преимуществе и помехе попыток две.
/// </summary>
/// <param name="Total">Итог вычисления выражения.</param>
/// <param name="Dice">Выпавшие кубики в порядке броска.</param>
/// <param name="IsChosen">Итог этой попытки принят за результат броска.</param>
public sealed record RollAttempt(double Total, IReadOnlyList<DieCast> Dice, bool IsChosen);

/// <summary>
/// Результат броска.
/// </summary>
/// <param name="Id">Идентификатор записи журнала бросков.</param>
/// <param name="Title">Название броска: «Проверка Скрытности» или пусто.</param>
/// <param name="Expression">Выражение броска.</param>
/// <param name="Mode">Способ выполнения броска.</param>
/// <param name="Total">Принятый итог.</param>
/// <param name="Attempts">Выполненные попытки.</param>
/// <param name="Timestamp">Момент броска.</param>
/// <param name="IsFavorite">Бросок отмечен как любимый.</param>
/// <param name="CharacterId">Персонаж, для которого выполнен бросок.</param>
public sealed record RollOutcome(
    Guid Id,
    string? Title,
    string Expression,
    RollMode Mode,
    double Total,
    IReadOnlyList<RollAttempt> Attempts,
    DateTimeOffset Timestamp,
    bool IsFavorite,
    Guid? CharacterId)
{
    /// <summary>Кубики принятой попытки.</summary>
    public IReadOnlyList<DieCast> Dice =>
        Attempts.FirstOrDefault(attempt => attempt.IsChosen)?.Dice ?? [];
}

/// <summary>
/// Запрос броска.
/// </summary>
/// <param name="Expression">Выражение броска: <c>2d6 + Сила</c>.</param>
/// <param name="Mode">Способ выполнения броска.</param>
/// <param name="Title">Название броска для журнала.</param>
/// <param name="CharacterId">
/// Персонаж, значения которого доступны выражению. Без персонажа в выражении
/// допустимы только кубики и числа.
/// </param>
public sealed record RollRequest(
    string Expression,
    RollMode Mode = RollMode.Normal,
    string? Title = null,
    Guid? CharacterId = null);

/// <summary>
/// Запись обозначений бросков.
/// Собрана в одном месте, чтобы кнопки кубиков, журнал и подсказки писали одинаково.
/// </summary>
public static class DiceNotation
{
    /// <summary>Буква, отделяющая количество кубиков от количества граней.</summary>
    public const char Separator = 'd';

    /// <summary>
    /// Возвращает обозначение одного кубика: <c>d20</c>.
    /// </summary>
    /// <param name="sides">Количество граней.</param>
    /// <returns>Обозначение кубика.</returns>
    public static string Die(int sides) => $"{Separator}{sides}";

    /// <summary>
    /// Буквы, которыми в выражении обозначается кубик.
    ///
    /// Русская «к» принята наравне с латинской «d»: пользователь пишет формулы
    /// на своём языке. Перечень задан здесь, а не в разборщике выражений, чтобы
    /// запись и чтение броска не могли разойтись.
    /// </summary>
    public static IReadOnlyList<char> Separators { get; } = ['d', 'D', 'к', 'К', 'д', 'Д'];

    /// <summary>
    /// Определяет, обозначает ли знак кубик.
    /// </summary>
    /// <param name="value">Проверяемый знак.</param>
    /// <returns><see langword="true"/>, если знак обозначает кубик.</returns>
    public static bool IsSeparator(char value) => Separators.Contains(value);

    /// <summary>
    /// Возвращает обозначение броска нескольких кубиков: <c>3d6</c>.
    /// </summary>
    /// <param name="count">Количество кубиков.</param>
    /// <param name="sides">Количество граней.</param>
    /// <returns>Выражение броска.</returns>
    public static string Throw(int count, int sides) => $"{count}{Separator}{sides}";

    /// <summary>
    /// Добавляет кубики к выражению броска.
    ///
    /// Позволяет собрать смешанный бросок вида <c>2d10 + 4d4 + 15d8</c> нажатиями
    /// на кубики. Кубики того же вида, что и в конце выражения, объединяются
    /// с ними: пять нажатий на d6 дают <c>5d6</c>, а не пять слагаемых подряд.
    /// </summary>
    /// <param name="expression">Выражение, к которому добавляются кубики.</param>
    /// <param name="count">Количество добавляемых кубиков.</param>
    /// <param name="sides">Количество граней.</param>
    /// <returns>Выражение с добавленными кубиками.</returns>
    public static string Add(string? expression, int count, int sides)
    {
        var current = expression?.TrimEnd() ?? string.Empty;

        if (current.Length == 0)
        {
            return Throw(count, sides);
        }

        return TryMerge(current, count, sides, out var merged)
            ? merged
            : $"{current} + {Throw(count, sides)}";
    }

    /// <summary>
    /// Пытается объединить добавляемые кубики с последней группой выражения.
    /// </summary>
    /// <param name="expression">Выражение без хвостовых пробелов.</param>
    /// <param name="count">Количество добавляемых кубиков.</param>
    /// <param name="sides">Количество граней.</param>
    /// <param name="merged">Объединённое выражение.</param>
    /// <returns><see langword="true"/>, если объединение удалось.</returns>
    private static bool TryMerge(string expression, int count, int sides, out string merged)
    {
        merged = string.Empty;

        var position = expression.Length;
        var sidesEnd = position;

        while (position > 0 && char.IsAsciiDigit(expression[position - 1]))
        {
            position--;
        }

        if (position == sidesEnd
            || !int.TryParse(expression[position..sidesEnd], out var lastSides)
            || lastSides != sides
            || position == 0
            || !IsSeparator(expression[position - 1]))
        {
            return false;
        }

        // Буква берётся из самого выражения: пользователь, написавший «2к6»,
        // не должен получить «5d6» — это его запись, а не приложения.
        var separator = expression[position - 1];

        position--;

        var countEnd = position;

        while (position > 0 && char.IsAsciiDigit(expression[position - 1]))
        {
            position--;
        }

        // Запись без количества означает один кубик: d20 равнозначно 1d20.
        var lastCount = 1;

        if (position != countEnd && !int.TryParse(expression[position..countEnd], out lastCount))
        {
            return false;
        }

        // Перед группой не должно быть имени: «Ловкость» оканчивается на «ь»,
        // но переменная вроде «урон2d6» — не бросок, и дописывать в неё нельзя.
        if (position > 0 && (char.IsLetterOrDigit(expression[position - 1]) || expression[position - 1] == '_'))
        {
            return false;
        }

        merged = $"{expression[..position]}{lastCount + count}{separator}{sides}";

        return true;
    }
}

/// <summary>
/// Броски кубиков: единственный вход подсистемы бросков.
///
/// Служба не содержит правил какой-либо игры: бросок — это выражение, вычисленное
/// единым движком формул, поэтому «1d20 + Ловкость» и «3d6 успехов» выполняются
/// одинаково. Каждый бросок попадает в журнал вместе с выпавшими кубиками.
/// </summary>
public interface IDiceService
{
    /// <summary>
    /// Возвращает кубики, доступные для броска: встроенные и созданные пользователем.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список кубиков.</returns>
    Task<Result<IReadOnlyList<DieDefinition>>> GetDiceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполняет бросок и записывает его в журнал.
    /// </summary>
    /// <param name="request">Запрос броска.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат броска либо описание ошибки выражения.</returns>
    Task<Result<RollOutcome>> RollAsync(RollRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает последние броски в порядке от новых к старым.
    /// </summary>
    /// <param name="characterId">Персонаж; <see langword="null"/> — броски всех персонажей.</param>
    /// <param name="limit">Наибольшее количество записей.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Записи журнала бросков.</returns>
    Task<Result<IReadOnlyList<RollOutcome>>> GetHistoryAsync(
        Guid? characterId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает любимые броски.
    /// </summary>
    /// <param name="characterId">Персонаж; <see langword="null"/> — броски всех персонажей.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Любимые броски.</returns>
    Task<Result<IReadOnlyList<RollOutcome>>> GetFavoritesAsync(
        Guid? characterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет бросок в любимые или убирает из них.
    ///
    /// Любимый бросок — это запись журнала: у неё уже есть выражение, способ броска
    /// и название, поэтому повтор ничего не восстанавливает по частям. Любимые записи
    /// не удаляются при очистке журнала и не вытесняются по достижении предела.
    /// </summary>
    /// <param name="rollId">Идентификатор записи журнала.</param>
    /// <param name="isFavorite">Бросок должен стать любимым.</param>
    /// <param name="title">Новое название броска; <see langword="null"/> — оставить прежнее.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Изменённая запись либо описание ошибки.</returns>
    Task<Result<RollOutcome>> SetFavoriteAsync(
        Guid rollId,
        bool isFavorite,
        string? title = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет запись журнала бросков.
    /// </summary>
    /// <param name="rollId">Идентификатор записи.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат удаления.</returns>
    Task<Result> DeleteAsync(Guid rollId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Очищает журнал бросков, сохраняя любимые броски.
    /// </summary>
    /// <param name="characterId">Персонаж; <see langword="null"/> — броски всех персонажей.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество удалённых записей.</returns>
    Task<Result<int>> ClearHistoryAsync(Guid? characterId, CancellationToken cancellationToken = default);
}
