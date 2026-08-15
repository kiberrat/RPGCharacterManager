namespace RPGCharacterManager.Engine.Parsing;

/// <summary>
/// Тип лексемы выражения.
/// </summary>
internal enum TokenType
{
    /// <summary>Числовой литерал.</summary>
    Number,

    /// <summary>Строковый литерал в кавычках.</summary>
    Text,

    /// <summary>Имя переменной или функции.</summary>
    Identifier,

    /// <summary>Бросок кубиков, например <c>2d6</c>.</summary>
    Dice,

    /// <summary>Оператор.</summary>
    Operator,

    /// <summary>Открывающая скобка.</summary>
    OpenParenthesis,

    /// <summary>Закрывающая скобка.</summary>
    CloseParenthesis,

    /// <summary>Разделитель аргументов функции.</summary>
    Comma,

    /// <summary>Конец выражения.</summary>
    EndOfExpression,
}

/// <summary>
/// Лексема выражения.
/// </summary>
/// <param name="Type">Тип лексемы.</param>
/// <param name="Text">Исходный текст лексемы.</param>
/// <param name="Position">Позиция первого символа лексемы в выражении.</param>
internal readonly record struct Token(TokenType Type, string Text, int Position);

/// <summary>
/// Ошибка разбора или вычисления выражения.
/// Используется внутри движка и преобразуется в результат операции на его границе.
/// </summary>
internal sealed class FormulaException : Exception
{
    /// <summary>
    /// Создаёт ошибку разбора выражения.
    /// </summary>
    /// <param name="message">Описание ошибки для пользователя.</param>
    /// <param name="position">Позиция в выражении, где обнаружена ошибка.</param>
    public FormulaException(string message, int position = -1)
        : base(position >= 0 ? $"{message} (позиция {position})" : message) => Position = position;

    /// <summary>Позиция ошибки в выражении или -1, если она неизвестна.</summary>
    public int Position { get; }
}
