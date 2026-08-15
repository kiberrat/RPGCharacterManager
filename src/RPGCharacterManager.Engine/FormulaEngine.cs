using System.Collections.Concurrent;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Engine.Parsing;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Engine;

/// <summary>
/// Единый движок вычислений приложения.
///
/// Разобранные выражения кэшируются: текст формулы разбирается один раз, а последующие
/// вычисления выполняются по готовому синтаксическому дереву. Это существенно ускоряет
/// автоматический пересчёт, при котором одни и те же формулы вычисляются многократно.
/// </summary>
public sealed class FormulaEngine : IFormulaEngine
{
    /// <summary>
    /// Предельное количество кэшируемых выражений.
    /// Ограничение исключает неограниченный рост памяти при массовом импорте контента.
    /// </summary>
    public const int MaximumCachedExpressions = 4096;

    private readonly ConcurrentDictionary<string, SyntaxNode> _cache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IFormulaFunction> _functions;
    private readonly EvaluationServices _services;
    private readonly EvaluationServices _minimumServices;
    private readonly EvaluationServices _maximumServices;

    /// <summary>
    /// Создаёт движок вычислений.
    /// </summary>
    /// <param name="functions">Доступные функции формул.</param>
    /// <param name="random">Источник случайных значений для бросков кубиков.</param>
    public FormulaEngine(IEnumerable<IFormulaFunction> functions, IRandomSource random)
    {
        Guard.NotNull(functions);
        Guard.NotNull(random);

        // При совпадении имён последняя зарегистрированная функция замещает предыдущую:
        // это позволяет плагину или игровой системе переопределить встроенную функцию.
        var map = new Dictionary<string, IFormulaFunction>(StringComparer.OrdinalIgnoreCase);

        foreach (var function in functions)
        {
            map[function.Name] = function;
        }

        _functions = map;
        _services = new EvaluationServices(map, random);
        _minimumServices = CreateServices(map, FixedRandomSource.Minimum);
        _maximumServices = CreateServices(map, FixedRandomSource.Maximum);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IFormulaFunction> Functions => _functions.Values;

    /// <inheritdoc />
    public Result<FormulaValue> Evaluate(string expression, IFormulaContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Result.Failure<FormulaValue>("Формула не задана");
        }

        try
        {
            var node = GetOrParse(expression);
            return Result.Success(node.Evaluate(context, _services));
        }
        catch (FormulaException exception)
        {
            return Result.Failure<FormulaValue>(exception.Message);
        }
        catch (Exception exception) when (exception is OverflowException or InvalidCastException)
        {
            return Result.Failure<FormulaValue>($"Ошибка вычисления формулы: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public Result<FormulaValue> Evaluate(string expression, IFormulaContext? context, IRandomSource random)
    {
        Guard.NotNull(random);

        if (string.IsNullOrWhiteSpace(expression))
        {
            return Result.Failure<FormulaValue>("Формула не задана");
        }

        try
        {
            var node = GetOrParse(expression);
            return Result.Success(node.Evaluate(context, CreateServices(_functions, random)));
        }
        catch (FormulaException exception)
        {
            return Result.Failure<FormulaValue>(exception.Message);
        }
        catch (Exception exception) when (exception is OverflowException or InvalidCastException)
        {
            return Result.Failure<FormulaValue>($"Ошибка вычисления формулы: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public Result<FormulaRange> EvaluateRange(string expression, IFormulaContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Result.Failure<FormulaRange>("Формула не задана");
        }

        try
        {
            var node = GetOrParse(expression);

            var minimum = node.Evaluate(context, _minimumServices).AsNumber();
            var maximum = node.Evaluate(context, _maximumServices).AsNumber();

            // Знак минус перед броском меняет порядок границ: «10 - 1d6» даёт
            // наибольшее значение при наименьшем броске.
            return Result.Success(minimum <= maximum
                ? new FormulaRange(minimum, maximum)
                : new FormulaRange(maximum, minimum));
        }
        catch (FormulaException exception)
        {
            return Result.Failure<FormulaRange>(exception.Message);
        }
        catch (Exception exception) when (exception is OverflowException or InvalidCastException)
        {
            return Result.Failure<FormulaRange>($"Ошибка вычисления формулы: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public Result Validate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Result.Failure("Формула не задана");
        }

        try
        {
            var node = GetOrParse(expression);
            ValidateFunctions(node);
            return Result.Success();
        }
        catch (FormulaException exception)
        {
            return Result.Failure(exception.Message);
        }
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<string>> GetReferencedVariables(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Result.Success<IReadOnlyList<string>>([]);
        }

        try
        {
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            GetOrParse(expression).CollectVariables(names);

            return Result.Success<IReadOnlyList<string>>(names.ToList());
        }
        catch (FormulaException exception)
        {
            return Result.Failure<IReadOnlyList<string>>(exception.Message);
        }
    }

    /// <summary>
    /// Создаёт набор служб вычисления с указанным источником случайных значений.
    /// Функции, обращающиеся к случайным значениям, пересоздаются с тем же источником,
    /// поэтому исход броска не зависит от формы его записи: и <c>2d6</c>,
    /// и <c>Кубик(2; 6)</c> обращаются к одному источнику.
    /// </summary>
    /// <param name="functions">Зарегистрированные функции.</param>
    /// <param name="random">Источник случайных значений.</param>
    /// <returns>Службы вычисления.</returns>
    private static EvaluationServices CreateServices(
        Dictionary<string, IFormulaFunction> functions,
        IRandomSource random)
    {
        var map = new Dictionary<string, IFormulaFunction>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in functions)
        {
            map[pair.Key] = pair.Value is IRandomAwareFunction aware
                ? aware.WithRandom(random)
                : pair.Value;
        }

        return new EvaluationServices(map, random);
    }

    private SyntaxNode GetOrParse(string expression)
    {
        if (_cache.TryGetValue(expression, out var cached))
        {
            return cached;
        }

        var node = new Parser(new Lexer(expression).Tokenize()).Parse();

        // Кэш очищается целиком при переполнении: выражения разбираются заново,
        // что дешевле поддержания порядка вытеснения при каждом обращении.
        if (_cache.Count >= MaximumCachedExpressions)
        {
            _cache.Clear();
        }

        _cache[expression] = node;
        return node;
    }

    /// <summary>
    /// Проверяет, что все вызываемые функции существуют и получают допустимое
    /// количество аргументов. Выполняется без вычисления выражения.
    /// </summary>
    /// <param name="node">Проверяемый узел дерева.</param>
    private void ValidateFunctions(SyntaxNode node)
    {
        if (node is FunctionNode function)
        {
            if (!_functions.TryGetValue(function.Name, out var declaration))
            {
                throw new FormulaException($"Неизвестная функция «{function.Name}»");
            }

            FunctionNode.ValidateArgumentCount(declaration, function.ArgumentCount);
        }

        foreach (var child in node.Children)
        {
            ValidateFunctions(child);
        }
    }
}
