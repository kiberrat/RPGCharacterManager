using RPGCharacterManager.Core.Abstractions.Engine;

namespace RPGCharacterManager.Engine.Parsing;

/// <summary>
/// Узел синтаксического дерева выражения.
///
/// Дерево строится один раз и кэшируется, поэтому повторное вычисление одной и той же
/// формулы не требует повторного разбора текста.
/// </summary>
internal abstract class SyntaxNode
{
    /// <summary>
    /// Дочерние узлы. Позволяет обходить дерево, не зная конкретных типов узлов.
    /// </summary>
    public virtual IReadOnlyList<SyntaxNode> Children => [];

    /// <summary>
    /// Вычисляет значение узла.
    /// </summary>
    /// <param name="context">Источник значений переменных.</param>
    /// <param name="services">Службы движка: функции и генератор случайных чисел.</param>
    /// <returns>Вычисленное значение.</returns>
    public abstract FormulaValue Evaluate(IFormulaContext? context, EvaluationServices services);

    /// <summary>
    /// Собирает имена переменных, используемых поддеревом.
    /// </summary>
    /// <param name="names">Множество, в которое добавляются имена.</param>
    public abstract void CollectVariables(ISet<string> names);
}

/// <summary>
/// Службы, необходимые для вычисления выражения.
/// </summary>
/// <param name="Functions">Доступные функции, сопоставленные по имени без учёта регистра.</param>
/// <param name="Random">Источник случайных значений для бросков кубиков.</param>
internal sealed record EvaluationServices(
    IReadOnlyDictionary<string, IFormulaFunction> Functions,
    IRandomSource Random);

/// <summary>Константа.</summary>
internal sealed class LiteralNode : SyntaxNode
{
    private readonly FormulaValue _value;

    /// <summary>
    /// Создаёт узел константы.
    /// </summary>
    /// <param name="value">Значение константы.</param>
    public LiteralNode(FormulaValue value) => _value = value;

    /// <inheritdoc />
    public override FormulaValue Evaluate(IFormulaContext? context, EvaluationServices services) => _value;

    /// <inheritdoc />
    public override void CollectVariables(ISet<string> names)
    {
    }
}

/// <summary>Обращение к переменной.</summary>
internal sealed class VariableNode : SyntaxNode
{
    private readonly string _name;

    /// <summary>
    /// Создаёт узел переменной.
    /// </summary>
    /// <param name="name">Имя переменной.</param>
    public VariableNode(string name) => _name = name;

    /// <inheritdoc />
    public override FormulaValue Evaluate(IFormulaContext? context, EvaluationServices services)
    {
        if (context is not null && context.TryGetVariable(_name, out var value))
        {
            return value;
        }

        throw new FormulaException($"Неизвестная переменная «{_name}»");
    }

    /// <inheritdoc />
    public override void CollectVariables(ISet<string> names) => names.Add(_name);
}

/// <summary>Бросок кубиков вида <c>2d6</c>.</summary>
internal sealed class DiceNode : SyntaxNode
{
    /// <summary>Максимальное количество кубиков в одном броске.</summary>
    public const int MaximumDiceCount = 1000;

    /// <summary>Максимальное количество граней кубика.</summary>
    public const int MaximumSideCount = 1_000_000;

    private readonly int _count;
    private readonly int _sides;

    /// <summary>
    /// Создаёт узел броска кубиков.
    /// </summary>
    /// <param name="count">Количество кубиков.</param>
    /// <param name="sides">Количество граней.</param>
    public DiceNode(int count, int sides)
    {
        if (count is < 1 or > MaximumDiceCount)
        {
            throw new FormulaException(
                $"Количество кубиков должно находиться в диапазоне от 1 до {MaximumDiceCount}");
        }

        if (sides is < 2 or > MaximumSideCount)
        {
            throw new FormulaException(
                $"Количество граней должно находиться в диапазоне от 2 до {MaximumSideCount}");
        }

        _count = count;
        _sides = sides;
    }

    /// <inheritdoc />
    public override FormulaValue Evaluate(IFormulaContext? context, EvaluationServices services)
    {
        var total = 0;

        for (var index = 0; index < _count; index++)
        {
            total += services.Random.Next(1, _sides);
        }

        return FormulaValue.FromNumber(total);
    }

    /// <inheritdoc />
    public override void CollectVariables(ISet<string> names)
    {
    }
}

/// <summary>Унарная операция.</summary>
internal sealed class UnaryNode : SyntaxNode
{
    private readonly string _operator;
    private readonly SyntaxNode _operand;

    /// <summary>
    /// Создаёт узел унарной операции.
    /// </summary>
    /// <param name="operatorText">Знак операции.</param>
    /// <param name="operand">Операнд.</param>
    public UnaryNode(string operatorText, SyntaxNode operand)
    {
        _operator = operatorText;
        _operand = operand;
    }

    /// <inheritdoc />
    public override IReadOnlyList<SyntaxNode> Children => [_operand];

    /// <inheritdoc />
    public override FormulaValue Evaluate(IFormulaContext? context, EvaluationServices services)
    {
        var value = _operand.Evaluate(context, services);

        return _operator switch
        {
            "-" => FormulaValue.FromNumber(-value.AsNumber()),
            "+" => FormulaValue.FromNumber(value.AsNumber()),
            "не" => FormulaValue.FromBoolean(!value.AsBoolean()),
            _ => throw new FormulaException($"Неизвестная унарная операция «{_operator}»"),
        };
    }

    /// <inheritdoc />
    public override void CollectVariables(ISet<string> names) => _operand.CollectVariables(names);
}

/// <summary>Бинарная операция.</summary>
internal sealed class BinaryNode : SyntaxNode
{
    private readonly string _operator;
    private readonly SyntaxNode _left;
    private readonly SyntaxNode _right;

    /// <summary>
    /// Создаёт узел бинарной операции.
    /// </summary>
    /// <param name="operatorText">Знак операции.</param>
    /// <param name="left">Левый операнд.</param>
    /// <param name="right">Правый операнд.</param>
    public BinaryNode(string operatorText, SyntaxNode left, SyntaxNode right)
    {
        _operator = operatorText;
        _left = left;
        _right = right;
    }

    /// <inheritdoc />
    public override IReadOnlyList<SyntaxNode> Children => [_left, _right];

    /// <inheritdoc />
    public override FormulaValue Evaluate(IFormulaContext? context, EvaluationServices services)
    {
        // Логические операции вычисляют правый операнд только при необходимости.
        switch (_operator)
        {
            case "и":
                return FormulaValue.FromBoolean(
                    _left.Evaluate(context, services).AsBoolean()
                    && _right.Evaluate(context, services).AsBoolean());

            case "или":
                return FormulaValue.FromBoolean(
                    _left.Evaluate(context, services).AsBoolean()
                    || _right.Evaluate(context, services).AsBoolean());

            default:
                break;
        }

        var left = _left.Evaluate(context, services);
        var right = _right.Evaluate(context, services);

        return _operator switch
        {
            "+" => Add(left, right),
            "-" => FormulaValue.FromNumber(left.AsNumber() - right.AsNumber()),
            "*" => FormulaValue.FromNumber(left.AsNumber() * right.AsNumber()),
            "/" => Divide(left, right),
            "%" => Modulo(left, right),
            "^" => FormulaValue.FromNumber(Math.Pow(left.AsNumber(), right.AsNumber())),
            "=" => FormulaValue.FromBoolean(left == right),
            "!=" => FormulaValue.FromBoolean(left != right),
            ">" => FormulaValue.FromBoolean(left.AsNumber() > right.AsNumber()),
            "<" => FormulaValue.FromBoolean(left.AsNumber() < right.AsNumber()),
            ">=" => FormulaValue.FromBoolean(left.AsNumber() >= right.AsNumber()),
            "<=" => FormulaValue.FromBoolean(left.AsNumber() <= right.AsNumber()),
            _ => throw new FormulaException($"Неизвестная операция «{_operator}»"),
        };
    }

    /// <inheritdoc />
    public override void CollectVariables(ISet<string> names)
    {
        _left.CollectVariables(names);
        _right.CollectVariables(names);
    }

    /// <summary>
    /// Складывает значения. Если хотя бы один операнд является строкой,
    /// выполняется объединение строк — это позволяет формулам собирать описания.
    /// </summary>
    /// <param name="left">Левый операнд.</param>
    /// <param name="right">Правый операнд.</param>
    /// <returns>Результат сложения.</returns>
    private static FormulaValue Add(FormulaValue left, FormulaValue right) =>
        left.Kind == FormulaValueKind.Text || right.Kind == FormulaValueKind.Text
            ? FormulaValue.FromText(left.AsText() + right.AsText())
            : FormulaValue.FromNumber(left.AsNumber() + right.AsNumber());

    private static FormulaValue Divide(FormulaValue left, FormulaValue right)
    {
        var divisor = right.AsNumber();

        if (Math.Abs(divisor) < double.Epsilon)
        {
            throw new FormulaException("Деление на ноль");
        }

        return FormulaValue.FromNumber(left.AsNumber() / divisor);
    }

    private static FormulaValue Modulo(FormulaValue left, FormulaValue right)
    {
        var divisor = right.AsNumber();

        if (Math.Abs(divisor) < double.Epsilon)
        {
            throw new FormulaException("Деление на ноль при вычислении остатка");
        }

        return FormulaValue.FromNumber(left.AsNumber() % divisor);
    }
}

/// <summary>Вызов функции.</summary>
internal sealed class FunctionNode : SyntaxNode
{
    private readonly string _name;
    private readonly IReadOnlyList<SyntaxNode> _arguments;

    /// <summary>
    /// Создаёт узел вызова функции.
    /// </summary>
    /// <param name="name">Имя функции.</param>
    /// <param name="arguments">Аргументы вызова.</param>
    public FunctionNode(string name, IReadOnlyList<SyntaxNode> arguments)
    {
        _name = name;
        _arguments = arguments;
    }

    /// <summary>Имя вызываемой функции.</summary>
    public string Name => _name;

    /// <summary>Количество переданных аргументов.</summary>
    public int ArgumentCount => _arguments.Count;

    /// <inheritdoc />
    public override IReadOnlyList<SyntaxNode> Children => _arguments;

    /// <inheritdoc />
    public override FormulaValue Evaluate(IFormulaContext? context, EvaluationServices services)
    {
        if (!services.Functions.TryGetValue(_name, out var function))
        {
            throw new FormulaException($"Неизвестная функция «{_name}»");
        }

        // Функция «Если» вычисляет только выбранную ветвь, поэтому обрабатывается отдельно.
        if (function is IConditionalFunction)
        {
            return EvaluateConditional(context, services);
        }

        var values = new FormulaValue[_arguments.Count];

        for (var index = 0; index < _arguments.Count; index++)
        {
            values[index] = _arguments[index].Evaluate(context, services);
        }

        ValidateArgumentCount(function, values.Length);
        return function.Invoke(values);
    }

    /// <inheritdoc />
    public override void CollectVariables(ISet<string> names)
    {
        foreach (var argument in _arguments)
        {
            argument.CollectVariables(names);
        }
    }

    /// <summary>
    /// Проверяет соответствие количества аргументов объявлению функции.
    /// </summary>
    /// <param name="function">Вызываемая функция.</param>
    /// <param name="count">Фактическое количество аргументов.</param>
    public static void ValidateArgumentCount(IFormulaFunction function, int count)
    {
        if (count < function.MinimumArgumentCount)
        {
            throw new FormulaException(
                $"Функция «{function.Name}» требует не менее {function.MinimumArgumentCount} аргументов");
        }

        if (function.MaximumArgumentCount is { } maximum && count > maximum)
        {
            throw new FormulaException(
                $"Функция «{function.Name}» принимает не более {maximum} аргументов");
        }
    }

    private FormulaValue EvaluateConditional(IFormulaContext? context, EvaluationServices services)
    {
        const int ConditionIndex = 0;
        const int TrueBranchIndex = 1;
        const int FalseBranchIndex = 2;
        const int MinimumArguments = 2;

        if (_arguments.Count is < MinimumArguments or > 3)
        {
            throw new FormulaException("Функция «Если» принимает два или три аргумента");
        }

        var condition = _arguments[ConditionIndex].Evaluate(context, services).AsBoolean();

        if (condition)
        {
            return _arguments[TrueBranchIndex].Evaluate(context, services);
        }

        return _arguments.Count > FalseBranchIndex
            ? _arguments[FalseBranchIndex].Evaluate(context, services)
            : FormulaValue.FromNumber(0);
    }
}

/// <summary>
/// Признак функции, аргументы которой вычисляются не все сразу.
/// </summary>
internal interface IConditionalFunction;
