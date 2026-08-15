using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Ai.Tools;

/// <summary>
/// Итог заполнения полей объекта значениями, предложенными помощником.
/// </summary>
/// <param name="Changes">Изменения полей в порядке следования описания вида.</param>
/// <param name="Problems">Значения, которые записать не удалось.</param>
internal sealed record AiWriteOutcome(
    IReadOnlyList<AiProposalChange> Changes,
    IReadOnlyList<string> Problems);

/// <summary>
/// Запись значений полей игрового объекта.
///
/// Один и тот же код применяется дважды: сначала — чтобы показать пользователю,
/// что именно изменится, и затем — чтобы применить подтверждённое предложение.
/// Поэтому предпросмотр и результат не могут разойтись.
/// </summary>
internal sealed class AiContentWriter
{
    private readonly IContentService _content;

    /// <summary>
    /// Создаёт запись значений полей.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    public AiContentWriter(IContentService content) => _content = Guard.NotNull(content);

    /// <summary>
    /// Заполняет поля объекта значениями.
    /// </summary>
    /// <param name="fields">Описания полей объекта или вложенной записи.</param>
    /// <param name="entity">Объект или вложенная запись.</param>
    /// <param name="values">Значения, названные по внутреннему имени либо по названию поля.</param>
    /// <param name="trackOldValues">Запоминать прежние значения полей.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Изменения и замечания.</returns>
    public async Task<AiWriteOutcome> FillAsync(
        IReadOnlyList<IContentField> fields,
        object entity,
        IReadOnlyDictionary<string, string?> values,
        bool trackOldValues,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(fields);
        Guard.NotNull(entity);
        Guard.NotNull(values);

        var changes = new List<AiProposalChange>();
        var problems = new List<string>();
        var references = new Dictionary<string, IReadOnlyList<ContentReference>>(StringComparer.Ordinal);

        foreach (var pair in values)
        {
            var field = AiContentText.ResolveField(fields, pair.Key);

            if (field is null)
            {
                problems.Add($"поля «{pair.Key}» у этого вида объектов нет");
                continue;
            }

            var before = trackOldValues
                ? await DescribeAsync(field, entity, references, cancellationToken).ConfigureAwait(false)
                : null;

            var problem = await ApplyAsync(field, entity, pair.Value, references, cancellationToken)
                .ConfigureAwait(false);

            if (problem is not null)
            {
                problems.Add(problem);
                continue;
            }

            var after = await DescribeAsync(field, entity, references, cancellationToken)
                .ConfigureAwait(false);

            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                changes.Add(new AiProposalChange(field.DisplayName, before, after));
            }
        }

        return new AiWriteOutcome(changes, problems);
    }

    /// <summary>
    /// Записывает одно значение в поле объекта.
    /// </summary>
    /// <param name="field">Описание поля.</param>
    /// <param name="entity">Объект.</param>
    /// <param name="value">Значение.</param>
    /// <param name="references">Перечни объектов для полей-ссылок.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Описание проблемы либо <see langword="null"/>.</returns>
    private async Task<string?> ApplyAsync(
        IContentField field,
        object entity,
        string? value,
        Dictionary<string, IReadOnlyList<ContentReference>> references,
        CancellationToken cancellationToken)
    {
        var text = value?.Trim();

        switch (field.Kind)
        {
            case ContentFieldKind.Boolean:
                field.SetBoolean(entity, IsAffirmative(text));
                return null;

            case ContentFieldKind.Reference:
                return await ApplyReferenceAsync(field, entity, text, references, cancellationToken)
                    .ConfigureAwait(false);

            case ContentFieldKind.Enumeration when text is { Length: > 0 }:
                var option = field.Options.FirstOrDefault(item =>
                    item.Equals(text, StringComparison.OrdinalIgnoreCase));

                if (option is null)
                {
                    return $"поле «{field.DisplayName}» принимает только: " +
                        string.Join(AiContentText.Separator, field.Options);
                }

                field.TrySetText(entity, option, out _);
                return null;

            default:
                return field.TrySetText(entity, text, out var error) ? null : error;
        }
    }

    /// <summary>
    /// Записывает значение поля-ссылки, распознавая как идентификатор, так и название.
    /// </summary>
    /// <param name="field">Описание поля.</param>
    /// <param name="entity">Объект.</param>
    /// <param name="text">Значение.</param>
    /// <param name="references">Перечни объектов для полей-ссылок.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Описание проблемы либо <see langword="null"/>.</returns>
    private async Task<string?> ApplyReferenceAsync(
        IContentField field,
        object entity,
        string? text,
        Dictionary<string, IReadOnlyList<ContentReference>> references,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            field.SetReference(entity, null);
            return null;
        }

        var known = await GetReferencesAsync(field.ReferenceTypeId, references, cancellationToken)
            .ConfigureAwait(false);

        if (Guid.TryParse(text, out var identifier))
        {
            // Пустой перечень означает, что объектов этого вида нет вовсе,
            // а значит, и указанного среди них нет. Проверка пропускается
            // только для незарегистрированного вида.
            if (known is not null && known.All(item => item.Id != identifier))
            {
                return $"поле «{field.DisplayName}»: объекта с идентификатором {identifier} не существует";
            }

            field.SetReference(entity, identifier);
            return null;
        }

        // Модель называет связанный объект так, как он подписан в приложении:
        // искать его по названию удобнее, чем требовать идентификатор.
        var found = known?.FirstOrDefault(item =>
            item.Name.Equals(text, StringComparison.OrdinalIgnoreCase));

        if (found is null)
        {
            return $"поле «{field.DisplayName}»: объект «{text}» не найден " +
                $"среди объектов вида «{field.ReferenceTypeId}»";
        }

        field.SetReference(entity, found.Id);
        return null;
    }

    /// <summary>
    /// Возвращает значение поля в виде, пригодном для показа пользователю.
    /// Ссылка показывается названием связанного объекта, а не идентификатором.
    /// </summary>
    /// <param name="field">Описание поля.</param>
    /// <param name="entity">Объект.</param>
    /// <param name="references">Перечни объектов для полей-ссылок.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Текстовое значение либо <see langword="null"/>, если поле пусто.</returns>
    private async Task<string?> DescribeAsync(
        IContentField field,
        object entity,
        Dictionary<string, IReadOnlyList<ContentReference>> references,
        CancellationToken cancellationToken)
    {
        if (field.Kind != ContentFieldKind.Reference)
        {
            var text = AiContentText.ReadField(field, entity);

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        if (field.GetReference(entity) is not { } identifier)
        {
            return null;
        }

        var known = await GetReferencesAsync(field.ReferenceTypeId, references, cancellationToken)
            .ConfigureAwait(false);

        return known?.FirstOrDefault(item => item.Id == identifier)?.Name ?? identifier.ToString();
    }

    /// <summary>
    /// Возвращает перечень объектов вида, на который ссылается поле.
    /// </summary>
    /// <param name="typeId">Идентификатор вида контента.</param>
    /// <param name="cache">Уже прочитанные перечни.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Перечень либо <see langword="null"/>, если вид не зарегистрирован.</returns>
    private async Task<IReadOnlyList<ContentReference>?> GetReferencesAsync(
        string? typeId,
        Dictionary<string, IReadOnlyList<ContentReference>> cache,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(typeId) || _content.FindType(typeId) is null)
        {
            return null;
        }

        if (cache.TryGetValue(typeId, out var found))
        {
            return found;
        }

        var references = await _content.GetReferencesAsync(typeId, cancellationToken).ConfigureAwait(false);

        cache[typeId] = references;

        return references;
    }

    /// <summary>
    /// Определяет, означает ли текст согласие.
    /// Модель отвечает по-разному, поэтому распознаются оба языка и цифра.
    /// </summary>
    /// <param name="text">Значение.</param>
    /// <returns><see langword="true"/>, если значение означает «да».</returns>
    private static bool IsAffirmative(string? text)
    {
        string[] affirmative = ["да", "true", "1", "yes", "истина"];

        return text is { Length: > 0 }
            && affirmative.Any(item => item.Equals(text, StringComparison.OrdinalIgnoreCase));
    }
}
