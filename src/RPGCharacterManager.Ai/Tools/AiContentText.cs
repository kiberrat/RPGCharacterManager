using System.Text;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Ai.Tools;

/// <summary>
/// Перевод описаний контента в текст, понятный языковой модели.
///
/// Модель ничего не знает об игровых системах приложения: всё, что ей известно,
/// она получает отсюда. Поэтому текст строится по описанию вида контента, а не по
/// заранее заготовленным спискам, и новый вид объектов становится доступен
/// помощнику сразу после регистрации — без изменения этого файла.
/// </summary>
internal static class AiContentText
{
    /// <summary>Разделитель перечислений в описаниях.</summary>
    public const string Separator = ", ";

    /// <summary>
    /// Находит вид контента по идентификатору либо по названию.
    ///
    /// Модель охотно называет вид так, как он подписан в интерфейсе, поэтому
    /// «Заклинания» и «Заклинание» распознаются наравне с «spells».
    /// </summary>
    /// <param name="content">Служба контента.</param>
    /// <param name="text">Идентификатор или название вида.</param>
    /// <returns>Описание вида либо <see langword="null"/>.</returns>
    public static IContentTypeDescriptor? Resolve(IContentService content, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim();

        return content.FindType(value)
            ?? content.Types.FirstOrDefault(type =>
                type.DisplayName.Equals(value, StringComparison.OrdinalIgnoreCase)
                || type.SingularName.Equals(value, StringComparison.OrdinalIgnoreCase)
                || type.Id.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Находит поле вида контента по внутреннему имени либо по названию.
    /// </summary>
    /// <param name="fields">Поля вида.</param>
    /// <param name="text">Имя или название поля.</param>
    /// <returns>Описание поля либо <see langword="null"/>.</returns>
    public static IContentField? ResolveField(IReadOnlyList<IContentField> fields, string text) =>
        fields.FirstOrDefault(field => field.Name.Equals(text, StringComparison.OrdinalIgnoreCase))
        ?? fields.FirstOrDefault(field =>
            field.DisplayName.Equals(text, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Описывает поля вида контента: что можно заполнить и в каком виде.
    /// </summary>
    /// <param name="descriptor">Описание вида контента.</param>
    /// <returns>Текст описания.</returns>
    public static string DescribeType(IContentTypeDescriptor descriptor)
    {
        var builder = new StringBuilder();

        builder.Append("Вид «").Append(descriptor.Id).Append("» — ").Append(descriptor.DisplayName)
            .Append(" (один объект: ").Append(descriptor.SingularName).AppendLine(").");

        if (!string.IsNullOrWhiteSpace(descriptor.Description))
        {
            builder.AppendLine(descriptor.Description);
        }

        builder.AppendLine("Поля:");

        foreach (var field in descriptor.Fields)
        {
            builder.Append("- ").Append(field.Name)
                .Append(" («").Append(field.DisplayName).Append("»), ")
                .Append(DescribeKind(field));

            if (field.IsRequired)
            {
                builder.Append(", обязательное");
            }

            if (!string.IsNullOrWhiteSpace(field.Hint))
            {
                builder.Append(". ").Append(field.Hint);
            }

            builder.AppendLine();
        }

        if (descriptor.Collections.Count > 0)
        {
            builder.AppendLine("Вложенные списки (заполняются инструментом add_list_item):");

            foreach (var list in descriptor.Collections)
            {
                builder.Append("- ").Append(list.Name)
                    .Append(" («").Append(list.DisplayName).Append("», одна запись: ")
                    .Append(list.SingularName).AppendLine("):");

                foreach (var field in list.Fields)
                {
                    builder.Append("    - ").Append(field.Name)
                        .Append(" («").Append(field.DisplayName).Append("»), ")
                        .AppendLine(DescribeKind(field));
                }
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Описывает объект: значения всех заполненных полей.
    /// </summary>
    /// <param name="descriptor">Описание вида контента.</param>
    /// <param name="entity">Игровой объект.</param>
    /// <returns>Текст описания.</returns>
    public static string DescribeEntity(IContentTypeDescriptor descriptor, EntityBase entity)
    {
        var builder = new StringBuilder();

        builder.Append(descriptor.SingularName).Append(" «").Append(descriptor.GetName(entity))
            .Append("», идентификатор ").Append(entity.Id).AppendLine(".");

        if (entity is ContentEntity { IsSystem: true })
        {
            builder.AppendLine("Объект системный: изменить его нельзя, можно только создать копию.");
        }

        foreach (var field in descriptor.Fields)
        {
            var value = ReadField(field, entity);

            if (!string.IsNullOrWhiteSpace(value))
            {
                builder.Append("- ").Append(field.Name).Append(": ").AppendLine(value);
            }
        }

        foreach (var list in descriptor.Collections)
        {
            var items = list.GetItems(entity);

            if (items.Count == 0)
            {
                continue;
            }

            builder.Append("Список ").Append(list.Name).Append(" (").Append(items.Count).AppendLine("):");

            foreach (var item in items)
            {
                var parts = list.Fields
                    .Select(field => (field.Name, Value: ReadField(field, item)))
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                    .Select(pair => $"{pair.Name}: {pair.Value}");

                builder.Append("    - ").AppendLine(string.Join(Separator, parts));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Читает значение поля в виде текста.
    /// </summary>
    /// <param name="field">Описание поля.</param>
    /// <param name="entity">Объект или вложенная запись.</param>
    /// <returns>Текстовое значение.</returns>
    public static string ReadField(IContentField field, object entity) => field.Kind switch
    {
        ContentFieldKind.Boolean => field.GetBoolean(entity) ? "да" : "нет",
        ContentFieldKind.Reference => field.GetReference(entity)?.ToString() ?? string.Empty,
        _ => field.GetText(entity),
    };

    /// <summary>
    /// Описывает способ ввода значения поля.
    /// </summary>
    /// <param name="field">Описание поля.</param>
    /// <returns>Название способа ввода.</returns>
    private static string DescribeKind(IContentField field) => field.Kind switch
    {
        ContentFieldKind.LongText => "текст в несколько строк",
        ContentFieldKind.WholeNumber => field.IsOptional ? "целое число или пусто" : "целое число",
        ContentFieldKind.Number => field.IsOptional ? "число или пусто" : "число",
        ContentFieldKind.Boolean => "да или нет",
        ContentFieldKind.Formula => "формула движка вычислений",
        ContentFieldKind.Reference =>
            $"ссылка на объект вида «{field.ReferenceTypeId}»: идентификатор или точное название",
        ContentFieldKind.Enumeration =>
            $"одно из значений: {string.Join(Separator, field.Options)}",
        ContentFieldKind.Color => "цвет в записи #RRGGBB",
        ContentFieldKind.Image => "путь к изображению",
        _ => "текст",
    };
}
