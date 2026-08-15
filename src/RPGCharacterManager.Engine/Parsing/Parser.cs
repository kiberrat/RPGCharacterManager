using System.Globalization;
using RPGCharacterManager.Core.Abstractions.Engine;

namespace RPGCharacterManager.Engine.Parsing;

/// <summary>
/// Построение синтаксического дерева выражения методом рекурсивного спуска.
///
/// Приоритет операций, от низшего к высшему:
/// <c>или</c> → <c>и</c> → сравнение → сложение → умножение → степень → унарные → первичные.
/// </summary>
internal sealed class Parser
{
    /// <summary>Логические ключевые слова, записываемые словами русского языка.</summary>
    private static readonly HashSet<string> LogicalKeywords =
        new(StringComparer.OrdinalIgnoreCase) { "и", "или", "не" };

    private readonly List<Token> _tokens;
    private int _index;

    /// <summary>
    /// Создаёт синтаксический анализатор.
    /// </summary>
    /// <param name="tokens">Лексемы выражения.</param>
    public Parser(List<Token> tokens) => _tokens = tokens;

    /// <summary>
    /// Разбирает выражение целиком.
    /// </summary>
    /// <returns>Корневой узел синтаксического дерева.</returns>
    /// <exception cref="FormulaException">Выражение содержит синтаксическую ошибку.</exception>
    public SyntaxNode Parse()
    {
        if (_tokens.Count == 1 && _tokens[0].Type == TokenType.EndOfExpression)
        {
            throw new FormulaException("Выражение пустое");
        }

        var node = ParseOr();

        if (Current.Type != TokenType.EndOfExpression)
        {
            throw new FormulaException($"Лишний текст «{Current.Text}»", Current.Position);
        }

        return node;
    }

    private Token Current => _tokens[_index];

    private Token Advance() => _tokens[_index++];

    private bool MatchOperator(params string[] operators)
    {
        if (Current.Type != TokenType.Operator)
        {
            return false;
        }

        return Array.Exists(operators, item => string.Equals(item, Current.Text, StringComparison.Ordinal));
    }

    private bool MatchKeyword(string keyword) =>
        Current.Type == TokenType.Identifier
        && string.Equals(Current.Text, keyword, StringComparison.OrdinalIgnoreCase);

    private SyntaxNode ParseOr()
    {
        var left = ParseAnd();

        while (MatchKeyword("или"))
        {
            Advance();
            left = new BinaryNode("или", left, ParseAnd());
        }

        return left;
    }

    private SyntaxNode ParseAnd()
    {
        var left = ParseComparison();

        while (MatchKeyword("и"))
        {
            Advance();
            left = new BinaryNode("и", left, ParseComparison());
        }

        return left;
    }

    private SyntaxNode ParseComparison()
    {
        var left = ParseAdditive();

        while (MatchOperator("=", "!=", ">", "<", ">=", "<="))
        {
            var operatorText = Advance().Text;
            left = new BinaryNode(operatorText, left, ParseAdditive());
        }

        return left;
    }

    private SyntaxNode ParseAdditive()
    {
        var left = ParseMultiplicative();

        while (MatchOperator("+", "-"))
        {
            var operatorText = Advance().Text;
            left = new BinaryNode(operatorText, left, ParseMultiplicative());
        }

        return left;
    }

    private SyntaxNode ParseMultiplicative()
    {
        var left = ParsePower();

        while (MatchOperator("*", "/", "%"))
        {
            var operatorText = Advance().Text;
            left = new BinaryNode(operatorText, left, ParsePower());
        }

        return left;
    }

    private SyntaxNode ParsePower()
    {
        var left = ParseUnary();

        // Возведение в степень правоассоциативно: 2^3^2 равно 2^(3^2).
        if (MatchOperator("^"))
        {
            Advance();
            return new BinaryNode("^", left, ParsePower());
        }

        return left;
    }

    private SyntaxNode ParseUnary()
    {
        if (MatchOperator("-", "+"))
        {
            var operatorText = Advance().Text;
            return new UnaryNode(operatorText, ParseUnary());
        }

        if (MatchKeyword("не"))
        {
            Advance();
            return new UnaryNode("не", ParseUnary());
        }

        return ParsePrimary();
    }

    private SyntaxNode ParsePrimary()
    {
        var token = Current;

        switch (token.Type)
        {
            case TokenType.Number:
                Advance();
                return new LiteralNode(FormulaValue.FromNumber(ParseNumber(token)));

            case TokenType.Text:
                Advance();
                return new LiteralNode(FormulaValue.FromText(token.Text));

            case TokenType.Dice:
                Advance();
                return CreateDiceNode(token);

            case TokenType.OpenParenthesis:
                return ParseParenthesized();

            case TokenType.Identifier:
                return ParseIdentifier();

            default:
                throw new FormulaException(
                    token.Type == TokenType.EndOfExpression
                        ? "Выражение оборвано"
                        : $"Неожиданный элемент «{token.Text}»",
                    token.Position);
        }
    }

    private SyntaxNode ParseParenthesized()
    {
        Advance();
        var inner = ParseOr();

        if (Current.Type != TokenType.CloseParenthesis)
        {
            throw new FormulaException("Не закрыта скобка", Current.Position);
        }

        Advance();
        return inner;
    }

    private SyntaxNode ParseIdentifier()
    {
        var token = Advance();

        if (LogicalKeywords.Contains(token.Text))
        {
            throw new FormulaException($"Неожиданное ключевое слово «{token.Text}»", token.Position);
        }

        if (Current.Type != TokenType.OpenParenthesis)
        {
            return new VariableNode(token.Text);
        }

        Advance();
        var arguments = new List<SyntaxNode>();

        if (Current.Type != TokenType.CloseParenthesis)
        {
            arguments.Add(ParseOr());

            while (Current.Type == TokenType.Comma)
            {
                Advance();
                arguments.Add(ParseOr());
            }
        }

        if (Current.Type != TokenType.CloseParenthesis)
        {
            throw new FormulaException(
                $"Не закрыт список аргументов функции «{token.Text}»",
                Current.Position);
        }

        Advance();
        return new FunctionNode(token.Text, arguments);
    }

    private static double ParseNumber(Token token) =>
        double.TryParse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormulaException($"Некорректное число «{token.Text}»", token.Position);

    private static DiceNode CreateDiceNode(Token token)
    {
        // Лексер приводит запись броска к виду «<количество>d<грани>» с латинской буквой
        // либо оставляет русскую; разделителем считается первая нецифровая позиция.
        var separatorIndex = 0;

        while (separatorIndex < token.Text.Length && char.IsDigit(token.Text[separatorIndex]))
        {
            separatorIndex++;
        }

        var countText = token.Text[..separatorIndex];
        var sidesText = token.Text[(separatorIndex + 1)..];

        if (!int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            || !int.TryParse(sidesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sides))
        {
            throw new FormulaException($"Некорректная запись броска «{token.Text}»", token.Position);
        }

        return new DiceNode(count, sides);
    }
}
