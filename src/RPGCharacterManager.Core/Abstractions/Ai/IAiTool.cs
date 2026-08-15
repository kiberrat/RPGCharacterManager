namespace RPGCharacterManager.Core.Abstractions.Ai;

/// <summary>
/// Результат работы инструмента помощника.
/// </summary>
/// <param name="Text">Текст, возвращаемый модели. Должен быть кратким и понятным.</param>
/// <param name="Proposals">Предложения изменить данные, требующие подтверждения.</param>
public sealed record AiToolResult(string Text, IReadOnlyList<AiProposal> Proposals)
{
    /// <summary>
    /// Создаёт результат без предложений.
    /// </summary>
    /// <param name="text">Текст для модели.</param>
    /// <returns>Результат.</returns>
    public static AiToolResult Answer(string text) => new(text, []);

    /// <summary>
    /// Создаёт результат с предложением изменить данные.
    /// </summary>
    /// <param name="text">Текст для модели.</param>
    /// <param name="proposal">Предложение.</param>
    /// <returns>Результат.</returns>
    public static AiToolResult Propose(string text, AiProposal proposal) => new(text, [proposal]);
}

/// <summary>
/// Действие, доступное языковой модели.
///
/// Инструменты — единственный способ помощника узнать что-либо о приложении и
/// что-либо в нём изменить. Ни один инструмент не знает конкретной игровой системы:
/// вид объекта передаётся параметром, а его поля берутся из описания вида контента.
/// Поэтому помощник одинаково работает и с заклинанием, и с кибер-имплантом.
/// </summary>
public interface IAiTool
{
    /// <summary>Имя инструмента, которым модель его вызывает.</summary>
    string Name { get; }

    /// <summary>Название действия для пользователя: «Поиск объектов».</summary>
    string Title { get; }

    /// <summary>Пояснение для модели: что инструмент делает и когда его вызывать.</summary>
    string Description { get; }

    /// <summary>Параметры инструмента.</summary>
    IReadOnlyList<AiToolParameter> Parameters { get; }

    /// <summary>
    /// Инструмент предлагает изменение данных.
    /// Такие инструменты ничего не записывают: они возвращают предложение.
    /// </summary>
    bool ChangesData => false;

    /// <summary>
    /// Выполняет действие.
    /// </summary>
    /// <param name="arguments">Аргументы вызова.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат работы инструмента.</returns>
    Task<AiToolResult> InvokeAsync(AiToolArguments arguments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает описание инструмента для передачи модели.
    /// </summary>
    /// <returns>Описание инструмента.</returns>
    AiToolSpec Describe() => new(Name, Description, Parameters);
}
