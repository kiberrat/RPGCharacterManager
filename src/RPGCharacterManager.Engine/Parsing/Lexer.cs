using System.Globalization;
using System.Text;
using RPGCharacterManager.Core.Abstractions.Dice;

namespace RPGCharacterManager.Engine.Parsing;

/// <summary>
/// Разбор текста выражения на лексемы.
///
/// Лексер понимает русские имена переменных и функций, десятичный разделитель
/// в виде точки и запятой, символы умножения <c>*</c> и <c>×</c>, а также запись
/// броска кубиков вида <c>2d6</c> и <c>2к6</c>.
/// </summary>
internal sealed class Lexer
{
    private const char DecimalPoint = '.';
    private const char DecimalComma = ',';
    private const char TextQuote = '"';

    /// <summary>Символы умножения, принимаемые движком.</summary>
    private static readonly char[] MultiplicationSymbols = ['*', '×', '·'];

    /// <summary>Символы деления, принимаемые движком.</summary>
    private static readonly char[] DivisionSymbols = ['/', '÷', ':'];

    /// <summary>
    /// Латинская и русская буквы обозначения кубика.
    /// Перечень задан вместе с записью броска: читать и писать бросок
    /// приложение обязано одинаково.
    /// </summary>
    private static readonly char[] DiceLetters = [.. DiceNotation.Separators];

    private readonly string _expression;
    private int _position;

    /// <summary>
    /// Создаёт лексер для указанного выражения.
    /// </summary>
    /// <param name="expression">Текст выражения.</param>
    public Lexer(string expression) => _expression = expression ?? string.Empty;

    /// <summary>
    /// Разбирает выражение на последовательность лексем.
    /// </summary>
    /// <returns>Список лексем, завершающийся признаком конца выражения.</returns>
    /// <exception cref="FormulaException">Выражение содержит недопустимый символ.</exception>
    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        _position = 0;

        while (_position < _expression.Length)
        {
            var current = _expression[_position];

            if (char.IsWhiteSpace(current))
            {
                _position++;
                continue;
            }

            if (char.IsDigit(current))
            {
                tokens.Add(ReadNumberOrDice());
                continue;
            }

            if (IsIdentifierStart(current))
            {
                tokens.Add(ReadIdentifierOrDice());
                continue;
            }

            if (current == TextQuote)
            {
                tokens.Add(ReadText());
                continue;
            }

            tokens.Add(ReadSymbol());
        }

        tokens.Add(new Token(TokenType.EndOfExpression, string.Empty, _position));
        return tokens;
    }

    private static bool IsIdentifierStart(char value) => char.IsLetter(value) || value == '_';

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(value) || value == '_' || value == '.';

    private Token ReadNumberOrDice()
    {
        var start = _position;

        while (_position < _expression.Length && char.IsDigit(_expression[_position]))
        {
            _position++;
        }

        // Запись броска кубиков: за количеством следует буква кубика и число граней.
        if (_position < _expression.Length
            && Array.IndexOf(DiceLetters, _expression[_position]) >= 0
            && _position + 1 < _expression.Length
            && char.IsDigit(_expression[_position + 1]))
        {
            _position++;

            while (_position < _expression.Length && char.IsDigit(_expression[_position]))
            {
                _position++;
            }

            return new Token(TokenType.Dice, _expression[start.._position], start);
        }

        if (_position < _expression.Length && IsDecimalSeparator(_expression, _position))
        {
            _position++;

            while (_position < _expression.Length && char.IsDigit(_expression[_position]))
            {
                _position++;
            }
        }

        var text = _expression[start.._position].Replace(DecimalComma, DecimalPoint);
        return new Token(TokenType.Number, text, start);
    }

    /// <summary>
    /// Определяет, является ли символ десятичным разделителем.
    /// Запятая считается разделителем только если за ней следует цифра, иначе
    /// она разделяет аргументы функции.
    /// </summary>
    /// <param name="expression">Текст выражения.</param>
    /// <param name="position">Проверяемая позиция.</param>
    /// <returns><see langword="true"/>, если символ является десятичным разделителем.</returns>
    private static bool IsDecimalSeparator(string expression, int position)
    {
        var current = expression[position];

        if (current == DecimalPoint)
        {
            return true;
        }

        return current == DecimalComma
            && position + 1 < expression.Length
            && char.IsDigit(expression[position + 1]);
    }

    private Token ReadIdentifierOrDice()
    {
        var start = _position;

        while (_position < _expression.Length && IsIdentifierPart(_expression[_position]))
        {
            _position++;
        }

        var text = _expression[start.._position];

        // Запись без указания количества кубиков: d20 равнозначно 1d20.
        if (text.Length > 1
            && Array.IndexOf(DiceLetters, text[0]) >= 0
            && text[1..].All(char.IsDigit))
        {
            return new Token(TokenType.Dice, "1" + text, start);
        }

        return new Token(TokenType.Identifier, text, start);
    }

    private Token ReadText()
    {
        var start = _position;
        _position++;

        var builder = new StringBuilder();

        while (_position < _expression.Length && _expression[_position] != TextQuote)
        {
            builder.Append(_expression[_position]);
            _position++;
        }

        if (_position >= _expression.Length)
        {
            throw new FormulaException("Не закрыта строковая константа", start);
        }

        _position++;
        return new Token(TokenType.Text, builder.ToString(), start);
    }

    private Token ReadSymbol()
    {
        var start = _position;
        var current = _expression[_position];

        switch (current)
        {
            case '(':
                _position++;
                return new Token(TokenType.OpenParenthesis, "(", start);

            case ')':
                _position++;
                return new Token(TokenType.CloseParenthesis, ")", start);

            case ';':
                _position++;
                return new Token(TokenType.Comma, ",", start);

            case DecimalComma:
                _position++;
                return new Token(TokenType.Comma, ",", start);

            default:
                return ReadOperator(start, current);
        }
    }

    private Token ReadOperator(int start, char current)
    {
        // Двухсимвольные операторы сравнения разбираются раньше односимвольных.
        if (_position + 1 < _expression.Length)
        {
            var pair = _expression.Substring(_position, 2);

            if (pair is ">=" or "<=" or "!=" or "<>" or "==")
            {
                _position += 2;
                return new Token(TokenType.Operator, NormalizeOperator(pair), start);
            }
        }

        if (Array.IndexOf(MultiplicationSymbols, current) >= 0)
        {
            _position++;
            return new Token(TokenType.Operator, "*", start);
        }

        if (Array.IndexOf(DivisionSymbols, current) >= 0)
        {
            _position++;
            return new Token(TokenType.Operator, "/", start);
        }

        if (current is '+' or '-' or '%' or '^' or '>' or '<' or '=')
        {
            _position++;
            return new Token(TokenType.Operator, NormalizeOperator(current.ToString()), start);
        }

        throw new FormulaException(
            $"Недопустимый символ «{current.ToString(CultureInfo.CurrentCulture)}»",
            start);
    }

    private static string NormalizeOperator(string value) => value switch
    {
        "==" => "=",
        "<>" => "!=",
        _ => value,
    };
}
