using System.Globalization;

namespace RPGCharacterManager.Core.Abstractions.Content;

/// <summary>
/// Описание одного поля игрового объекта.
///
/// Редактор контента строит форму по этим описаниям и не знает о конкретных типах,
/// поэтому добавление нового вида контента не требует изменения интерфейса.
/// </summary>
public interface IContentField
{
    /// <summary>Внутреннее имя поля.</summary>
    string Name { get; }

    /// <summary>Отображаемое название поля.</summary>
    string DisplayName { get; }

    /// <summary>Способ ввода значения.</summary>
    ContentFieldKind Kind { get; }

    /// <summary>Раздел формы, в котором отображается поле.</summary>
    string Group { get; }

    /// <summary>Поле обязательно к заполнению.</summary>
    bool IsRequired { get; }

    /// <summary>
    /// Числовое поле допускает пустое значение.
    ///
    /// Пустое значение необязательного числа означает «не задано», а не ноль:
    /// незаполненный максимум характеристики не должен ограничивать её нулём.
    /// </summary>
    bool IsOptional { get; }

    /// <summary>Пояснение к полю.</summary>
    string? Hint { get; }

    /// <summary>
    /// Идентификатор типа контента, на который ссылается поле.
    /// Заполняется для полей вида <see cref="ContentFieldKind.Reference"/>.
    /// </summary>
    string? ReferenceTypeId { get; }

    /// <summary>
    /// Допустимые значения поля вида <see cref="ContentFieldKind.Enumeration"/>.
    /// </summary>
    IReadOnlyList<string> Options { get; }

    /// <summary>
    /// Возвращает текстовое представление значения поля.
    /// </summary>
    /// <param name="entity">Игровой объект.</param>
    /// <returns>Текст для отображения в редакторе.</returns>
    string GetText(object entity);

    /// <summary>
    /// Разбирает введённый текст и записывает значение в объект.
    /// </summary>
    /// <param name="entity">Игровой объект.</param>
    /// <param name="text">Введённый текст.</param>
    /// <param name="error">Описание ошибки разбора.</param>
    /// <returns><see langword="true"/>, если значение записано.</returns>
    bool TrySetText(object entity, string? text, out string? error);

    /// <summary>
    /// Возвращает логическое значение поля вида <see cref="ContentFieldKind.Boolean"/>.
    /// </summary>
    /// <param name="entity">Игровой объект.</param>
    /// <returns>Значение поля.</returns>
    bool GetBoolean(object entity);

    /// <summary>
    /// Записывает логическое значение поля.
    /// </summary>
    /// <param name="entity">Игровой объект.</param>
    /// <param name="value">Новое значение.</param>
    void SetBoolean(object entity, bool value);

    /// <summary>
    /// Возвращает значение поля-ссылки.
    /// </summary>
    /// <param name="entity">Игровой объект.</param>
    /// <returns>Идентификатор связанного объекта или <see langword="null"/>.</returns>
    Guid? GetReference(object entity);

    /// <summary>
    /// Записывает значение поля-ссылки.
    /// </summary>
    /// <param name="entity">Игровой объект.</param>
    /// <param name="value">Идентификатор связанного объекта.</param>
    void SetReference(object entity, Guid? value);

    /// <summary>
    /// Копирует значение поля из одного объекта в другой.
    /// Используется при сохранении и создании копии объекта.
    /// </summary>
    /// <param name="source">Объект-источник.</param>
    /// <param name="destination">Объект-приёмник.</param>
    void CopyValue(object source, object destination);
}

/// <summary>
/// Описание списка вложенных записей игрового объекта.
///
/// Некоторые свойства объекта нельзя выразить одним полем: у предмета несколько
/// бонусов, у заклинания несколько уровней усиления. Редактор строит для такого
/// списка отдельный раздел с теми же описаниями полей, поэтому новый список
/// добавляется описанием и не требует изменения интерфейса.
/// </summary>
public interface IContentList
{
    /// <summary>Внутреннее имя списка.</summary>
    string Name { get; }

    /// <summary>Название раздела: «Бонусы».</summary>
    string DisplayName { get; }

    /// <summary>Название одной записи: «Бонус».</summary>
    string SingularName { get; }

    /// <summary>Пояснение к списку.</summary>
    string Description { get; }

    /// <summary>Поля одной записи в порядке отображения.</summary>
    IReadOnlyList<IContentField> Fields { get; }

    /// <summary>
    /// Возвращает записи списка.
    /// </summary>
    /// <param name="entity">Игровой объект.</param>
    /// <returns>Записи в порядке отображения.</returns>
    IReadOnlyList<object> GetItems(object entity);

    /// <summary>
    /// Создаёт новую запись и добавляет её объекту.
    /// </summary>
    /// <param name="entity">Игровой объект.</param>
    /// <returns>Созданная запись.</returns>
    object AddItem(object entity);

    /// <summary>
    /// Удаляет запись из списка.
    /// </summary>
    /// <param name="entity">Игровой объект.</param>
    /// <param name="item">Удаляемая запись.</param>
    void RemoveItem(object entity, object item);

    /// <summary>
    /// Переносит записи списка из одного объекта в другой.
    /// Применяется при сохранении и создании копии объекта.
    /// </summary>
    /// <param name="source">Объект-источник.</param>
    /// <param name="destination">Объект-приёмник.</param>
    void CopyItems(object source, object destination);
}

/// <summary>
/// Поле игрового объекта с типизированным доступом к значению.
/// </summary>
/// <typeparam name="TEntity">Тип игрового объекта.</typeparam>
public sealed class ContentField<TEntity> : IContentField
    where TEntity : class
{
    private const string DecimalFormat = "0.####";

    private readonly Func<TEntity, object?> _get;
    private readonly Action<TEntity, object?> _set;

    /// <summary>
    /// Создаёт описание поля.
    /// </summary>
    /// <param name="name">Внутреннее имя поля.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="kind">Способ ввода значения.</param>
    /// <param name="get">Чтение значения из объекта.</param>
    /// <param name="set">Запись значения в объект.</param>
    public ContentField(
        string name,
        string displayName,
        ContentFieldKind kind,
        Func<TEntity, object?> get,
        Action<TEntity, object?> set)
    {
        Name = name;
        DisplayName = displayName;
        Kind = kind;
        _get = get;
        _set = set;
        Group = ContentFieldGroups.General;
        Options = [];
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public ContentFieldKind Kind { get; }

    /// <inheritdoc />
    public string Group { get; init; }

    /// <inheritdoc />
    public bool IsRequired { get; init; }

    /// <inheritdoc />
    public bool IsOptional { get; init; }

    /// <inheritdoc />
    public string? Hint { get; init; }

    /// <inheritdoc />
    public string? ReferenceTypeId { get; init; }

    /// <inheritdoc />
    public IReadOnlyList<string> Options { get; init; }

    /// <inheritdoc />
    public string GetText(object entity) => _get(Cast(entity)) switch
    {
        null => string.Empty,
        double number => number.ToString(DecimalFormat, CultureInfo.CurrentCulture),
        int number => number.ToString(CultureInfo.CurrentCulture),
        bool flag => flag ? "да" : "нет",
        Guid identifier => identifier.ToString(),
        var value => value.ToString() ?? string.Empty,
    };

    /// <inheritdoc />
    public bool TrySetText(object entity, string? text, out string? error)
    {
        var target = Cast(entity);
        var trimmed = text?.Trim();

        if (IsRequired && string.IsNullOrWhiteSpace(trimmed))
        {
            error = $"Поле «{DisplayName}» обязательно к заполнению.";
            return false;
        }

        switch (Kind)
        {
            case ContentFieldKind.WholeNumber:
                return TrySetInteger(target, trimmed, out error);

            case ContentFieldKind.Number:
                return TrySetNumber(target, trimmed, out error);

            default:
                _set(target, string.IsNullOrWhiteSpace(trimmed) ? null : trimmed);
                error = null;
                return true;
        }
    }

    /// <inheritdoc />
    public bool GetBoolean(object entity) => _get(Cast(entity)) is true;

    /// <inheritdoc />
    public void SetBoolean(object entity, bool value) => _set(Cast(entity), value);

    /// <inheritdoc />
    public Guid? GetReference(object entity) => _get(Cast(entity)) as Guid?;

    /// <inheritdoc />
    public void SetReference(object entity, Guid? value) => _set(Cast(entity), value);

    /// <inheritdoc />
    public void CopyValue(object source, object destination) =>
        _set(Cast(destination), _get(Cast(source)));

    private bool TrySetInteger(TEntity target, string? text, out string? error)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            // Необязательное число получает «не задано», обязательное — ноль.
            _set(target, IsOptional ? null : 0);
            error = null;
            return true;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value)
            || int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            _set(target, value);
            error = null;
            return true;
        }

        error = $"Поле «{DisplayName}»: требуется целое число.";
        return false;
    }

    private bool TrySetNumber(TEntity target, string? text, out string? error)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            // Необязательное число получает «не задано», обязательное — ноль.
            _set(target, IsOptional ? null : 0d);
            error = null;
            return true;
        }

        // Пользователь может ввести дробную часть как через запятую, так и через точку.
        var normalized = text.Replace(',', '.');

        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            _set(target, value);
            error = null;
            return true;
        }

        error = $"Поле «{DisplayName}»: требуется число.";
        return false;
    }

    private static TEntity Cast(object entity) => entity as TEntity
        ?? throw new ArgumentException(
            $"Ожидался объект типа {typeof(TEntity).Name}.",
            nameof(entity));
}
