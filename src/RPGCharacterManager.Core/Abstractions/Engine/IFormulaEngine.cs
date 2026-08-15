using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Engine;

/// <summary>
/// Источник значений переменных при вычислении формулы.
///
/// Реализация предоставляет движку характеристики, ресурсы, уровень персонажа
/// и любые другие именованные величины. Движок не знает, откуда берутся значения,
/// поэтому одна и та же формула применима к персонажу, монстру или предмету.
/// </summary>
public interface IFormulaContext
{
    /// <summary>
    /// Возвращает значение переменной.
    /// </summary>
    /// <param name="name">Имя переменной.</param>
    /// <param name="value">Найденное значение.</param>
    /// <returns><see langword="true"/>, если переменная существует.</returns>
    bool TryGetVariable(string name, out FormulaValue value);
}

/// <summary>
/// Пользовательская функция, доступная в формулах.
///
/// Новая функция добавляется регистрацией реализации в контейнере зависимостей и
/// сразу становится доступной во всех формулах приложения.
/// </summary>
public interface IFormulaFunction
{
    /// <summary>Имя функции, используемое в выражении.</summary>
    string Name { get; }

    /// <summary>Минимальное количество аргументов.</summary>
    int MinimumArgumentCount { get; }

    /// <summary>
    /// Максимальное количество аргументов.
    /// Значение <see langword="null"/> означает неограниченное количество.
    /// </summary>
    int? MaximumArgumentCount { get; }

    /// <summary>Описание функции для редактора формул.</summary>
    string Description { get; }

    /// <summary>
    /// Вычисляет значение функции.
    /// </summary>
    /// <param name="arguments">Вычисленные значения аргументов.</param>
    /// <returns>Результат работы функции.</returns>
    FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments);
}

/// <summary>
/// Функция формул, обращающаяся к источнику случайных значений.
///
/// Движок пересоздаёт такие функции, когда вычисляет выражение с заданным исходом
/// броска: без этого <c>Кубик(2; 6)</c> продолжал бы бросать кубики и при вычислении
/// границ выражения. Функция плагина получает то же поведение, реализовав интерфейс.
/// </summary>
public interface IRandomAwareFunction
{
    /// <summary>
    /// Создаёт копию функции, использующую другой источник случайных значений.
    /// </summary>
    /// <param name="random">Источник случайных значений.</param>
    /// <returns>Функция с заданным источником.</returns>
    IFormulaFunction WithRandom(IRandomSource random);
}

/// <summary>
/// Диапазон значений, которые способно вернуть выражение с бросками кубиков.
/// </summary>
/// <param name="Minimum">Наименьшее возможное значение.</param>
/// <param name="Maximum">Наибольшее возможное значение.</param>
public readonly record struct FormulaRange(double Minimum, double Maximum)
{
    /// <summary>Выражение не содержит случайностей и всегда даёт одно значение.</summary>
    public bool IsExact => Math.Abs(Maximum - Minimum) < double.Epsilon;
}

/// <summary>
/// Единый движок вычислений приложения.
///
/// STYLE_GUIDE запрещает выполнять игровые вычисления вне этого движка:
/// любая механика — характеристики, урон, лечение, ресурсы, требования —
/// описывается формулой и вычисляется здесь.
/// </summary>
public interface IFormulaEngine
{
    /// <summary>
    /// Вычисляет выражение.
    /// </summary>
    /// <param name="expression">Текст выражения.</param>
    /// <param name="context">Источник значений переменных.</param>
    /// <returns>Результат вычисления либо описание ошибки.</returns>
    Result<FormulaValue> Evaluate(string expression, IFormulaContext? context = null);

    /// <summary>
    /// Вычисляет выражение, выполняя все броски кубиков указанным источником
    /// случайных значений.
    ///
    /// Позволяет подсистеме бросков увидеть каждый выпавший кубик: движок возвращает
    /// только итог выражения, а источник значений знает, сколько граней было у кости
    /// и что на ней выпало. Тот же способ задаёт заранее известный исход в тестах.
    /// </summary>
    /// <param name="expression">Текст выражения.</param>
    /// <param name="context">Источник значений переменных.</param>
    /// <param name="random">Источник случайных значений.</param>
    /// <returns>Результат вычисления либо описание ошибки.</returns>
    Result<FormulaValue> Evaluate(string expression, IFormulaContext? context, IRandomSource random);

    /// <summary>
    /// Вычисляет наименьшее и наибольшее значения выражения, заменяя каждый бросок
    /// кубиков его худшим и лучшим исходом.
    ///
    /// Позволяет показать пользователю, что даёт формула урона или лечения, не выполняя
    /// броска: карточка оружия отображает «2d6 + 3 (5–15)» и сразу подтверждает, что
    /// масштабирование настроено верно.
    /// </summary>
    /// <param name="expression">Текст выражения.</param>
    /// <param name="context">Источник значений переменных.</param>
    /// <returns>Диапазон значений либо описание ошибки.</returns>
    Result<FormulaRange> EvaluateRange(string expression, IFormulaContext? context = null);

    /// <summary>
    /// Проверяет выражение на синтаксические ошибки и обращения к неизвестным функциям.
    /// Вычисление при этом не выполняется.
    /// </summary>
    /// <param name="expression">Текст выражения.</param>
    /// <returns>Результат проверки.</returns>
    Result Validate(string expression);

    /// <summary>
    /// Возвращает имена переменных, используемых выражением.
    /// Применяется для построения графа зависимостей и автоматического пересчёта.
    /// </summary>
    /// <param name="expression">Текст выражения.</param>
    /// <returns>Список имён переменных либо описание ошибки разбора.</returns>
    Result<IReadOnlyList<string>> GetReferencedVariables(string expression);

    /// <summary>Зарегистрированные функции, доступные в формулах.</summary>
    IReadOnlyCollection<IFormulaFunction> Functions { get; }
}

/// <summary>
/// Источник случайных значений для бросков кубиков.
/// Выделен в отдельную зависимость, чтобы вычисления можно было проверять тестами.
/// </summary>
public interface IRandomSource
{
    /// <summary>
    /// Возвращает случайное целое число в диапазоне включительно.
    /// </summary>
    /// <param name="minimumInclusive">Нижняя граница.</param>
    /// <param name="maximumInclusive">Верхняя граница.</param>
    /// <returns>Случайное число.</returns>
    int Next(int minimumInclusive, int maximumInclusive);
}
