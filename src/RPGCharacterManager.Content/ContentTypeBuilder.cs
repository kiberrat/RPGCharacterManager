using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Content;

/// <summary>
/// Описание вида контента, построенное набором полей.
/// </summary>
/// <typeparam name="TEntity">Тип сущности базы данных.</typeparam>
internal sealed class ContentTypeDescriptor<TEntity> : IContentTypeDescriptor
    where TEntity : EntityBase, new()
{
    private readonly Func<TEntity> _factory;
    private readonly Func<TEntity, string> _getName;
    private readonly Action<TEntity, string> _setName;

    /// <summary>
    /// Создаёт описание вида контента.
    /// </summary>
    /// <param name="id">Идентификатор вида.</param>
    /// <param name="displayName">Название во множественном числе.</param>
    /// <param name="singularName">Название одного объекта.</param>
    /// <param name="description">Пояснение к виду контента.</param>
    /// <param name="order">Порядок отображения.</param>
    /// <param name="fields">Поля объекта.</param>
    /// <param name="collections">Списки вложенных записей объекта.</param>
    /// <param name="factory">Создание нового объекта.</param>
    /// <param name="getName">Чтение названия объекта.</param>
    /// <param name="setName">Запись названия объекта.</param>
    /// <param name="filter">Условие отбора объектов этого вида.</param>
    /// <param name="include">Загрузка связанных данных объекта.</param>
    public ContentTypeDescriptor(
        string id,
        string displayName,
        string singularName,
        string description,
        int order,
        IReadOnlyList<IContentField> fields,
        IReadOnlyList<IContentList> collections,
        Func<TEntity> factory,
        Func<TEntity, string> getName,
        Action<TEntity, string> setName,
        Expression<Func<TEntity, bool>>? filter,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include)
    {
        Id = id;
        DisplayName = displayName;
        SingularName = singularName;
        Description = description;
        Order = order;
        Fields = fields;
        Collections = collections;
        Filter = filter;
        Include = include;

        _factory = factory;
        _getName = getName;
        _setName = setName;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public string SingularName { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public int Order { get; }

    /// <inheritdoc />
    public Type EntityType => typeof(TEntity);

    /// <inheritdoc />
    public IReadOnlyList<IContentField> Fields { get; }

    /// <inheritdoc />
    public IReadOnlyList<IContentList> Collections { get; }

    /// <summary>Условие отбора объектов вида или <see langword="null"/>, если отбираются все.</summary>
    public Expression<Func<TEntity, bool>>? Filter { get; }

    /// <summary>Загрузка связанных данных объекта или <see langword="null"/>.</summary>
    public Func<IQueryable<TEntity>, IQueryable<TEntity>>? Include { get; }

    /// <inheritdoc />
    public EntityBase CreateInstance() => _factory();

    /// <inheritdoc />
    public string GetName(EntityBase entity) => _getName(Cast(entity));

    /// <inheritdoc />
    public void SetName(EntityBase entity, string name) => _setName(Cast(entity), name);

    private static TEntity Cast(EntityBase entity) => entity as TEntity
        ?? throw new ArgumentException($"Ожидался объект типа {typeof(TEntity).Name}.", nameof(entity));
}

/// <summary>
/// Построитель описания вида контента.
///
/// Позволяет описать новый вид объектов перечислением его полей, без написания
/// отдельного редактора и отдельного хранилища.
/// </summary>
/// <typeparam name="TEntity">Тип сущности базы данных.</typeparam>
internal sealed class ContentTypeBuilder<TEntity>
    where TEntity : EntityBase, new()
{
    private readonly List<IContentField> _fields = [];
    private readonly List<IContentList> _collections = [];
    private readonly string _id;
    private readonly string _displayName;
    private readonly string _singularName;

    private string _description = string.Empty;
    private int _order;
    private Func<TEntity> _factory = () => new TEntity();
    private Func<TEntity, string> _getName = _ => string.Empty;
    private Action<TEntity, string> _setName = (_, _) => { };
    private Expression<Func<TEntity, bool>>? _filter;
    private Func<IQueryable<TEntity>, IQueryable<TEntity>>? _include;

    /// <summary>
    /// Создаёт построитель описания вида контента.
    /// </summary>
    /// <param name="id">Идентификатор вида.</param>
    /// <param name="displayName">Название во множественном числе.</param>
    /// <param name="singularName">Название одного объекта.</param>
    public ContentTypeBuilder(string id, string displayName, string singularName)
    {
        _id = Guard.NotNullOrWhiteSpace(id);
        _displayName = Guard.NotNullOrWhiteSpace(displayName);
        _singularName = Guard.NotNullOrWhiteSpace(singularName);
    }

    /// <summary>
    /// Задаёт пояснение и порядок отображения вида контента.
    /// </summary>
    /// <param name="description">Пояснение.</param>
    /// <param name="order">Порядок отображения.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> Describe(string description, int order)
    {
        _description = description;
        _order = order;
        return this;
    }

    /// <summary>
    /// Задаёт способ создания нового объекта.
    /// </summary>
    /// <param name="factory">Фабрика объекта.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> CreatedBy(Func<TEntity> factory)
    {
        _factory = factory;
        return this;
    }

    /// <summary>
    /// Задаёт доступ к названию объекта.
    /// </summary>
    /// <param name="get">Чтение названия.</param>
    /// <param name="set">Запись названия.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> NamedBy(Func<TEntity, string> get, Action<TEntity, string> set)
    {
        _getName = get;
        _setName = set;
        return this;
    }

    /// <summary>
    /// Ограничивает состав объектов вида.
    /// </summary>
    /// <param name="filter">Условие отбора.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> FilteredBy(Expression<Func<TEntity, bool>> filter)
    {
        _filter = filter;
        return this;
    }

    /// <summary>
    /// Задаёт загрузку связанных данных объекта.
    /// </summary>
    /// <param name="include">Настройка запроса.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> Including(Func<IQueryable<TEntity>, IQueryable<TEntity>> include)
    {
        _include = include;
        return this;
    }

    /// <summary>
    /// Добавляет текстовое поле.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <param name="group">Раздел формы.</param>
    /// <param name="isRequired">Поле обязательно.</param>
    /// <param name="hint">Пояснение.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> Text(
        string name,
        string displayName,
        Func<TEntity, string?> get,
        Action<TEntity, string?> set,
        string group = ContentFieldGroups.General,
        bool isRequired = false,
        string? hint = null) =>
        Add(name, displayName, ContentFieldKind.Text, get, set, group, isRequired, hint);

    /// <summary>
    /// Добавляет многострочное текстовое поле.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <param name="group">Раздел формы.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> LongText(
        string name,
        string displayName,
        Func<TEntity, string?> get,
        Action<TEntity, string?> set,
        string group = ContentFieldGroups.General) =>
        Add(name, displayName, ContentFieldKind.LongText, get, set, group, false, null);

    /// <summary>
    /// Добавляет поле формулы.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <param name="hint">Пояснение.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> Formula(
        string name,
        string displayName,
        Func<TEntity, string?> get,
        Action<TEntity, string?> set,
        string? hint = null) =>
        Add(name, displayName, ContentFieldKind.Formula, get, set, ContentFieldGroups.Formulas, false, hint);

    /// <summary>
    /// Добавляет поле требований в виде выражения.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> Requirement(
        string name,
        string displayName,
        Func<TEntity, string?> get,
        Action<TEntity, string?> set) =>
        Add(
            name,
            displayName,
            ContentFieldKind.Formula,
            get,
            set,
            ContentFieldGroups.Requirements,
            false,
            "Условие, например: Уровень >= 5 и Сила >= 15");

    /// <summary>
    /// Добавляет целочисленное поле.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <param name="group">Раздел формы.</param>
    /// <param name="hint">Пояснение к полю.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> Integer(
        string name,
        string displayName,
        Func<TEntity, int> get,
        Action<TEntity, int> set,
        string group = ContentFieldGroups.Rules,
        string? hint = null) =>
        Add(
            name,
            displayName,
            ContentFieldKind.WholeNumber,
            entity => get(entity),
            (entity, value) => set(entity, value is int number ? number : 0),
            group,
            false,
            hint);

    /// <summary>
    /// Добавляет необязательное целочисленное поле.
    ///
    /// Пустое значение означает «не задано»: незаполненный предел не превращается
    /// в ноль и потому не ограничивает объект.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <param name="group">Раздел формы.</param>
    /// <param name="hint">Пояснение к полю.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> OptionalInteger(
        string name,
        string displayName,
        Func<TEntity, int?> get,
        Action<TEntity, int?> set,
        string group = ContentFieldGroups.Rules,
        string? hint = null)
    {
        _fields.Add(new ContentField<TEntity>(
            name,
            displayName,
            ContentFieldKind.WholeNumber,
            entity => get(entity),
            (entity, value) => set(entity, value as int?))
        {
            Group = group,
            IsOptional = true,
            Hint = hint,
        });

        return this;
    }

    /// <summary>
    /// Добавляет необязательное числовое поле.
    /// Пустое значение означает «не задано».
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <param name="group">Раздел формы.</param>
    /// <param name="hint">Пояснение к полю.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> OptionalNumber(
        string name,
        string displayName,
        Func<TEntity, double?> get,
        Action<TEntity, double?> set,
        string group = ContentFieldGroups.Rules,
        string? hint = null)
    {
        _fields.Add(new ContentField<TEntity>(
            name,
            displayName,
            ContentFieldKind.Number,
            entity => get(entity),
            (entity, value) => set(entity, value as double?))
        {
            Group = group,
            IsOptional = true,
            Hint = hint,
        });

        return this;
    }

    /// <summary>
    /// Добавляет числовое поле.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <param name="group">Раздел формы.</param>
    /// <param name="hint">Пояснение к полю.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> Number(
        string name,
        string displayName,
        Func<TEntity, double> get,
        Action<TEntity, double> set,
        string group = ContentFieldGroups.Rules,
        string? hint = null) =>
        Add(
            name,
            displayName,
            ContentFieldKind.Number,
            entity => get(entity),
            (entity, value) => set(entity, value is double number ? number : 0),
            group,
            false,
            hint);

    /// <summary>
    /// Добавляет логическое поле.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <param name="group">Раздел формы.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> Boolean(
        string name,
        string displayName,
        Func<TEntity, bool> get,
        Action<TEntity, bool> set,
        string group = ContentFieldGroups.Rules) =>
        Add(
            name,
            displayName,
            ContentFieldKind.Boolean,
            entity => get(entity),
            (entity, value) => set(entity, value is true),
            group,
            false,
            null);

    /// <summary>
    /// Добавляет поле-ссылку на объект другого вида контента.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="referenceTypeId">Идентификатор вида контента, на который ссылается поле.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <param name="group">Раздел формы.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> Reference(
        string name,
        string displayName,
        string referenceTypeId,
        Func<TEntity, Guid?> get,
        Action<TEntity, Guid?> set,
        string group = ContentFieldGroups.Rules)
    {
        _fields.Add(new ContentField<TEntity>(
            name,
            displayName,
            ContentFieldKind.Reference,
            entity => get(entity),
            (entity, value) => set(entity, value as Guid?))
        {
            Group = group,
            ReferenceTypeId = referenceTypeId,
        });

        return this;
    }

    /// <summary>
    /// Добавляет поле выбора из перечня допустимых значений.
    ///
    /// Перечень задаётся описанием вида, а не пользователем: так описываются
    /// свойства, у которых состав значений определён самим приложением.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="options">Допустимые значения в порядке отображения.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <param name="group">Раздел формы.</param>
    /// <param name="hint">Пояснение к полю.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> Enumeration(
        string name,
        string displayName,
        IReadOnlyList<string> options,
        Func<TEntity, string?> get,
        Action<TEntity, string?> set,
        string group = ContentFieldGroups.Rules,
        string? hint = null)
    {
        _fields.Add(new ContentField<TEntity>(
            name,
            displayName,
            ContentFieldKind.Enumeration,
            entity => get(entity),
            (entity, value) => set(entity, value as string))
        {
            Group = group,
            Options = options,
            Hint = hint,
        });

        return this;
    }

    /// <summary>
    /// Добавляет поле цвета.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> Color(
        string name,
        string displayName,
        Func<TEntity, string?> get,
        Action<TEntity, string?> set) =>
        Add(
            name,
            displayName,
            ContentFieldKind.Color,
            get,
            set,
            ContentFieldGroups.Appearance,
            false,
            "Цвет в записи #RRGGBB");

    /// <summary>
    /// Добавляет поле пути к изображению.
    /// </summary>
    /// <param name="name">Внутреннее имя.</param>
    /// <param name="displayName">Отображаемое название.</param>
    /// <param name="get">Чтение значения.</param>
    /// <param name="set">Запись значения.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> Image(
        string name,
        string displayName,
        Func<TEntity, string?> get,
        Action<TEntity, string?> set) =>
        Add(name, displayName, ContentFieldKind.Image, get, set, ContentFieldGroups.Appearance, false, null);

    /// <summary>
    /// Добавляет виду контента список вложенных записей.
    /// </summary>
    /// <typeparam name="TItem">Тип вложенной записи.</typeparam>
    /// <param name="name">Внутреннее имя списка.</param>
    /// <param name="displayName">Название раздела.</param>
    /// <param name="singularName">Название одной записи.</param>
    /// <param name="get">Доступ к списку объекта.</param>
    /// <param name="build">Описание полей записи.</param>
    /// <returns>Тот же построитель.</returns>
    public ContentTypeBuilder<TEntity> Collection<TItem>(
        string name,
        string displayName,
        string singularName,
        Func<TEntity, ICollection<TItem>> get,
        Action<ContentListBuilder<TEntity, TItem>> build)
        where TItem : EntityBase, new()
    {
        Guard.NotNull(build);

        var builder = new ContentListBuilder<TEntity, TItem>(name, displayName, singularName, get);

        build(builder);
        _collections.Add(builder.Build());

        return this;
    }

    /// <summary>
    /// Завершает построение описания вида контента.
    /// </summary>
    /// <returns>Готовое описание.</returns>
    public IContentTypeDescriptor Build() => new ContentTypeDescriptor<TEntity>(
        _id,
        _displayName,
        _singularName,
        _description,
        _order,
        _fields,
        _collections,
        _factory,
        _getName,
        _setName,
        _filter,
        _include);

    private ContentTypeBuilder<TEntity> Add(
        string name,
        string displayName,
        ContentFieldKind kind,
        Func<TEntity, string?> get,
        Action<TEntity, string?> set,
        string group,
        bool isRequired,
        string? hint)
    {
        _fields.Add(new ContentField<TEntity>(
            name,
            displayName,
            kind,
            entity => get(entity),
            (entity, value) => set(entity, value as string))
        {
            Group = group,
            IsRequired = isRequired,
            Hint = hint,
        });

        return this;
    }

    private ContentTypeBuilder<TEntity> Add(
        string name,
        string displayName,
        ContentFieldKind kind,
        Func<TEntity, object?> get,
        Action<TEntity, object?> set,
        string group,
        bool isRequired,
        string? hint)
    {
        _fields.Add(new ContentField<TEntity>(name, displayName, kind, get, set)
        {
            Group = group,
            IsRequired = isRequired,
            Hint = hint,
        });

        return this;
    }
}
