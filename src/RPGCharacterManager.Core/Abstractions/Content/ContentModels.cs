namespace RPGCharacterManager.Core.Abstractions.Content;

/// <summary>
/// Способ ввода значения поля игрового объекта.
/// Определяет, каким элементом управления поле отображается в редакторе контента.
/// </summary>
public enum ContentFieldKind
{
    /// <summary>Однострочный текст.</summary>
    Text = 0,

    /// <summary>Многострочный текст.</summary>
    LongText = 1,

    /// <summary>Целое число.</summary>
    WholeNumber = 2,

    /// <summary>Дробное число.</summary>
    Number = 3,

    /// <summary>Логическое значение.</summary>
    Boolean = 4,

    /// <summary>Выражение движка формул.</summary>
    Formula = 5,

    /// <summary>Ссылка на другой игровой объект.</summary>
    Reference = 6,

    /// <summary>Выбор одного значения из перечня.</summary>
    Enumeration = 7,

    /// <summary>Цвет в шестнадцатеричной записи.</summary>
    Color = 8,

    /// <summary>Путь к изображению.</summary>
    Image = 9,
}

/// <summary>
/// Названия групп полей в форме редактирования.
/// Собраны в одном месте, чтобы разные типы контента использовали одинаковые разделы.
/// </summary>
public static class ContentFieldGroups
{
    /// <summary>Основные сведения: название, описание, источник.</summary>
    public const string General = "Основное";

    /// <summary>Игровые параметры объекта.</summary>
    public const string Rules = "Игровые параметры";

    /// <summary>Формулы и вычисления.</summary>
    public const string Formulas = "Формулы";

    /// <summary>Требования к использованию объекта.</summary>
    public const string Requirements = "Требования";

    /// <summary>Внешний вид: изображение, значок, цвет.</summary>
    public const string Appearance = "Оформление";

    /// <summary>Пользовательские свойства, добавленные самим пользователем.</summary>
    public const string CustomProperties = "Пользовательские свойства";
}

/// <summary>
/// Строка списка игровых объектов.
/// Содержит только сведения, необходимые для отображения, чтобы список
/// оставался быстрым при сотнях тысяч записей.
/// </summary>
/// <param name="Id">Идентификатор объекта.</param>
/// <param name="Name">Название объекта.</param>
/// <param name="Description">Краткое описание.</param>
/// <param name="IsSystem">Объект является системным и недоступен для изменения.</param>
public sealed record ContentItem(Guid Id, string Name, string? Description, bool IsSystem);

/// <summary>
/// Элемент перечня для полей-ссылок.
/// </summary>
/// <param name="Id">Идентификатор объекта.</param>
/// <param name="Name">Название объекта.</param>
public sealed record ContentReference(Guid Id, string Name)
{
    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// Владелец игровых объектов: игровая система, расширение или и то, и другое.
///
/// Заданы оба — объект должен принадлежать обоим сразу. Не задано ничего —
/// владельца нет, и отбор пуст: выгружать «всё подряд» приложение не берётся.
/// </summary>
/// <param name="GameSystemId">Игровая система.</param>
/// <param name="ContentPackId">Расширение.</param>
public sealed record ContentOwner(Guid? GameSystemId = null, Guid? ContentPackId = null)
{
    /// <summary>Владелец задан хотя бы одним признаком.</summary>
    public bool IsSpecified => GameSystemId.HasValue || ContentPackId.HasValue;
}
