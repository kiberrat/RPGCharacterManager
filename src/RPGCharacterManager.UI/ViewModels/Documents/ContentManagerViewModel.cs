using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;
using RPGCharacterManager.UI.ViewModels.Content;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Документ «Контент»: редактор всех игровых объектов.
///
/// Форма строится по описанию вида контента, поэтому новый вид объектов появляется
/// в редакторе сразу после регистрации своего описания — изменять это окно не требуется.
/// </summary>
public sealed partial class ContentManagerViewModel : DocumentViewModelBase
{
    /// <summary>Количество объектов, загружаемых в список за один раз.</summary>
    public const int PageSize = 200;

    private readonly IContentService _content;
    private readonly ICustomPropertyService _customProperties;
    private readonly IFormulaEngine _formulas;
    private readonly IBackgroundTaskService _backgroundTasks;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;

    private EntityBase? _editedEntity;
    private IReadOnlyList<PropertyDefinition> _customDefinitions = [];
    private bool _isLoadingEditor;

    [ObservableProperty]
    private IContentTypeDescriptor? _selectedType;

    [ObservableProperty]
    private ContentItem? _selectedItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _listSummary = string.Empty;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private bool _isSystemObject;

    /// <summary>
    /// Создаёт модель представления менеджера контента.
    /// </summary>
    /// <param name="content">Служба контента.</param>
    /// <param name="customProperties">Служба пользовательских свойств.</param>
    /// <param name="formulas">Движок вычислений для проверки формул.</param>
    /// <param name="backgroundTasks">Служба фоновых задач.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    /// <param name="notifications">Служба всплывающих уведомлений.</param>
    public ContentManagerViewModel(
        IContentService content,
        ICustomPropertyService customProperties,
        IFormulaEngine formulas,
        IBackgroundTaskService backgroundTasks,
        IDialogService dialogs,
        INotificationService notifications)
        : base(CoreShellContributor.ContentDocumentId, "Контент")
    {
        _content = Guard.NotNull(content);
        _customProperties = Guard.NotNull(customProperties);
        _formulas = Guard.NotNull(formulas);
        _backgroundTasks = Guard.NotNull(backgroundTasks);
        _dialogs = Guard.NotNull(dialogs);
        _notifications = Guard.NotNull(notifications);

        Types = _content.Types;
    }

    /// <summary>Виды контента, доступные для редактирования.</summary>
    public IReadOnlyList<IContentTypeDescriptor> Types { get; }

    /// <summary>Объекты выбранного вида.</summary>
    public ObservableCollection<ContentItem> Items { get; } = [];

    /// <summary>Разделы формы редактирования.</summary>
    public ObservableCollection<ContentFieldGroupViewModel> FieldGroups { get; } = [];

    /// <summary>Разделы формы со списками вложенных записей.</summary>
    public ObservableCollection<ContentListViewModel> Lists { get; } = [];

    /// <summary>Объект открыт в редакторе.</summary>
    public bool IsEditorOpen => _editedEntity is not null;

    /// <summary>Список объектов пуст.</summary>
    public bool IsListEmpty => Items.Count == 0;

    /// <summary>Вид контента выбран.</summary>
    public bool IsTypeSelected => SelectedType is not null;

    /// <inheritdoc />
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Types.Count > 0)
        {
            SelectedType = Types[0];
            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <inheritdoc />
    public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
    {
        if (!HasUnsavedChanges)
        {
            return true;
        }

        return await _dialogs.ShowConfirmationAsync(
                "Несохранённый объект",
                "В открытом объекте есть несохранённые изменения. Закрыть раздел и потерять их?")
            .ConfigureAwait(true);
    }

    /// <summary>
    /// Перечитывает список объектов выбранного вида.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    [RelayCommand]
    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        var type = SelectedType;

        if (type is null)
        {
            return;
        }

        var page = await _backgroundTasks
            .RunAsync(
                $"Загрузка: {type.DisplayName}",
                token => _content.SearchAsync(type.Id, SearchText, 0, PageSize, token),
                cancellationToken)
            .ConfigureAwait(true);

        Items.Clear();

        foreach (var item in page.Items)
        {
            Items.Add(item);
        }

        ListSummary = page.TotalCount > page.Items.Count
            ? string.Create(
                CultureInfo.CurrentCulture,
                $"Показано {page.Items.Count} из {page.TotalCount}. Уточните поиск, чтобы увидеть остальные.")
            : string.Create(CultureInfo.CurrentCulture, $"Объектов: {page.TotalCount}");

        OnPropertyChanged(nameof(IsListEmpty));
    }

    /// <summary>
    /// Создаёт новый объект выбранного вида.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после открытия объекта в редакторе.</returns>
    [RelayCommand]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        var type = SelectedType;

        if (type is null)
        {
            return;
        }

        var entity = type.CreateInstance();
        type.SetName(entity, $"Новый объект: {type.SingularName}");

        SelectedItem = null;
        await OpenEditorAsync(type, entity, cancellationToken).ConfigureAwait(true);

        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Создаёт копию выбранного объекта.
    ///
    /// Копирование — способ изменить системный объект: исходный остаётся неизменным.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после открытия копии в редакторе.</returns>
    [RelayCommand]
    private async Task DuplicateAsync(CancellationToken cancellationToken)
    {
        var type = SelectedType;
        var item = SelectedItem;

        if (type is null || item is null)
        {
            return;
        }

        var result = await _content.DuplicateAsync(type.Id, item.Id, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Копирование", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);
            return;
        }

        SelectedItem = null;
        await OpenEditorAsync(type, result.Value, cancellationToken).ConfigureAwait(true);

        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Сохраняет открытый объект.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var type = SelectedType;
        var entity = _editedEntity;

        if (type is null || entity is null)
        {
            return;
        }

        if (!TryApplyFields(out var error))
        {
            await _dialogs.ShowErrorAsync("Проверка полей", error!).ConfigureAwait(true);
            return;
        }

        var result = await _backgroundTasks
            .RunAsync(
                "Сохранение объекта",
                token => _content.SaveAsync(type.Id, entity, token),
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Сохранение", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);
            return;
        }

        await SaveCustomPropertiesAsync(entity.Id, cancellationToken).ConfigureAwait(true);

        HasUnsavedChanges = false;
        _notifications.Show($"{type.SingularName}: «{type.GetName(entity)}» сохранён", NotificationKind.Success);

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
        SelectedItem = Items.FirstOrDefault(item => item.Id == entity.Id);
    }

    /// <summary>
    /// Удаляет выбранный объект.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        var type = SelectedType;
        var item = SelectedItem;

        if (type is null || item is null)
        {
            return;
        }

        if (item.IsSystem)
        {
            await _dialogs.ShowInformationAsync(
                    "Системный объект",
                    "Системные объекты нельзя удалить. Отключите игровую систему или контент-пак, к которому объект относится.")
                .ConfigureAwait(true);
            return;
        }

        var confirmed = await _dialogs
            .ShowConfirmationAsync("Удаление", $"Удалить объект «{item.Name}»?")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var result = await _content.DeleteAsync(type.Id, item.Id, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Удаление", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);
            return;
        }

        CloseEditor();
        _notifications.Show($"Объект «{item.Name}» удалён", NotificationKind.Success);

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Закрывает открытый объект без сохранения.
    /// </summary>
    [RelayCommand]
    private void CloseCurrent() => CloseEditor();

    partial void OnSelectedTypeChanged(IContentTypeDescriptor? value)
    {
        CloseEditor();
        OnPropertyChanged(nameof(IsTypeSelected));

        _ = ReloadAsync(CancellationToken.None);
    }

    partial void OnSelectedItemChanged(ContentItem? value)
    {
        if (value is not null && SelectedType is not null)
        {
            _ = LoadSelectedAsync(SelectedType, value.Id);
        }
    }

    partial void OnSearchTextChanged(string value) => _ = ReloadAsync(CancellationToken.None);

    private async Task LoadSelectedAsync(IContentTypeDescriptor type, Guid id)
    {
        var entity = await _content.GetAsync(type.Id, id).ConfigureAwait(true);

        if (entity is not null)
        {
            await OpenEditorAsync(type, entity, CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async Task OpenEditorAsync(
        IContentTypeDescriptor type,
        EntityBase entity,
        CancellationToken cancellationToken)
    {
        _isLoadingEditor = true;

        try
        {
            _editedEntity = entity;
            EditorTitle = $"{type.SingularName}: {type.GetName(entity)}";
            IsSystemObject = entity is ContentEntity { IsSystem: true };

            FieldGroups.Clear();
            Lists.Clear();

            // Перечни для полей-ссылок загружаются один раз на форму, а не на каждое поле.
            var references = await LoadReferencesAsync(type, cancellationToken).ConfigureAwait(true);

            foreach (var group in type.Fields.GroupBy(field => field.Group))
            {
                var fields = group.Select(field => new ContentFieldViewModel(
                    field,
                    entity,
                    field.ReferenceTypeId is { } referenceType && references.TryGetValue(referenceType, out var list)
                        ? list
                        : [],
                    MarkChanged));

                FieldGroups.Add(new ContentFieldGroupViewModel(group.Key, fields));
            }

            foreach (var list in type.Collections)
            {
                Lists.Add(new ContentListViewModel(list, entity, references, MarkChanged));
            }

            await LoadCustomPropertiesAsync(type, entity, cancellationToken).ConfigureAwait(true);

            HasUnsavedChanges = false;
            OnPropertyChanged(nameof(IsEditorOpen));
        }
        finally
        {
            _isLoadingEditor = false;
        }
    }

    private async Task<Dictionary<string, IReadOnlyList<ContentReference>>> LoadReferencesAsync(
        IContentTypeDescriptor type,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, IReadOnlyList<ContentReference>>(StringComparer.Ordinal);

        // Поля списков вложенных записей тоже могут ссылаться на другие виды контента,
        // поэтому перечни собираются и по ним: иначе список выбора остался бы пустым.
        var referenceTypes = type.Fields
            .Concat(type.Collections.SelectMany(list => list.Fields))
            .Select(field => field.ReferenceTypeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal);

        foreach (var referenceType in referenceTypes)
        {
            result[referenceType!] = await _content
                .GetReferencesAsync(referenceType!, cancellationToken)
                .ConfigureAwait(true);
        }

        return result;
    }

    /// <summary>
    /// Загружает пользовательские свойства вида контента и их значения для объекта.
    /// Свойства отображаются отдельным разделом формы наравне со встроенными полями.
    /// </summary>
    /// <param name="type">Вид контента.</param>
    /// <param name="entity">Редактируемый объект.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    private async Task LoadCustomPropertiesAsync(
        IContentTypeDescriptor type,
        EntityBase entity,
        CancellationToken cancellationToken)
    {
        _customDefinitions = await _customProperties
            .GetDefinitionsAsync(type.Id, cancellationToken)
            .ConfigureAwait(true);

        if (_customDefinitions.Count == 0)
        {
            return;
        }

        var values = await _customProperties
            .GetValuesAsync(entity.Id, cancellationToken)
            .ConfigureAwait(true);

        var holder = new CustomPropertyValues();

        foreach (var definition in _customDefinitions)
        {
            holder.Values[definition.Id] =
                values.TryGetValue(definition.Id, out var value) ? value : definition.DefaultValue;
        }

        _customPropertyValues = holder;

        var fields = _customDefinitions.Select(definition => new ContentFieldViewModel(
            CustomPropertyField.Create(definition, holder),
            entity,
            [],
            MarkChanged));

        FieldGroups.Add(new ContentFieldGroupViewModel(ContentFieldGroups.CustomProperties, fields));
    }

    private CustomPropertyValues? _customPropertyValues;

    private async Task SaveCustomPropertiesAsync(Guid objectId, CancellationToken cancellationToken)
    {
        if (_customPropertyValues is null || _customDefinitions.Count == 0)
        {
            return;
        }

        await _customProperties
            .SaveValuesAsync(objectId, _customPropertyValues.Values, cancellationToken)
            .ConfigureAwait(true);
    }

    private bool TryApplyFields(out string? error)
    {
        foreach (var group in FieldGroups)
        {
            foreach (var field in group.Fields)
            {
                if (!field.TryApply(out error))
                {
                    return false;
                }

                if (field.IsFormula && !ValidateFormula(field))
                {
                    error = $"Поле «{field.DisplayName}» содержит ошибку в формуле.";
                    return false;
                }
            }
        }

        foreach (var list in Lists)
        {
            if (!list.TryApply(out error))
            {
                return false;
            }

            foreach (var field in list.Rows.SelectMany(row => row.Fields).Where(field => field.IsFormula))
            {
                if (!ValidateFormula(field))
                {
                    error = $"Поле «{field.DisplayName}» в разделе «{list.Title}» содержит ошибку в формуле.";
                    return false;
                }
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Проверяет формулу поля единым движком вычислений.
    /// Ошибка показывается рядом с полем, а не общим сообщением.
    /// </summary>
    /// <param name="field">Проверяемое поле.</param>
    /// <returns><see langword="true"/>, если формула корректна или не задана.</returns>
    private bool ValidateFormula(ContentFieldViewModel field)
    {
        if (string.IsNullOrWhiteSpace(field.Text))
        {
            field.ShowError(null);
            return true;
        }

        var result = _formulas.Validate(field.Text);
        field.ShowError(result.IsFailure ? result.Error : null);

        return result.IsSuccess;
    }

    private void MarkChanged()
    {
        if (!_isLoadingEditor)
        {
            HasUnsavedChanges = true;
        }
    }

    private void CloseEditor()
    {
        _editedEntity = null;
        _customPropertyValues = null;
        _customDefinitions = [];

        FieldGroups.Clear();
        Lists.Clear();
        EditorTitle = string.Empty;
        IsSystemObject = false;
        HasUnsavedChanges = false;

        OnPropertyChanged(nameof(IsEditorOpen));
    }
}

/// <summary>
/// Значения пользовательских свойств редактируемого объекта.
/// </summary>
internal sealed class CustomPropertyValues
{
    /// <summary>Значения, сопоставленные идентификатору описания свойства.</summary>
    public Dictionary<Guid, string?> Values { get; } = [];
}

/// <summary>
/// Поле формы, отображающее пользовательское свойство.
///
/// Позволяет показать пользовательские свойства теми же средствами, что и встроенные
/// поля: редактор не различает их между собой.
/// </summary>
internal static class CustomPropertyField
{
    /// <summary>
    /// Создаёт описание поля для пользовательского свойства.
    /// </summary>
    /// <param name="definition">Описание свойства.</param>
    /// <param name="holder">Хранилище значений редактируемого объекта.</param>
    /// <returns>Описание поля формы.</returns>
    public static IContentField Create(PropertyDefinition definition, CustomPropertyValues holder)
    {
        var id = definition.Id;

        return new ContentField<EntityBase>(
            definition.SystemName,
            definition.DisplayName,
            ToFieldKind(definition.DataType),
            _ => holder.Values.TryGetValue(id, out var value) ? value : null,
            (_, value) => holder.Values[id] = value as string)
        {
            Group = ContentFieldGroups.CustomProperties,
            IsRequired = definition.IsRequired,
            Hint = definition.Description,
        };
    }

    private static ContentFieldKind ToFieldKind(GameValueType dataType) => dataType switch
    {
        GameValueType.WholeNumber => ContentFieldKind.WholeNumber,
        GameValueType.FractionalNumber or GameValueType.Percent => ContentFieldKind.Number,
        GameValueType.Boolean => ContentFieldKind.Boolean,
        GameValueType.LongText or GameValueType.Markdown or GameValueType.Json => ContentFieldKind.LongText,
        GameValueType.Formula or GameValueType.DiceFormula => ContentFieldKind.Formula,
        GameValueType.Color => ContentFieldKind.Color,
        GameValueType.Image => ContentFieldKind.Image,
        _ => ContentFieldKind.Text,
    };
}
