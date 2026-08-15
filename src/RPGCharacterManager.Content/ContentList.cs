using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Content;

/// <summary>
/// Список вложенных записей игрового объекта, описанный набором полей.
/// </summary>
/// <typeparam name="TEntity">Тип игрового объекта.</typeparam>
/// <typeparam name="TItem">Тип вложенной записи.</typeparam>
internal sealed class ContentList<TEntity, TItem> : IContentList
    where TEntity : EntityBase
    where TItem : EntityBase, new()
{
    private readonly Func<TEntity, ICollection<TItem>> _get;
    private readonly Action<TItem, TEntity> _attach;
    private readonly Func<TItem, int> _order;

    /// <summary>
    /// Создаёт описание списка вложенных записей.
    /// </summary>
    /// <param name="name">Внутреннее имя списка.</param>
    /// <param name="displayName">Название раздела.</param>
    /// <param name="singularName">Название одной записи.</param>
    /// <param name="description">Пояснение к списку.</param>
    /// <param name="fields">Поля одной записи.</param>
    /// <param name="get">Доступ к списку объекта.</param>
    /// <param name="attach">Связывание созданной записи с объектом.</param>
    /// <param name="order">Порядок отображения записи.</param>
    public ContentList(
        string name,
        string displayName,
        string singularName,
        string description,
        IReadOnlyList<IContentField> fields,
        Func<TEntity, ICollection<TItem>> get,
        Action<TItem, TEntity> attach,
        Func<TItem, int> order)
    {
        Name = name;
        DisplayName = displayName;
        SingularName = singularName;
        Description = description;
        Fields = fields;

        _get = get;
        _attach = attach;
        _order = order;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public string SingularName { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public IReadOnlyList<IContentField> Fields { get; }

    /// <inheritdoc />
    public IReadOnlyList<object> GetItems(object entity) =>
        _get(Cast(entity)).OrderBy(_order).Cast<object>().ToList();

    /// <inheritdoc />
    public object AddItem(object entity)
    {
        var target = Cast(entity);
        var item = new TItem();

        _attach(item, target);
        _get(target).Add(item);

        return item;
    }

    /// <inheritdoc />
    public void RemoveItem(object entity, object item) => _get(Cast(entity)).Remove(CastItem(item));

    /// <inheritdoc />
    public void CopyItems(object source, object destination)
    {
        var target = Cast(destination);
        var items = _get(target);

        items.Clear();

        foreach (var original in _get(Cast(source)).OrderBy(_order))
        {
            // Запись копии получает собственный идентификатор: иначе копия предмета
            // и его источник ссылались бы на одни и те же бонусы.
            var copy = new TItem();

            foreach (var field in Fields)
            {
                field.CopyValue(original, copy);
            }

            _attach(copy, target);
            items.Add(copy);
        }
    }

    /// <summary>
    /// Приводит запись к типу элемента списка.
    /// </summary>
    /// <param name="item">Запись.</param>
    /// <returns>Запись нужного типа.</returns>
    public static TItem CastItem(object item) => item as TItem
        ?? throw new ArgumentException($"Ожидалась запись типа {typeof(TItem).Name}.", nameof(item));

    private static TEntity Cast(object entity) => entity as TEntity
        ?? throw new ArgumentException(
            $"Ожидался объект типа {typeof(TEntity).Name}.",
            nameof(entity));
}

/// <summary>
/// Построитель описания списка вложенных записей.
/// </summary>
/// <typeparam name="TEntity">Тип игрового объекта.</typeparam>
/// <typeparam name="TItem">Тип вложенной записи.</typeparam>
internal sealed class ContentListBuilder<TEntity, TItem>
    where TEntity : EntityBase
    where TItem : EntityBase, new()
{
    private readonly List<IContentField> _fields = [];
    private readonly string _name;
    private readonly string _displayName;
    private readonly string _singularName;
    private readonly Func<TEntity, ICollection<TItem>> _get;

    private string _description = string.Empty;
    private Action<TItem, TEntity> _attach = (_, _) => { };
    private Func<TItem, int> _order = _ => 0;

    /// <summary>
    /// Создаёт построитель списка.
    /// </summary>
    /// <param name="name">Внутреннее имя списка.</param>
    /// <param name="displayName">Название раздела.</param>
    /// <param name="singularName">Название одной записи.</param>
    /// <param name="get">Доступ к списку объекта.</param>
    public ContentListBuilder(
        string name,
        string displayName,
        string singularName,
        Func<TEntity, ICollection<TItem>> get)
    {
        _name = Guard.NotNullOrWhiteSpace(name);
        _displayName = Guard.NotNullOrWhiteSpace(displayName);
        _singularName = Guard.NotNullOrWhiteSpace(singularName);
        _get = Guard.NotNull(get);
    }

    /// <summary>
    /// Задаёт пояснение к списку.
    /// </summary>
    /// <param name="description">Пояснение.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentListBuilder<TEntity, TItem> Describe(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Задаёт связывание созданной записи с объектом и порядок отображения.
    /// </summary>
    /// <param name="attach">Связывание записи с объектом.</param>
    /// <param name="order">Порядок отображения записи.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentListBuilder<TEntity, TItem> AttachedBy(
        Action<TItem, TEntity> attach,
        Func<TItem, int> order)
    {
        _attach = attach;
        _order = order;
        return this;
    }

    /// <summary>
    /// Добавляет записи текстовое поле.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <param name="hint">Пояснение к полю.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentListBuilder<TEntity, TItem> Text(
        string name,
        string displayName,
        Func<TItem, string?> get,
        Action<TItem, string?> set,
        string? hint = null) =>
        Add(name, displayName, ContentFieldKind.Text, entity => get(entity), (entity, value) => set(entity, value as string), hint);

    /// <summary>
    /// Добавляет записи поле формулы.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <param name="hint">Пояснение к полю.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentListBuilder<TEntity, TItem> Formula(
        string name,
        string displayName,
        Func<TItem, string?> get,
        Action<TItem, string?> set,
        string? hint = null) =>
        Add(name, displayName, ContentFieldKind.Formula, entity => get(entity), (entity, value) => set(entity, value as string), hint);

    /// <summary>
    /// Добавляет записи целочисленное поле.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentListBuilder<TEntity, TItem> Integer(
        string name,
        string displayName,
        Func<TItem, int> get,
        Action<TItem, int> set) =>
        Add(
            name,
            displayName,
            ContentFieldKind.WholeNumber,
            entity => get(entity),
            (entity, value) => set(entity, value is int number ? number : 0),
            null);

    /// <summary>
    /// Добавляет записи логическое поле.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentListBuilder<TEntity, TItem> Boolean(
        string name,
        string displayName,
        Func<TItem, bool> get,
        Action<TItem, bool> set) =>
        Add(
            name,
            displayName,
            ContentFieldKind.Boolean,
            entity => get(entity),
            (entity, value) => set(entity, value is true),
            null);

    /// <summary>
    /// Добавляет записи поле-ссылку на объект другого вида контента.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="referenceTypeId">Идентификатор вида контента.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentListBuilder<TEntity, TItem> Reference(
        string name,
        string displayName,
        string referenceTypeId,
        Func<TItem, Guid?> get,
        Action<TItem, Guid?> set)
    {
        _fields.Add(new ContentField<TItem>(
            name,
            displayName,
            ContentFieldKind.Reference,
            entity => get(entity),
            (entity, value) => set(entity, value as Guid?))
        {
            ReferenceTypeId = referenceTypeId,
        });

        return this;
    }

    /// <summary>
    /// Добавляет записи поле выбора одного значения из перечня.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="options">Допустимые значения в порядке отображения.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <param name="hint">Пояснение к полю.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentListBuilder<TEntity, TItem> Enumeration(
        string name,
        string displayName,
        IReadOnlyList<string> options,
        Func<TItem, string?> get,
        Action<TItem, string?> set,
        string? hint = null)
    {
        _fields.Add(new ContentField<TItem>(
            name,
            displayName,
            ContentFieldKind.Enumeration,
            entity => get(entity),
            (entity, value) => set(entity, value as string))
        {
            Options = options,
            Hint = hint,
        });

        return this;
    }

    private ContentListBuilder<TEntity, TItem> Add(
        string name,
        string displayName,
        ContentFieldKind kind,
        Func<TItem, object?> get,
        Action<TItem, object?> set,
        string? hint)
    {
        _fields.Add(new ContentField<TItem>(name, displayName, kind, get, set) { Hint = hint });
        return this;
    }

    /// <summary>
    /// Завершает построение описания списка.
    /// </summary>
    /// <returns>Готовое описание.</returns>
    public IContentList Build() => new ContentList<TEntity, TItem>(
        _name,
        _displayName,
        _singularName,
        _description,
        _fields,
        _get,
        _attach,
        _order);
}
