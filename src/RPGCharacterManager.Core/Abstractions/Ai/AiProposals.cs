namespace RPGCharacterManager.Core.Abstractions.Ai;

/// <summary>
/// Что предлагает сделать помощник с игровым объектом.
///
/// Удаления в перечне нет намеренно: документ 024_AI_Помощник.md прямо запрещает
/// помощнику удалять сведения, поэтому такого предложения он создать не может —
/// не потому, что оно запрещено проверкой, а потому, что его нечем выразить.
/// </summary>
public enum AiProposalKind
{
    /// <summary>Создать новый объект.</summary>
    Create = 0,

    /// <summary>Изменить существующий объект.</summary>
    Update = 1,
}

/// <summary>
/// Состояние предложения помощника.
/// </summary>
public enum AiProposalState
{
    /// <summary>Ожидает решения пользователя.</summary>
    Pending = 0,

    /// <summary>Применено к базе данных.</summary>
    Applied = 1,

    /// <summary>Отклонено пользователем.</summary>
    Rejected = 2,

    /// <summary>Применить не удалось.</summary>
    Failed = 3,
}

/// <summary>
/// Изменение одного поля в предложении помощника.
/// </summary>
/// <param name="Field">Отображаемое название поля.</param>
/// <param name="OldValue">Прежнее значение; <see langword="null"/> для нового объекта.</param>
/// <param name="NewValue">Предлагаемое значение.</param>
public sealed record AiProposalChange(string Field, string? OldValue, string? NewValue);

/// <summary>
/// Предложение помощника изменить игровые данные.
///
/// Документ 024_AI_Помощник.md запрещает помощнику менять данные самостоятельно:
/// он только предлагает, а применяет изменение пользователь. Поэтому инструменты
/// записи не обращаются к базе, а возвращают предложение — оно попадает в список
/// действий и ждёт подтверждения.
/// </summary>
public sealed class AiProposal
{
    /// <summary>
    /// Создаёт предложение помощника.
    /// </summary>
    /// <param name="kind">Что предлагается сделать.</param>
    /// <param name="typeId">Идентификатор вида контента.</param>
    /// <param name="typeName">Название одного объекта вида: «Заклинание».</param>
    /// <param name="title">Название объекта.</param>
    /// <param name="changes">Изменения полей.</param>
    /// <param name="values">Значения полей, которые нужно записать.</param>
    /// <param name="targetId">Идентификатор изменяемого объекта.</param>
    /// <param name="listName">Внутреннее имя вложенного списка, если добавляется запись списка.</param>
    public AiProposal(
        AiProposalKind kind,
        string typeId,
        string typeName,
        string title,
        IReadOnlyList<AiProposalChange> changes,
        IReadOnlyDictionary<string, string?> values,
        Guid? targetId = null,
        string? listName = null)
    {
        Kind = kind;
        TypeId = typeId;
        TypeName = typeName;
        Title = title;
        Changes = changes;
        Values = values;
        TargetId = targetId;
        ListName = listName;
    }

    /// <summary>Идентификатор предложения в пределах беседы.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>Что предлагается сделать.</summary>
    public AiProposalKind Kind { get; }

    /// <summary>Идентификатор вида контента.</summary>
    public string TypeId { get; }

    /// <summary>Название одного объекта вида.</summary>
    public string TypeName { get; }

    /// <summary>
    /// Идентификатор объекта, к которому относится предложение.
    ///
    /// Для <see cref="AiProposalKind.Update"/> — изменяемый объект.
    /// Для <see cref="AiProposalKind.Create"/> — объект-образец: заданный
    /// идентификатор означает, что создаётся его копия с изменёнными полями.
    /// </summary>
    public Guid? TargetId { get; }

    /// <summary>Название объекта.</summary>
    public string Title { get; }

    /// <summary>
    /// Внутреннее имя вложенного списка, в который добавляется запись.
    /// Задано только для предложений, добавляющих запись списка.
    /// </summary>
    public string? ListName { get; }

    /// <summary>Изменения полей в порядке отображения.</summary>
    public IReadOnlyList<AiProposalChange> Changes { get; }

    /// <summary>Значения полей, которые нужно записать при применении.</summary>
    public IReadOnlyDictionary<string, string?> Values { get; }

    /// <summary>Состояние предложения. Изменяется помощником при применении и отклонении.</summary>
    public AiProposalState State { get; set; } = AiProposalState.Pending;

    /// <summary>Причина, по которой применить предложение не удалось.</summary>
    public string? Error { get; set; }

    /// <summary>Краткое описание действия: «Создать: Заклинание «Огненный шар»».</summary>
    public string Summary => (Kind, ListName, TargetId) switch
    {
        (_, { Length: > 0 }, _) => $"Дополнить: {TypeName} «{Title}»",
        (AiProposalKind.Create, _, not null) => $"Создать копию: {TypeName} «{Title}»",
        (AiProposalKind.Create, _, _) => $"Создать: {TypeName} «{Title}»",
        _ => $"Изменить: {TypeName} «{Title}»",
    };
}
