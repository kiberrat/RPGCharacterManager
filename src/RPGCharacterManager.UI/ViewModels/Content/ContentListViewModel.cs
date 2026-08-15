using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Content;

/// <summary>
/// Одна запись списка вложенных объектов в форме редактирования.
/// </summary>
public sealed class ContentListRowViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт строку списка.
    /// </summary>
    /// <param name="item">Редактируемая запись.</param>
    /// <param name="fields">Поля записи.</param>
    public ContentListRowViewModel(object item, IEnumerable<ContentFieldViewModel> fields)
    {
        Item = Guard.NotNull(item);
        Fields = new ObservableCollection<ContentFieldViewModel>(fields);
    }

    /// <summary>Редактируемая запись.</summary>
    public object Item { get; }

    /// <summary>Поля записи.</summary>
    public ObservableCollection<ContentFieldViewModel> Fields { get; }
}

/// <summary>
/// Раздел формы редактирования со списком вложенных записей.
///
/// Состав полей строки берётся из описания списка, поэтому редактор одинаково
/// показывает бонусы предмета и любой другой список, добавленный в будущем.
/// </summary>
public sealed partial class ContentListViewModel : ViewModelBase
{
    private readonly IContentList _list;
    private readonly EntityBase _entity;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ContentReference>> _references;
    private readonly Action _changed;

    /// <summary>
    /// Создаёт раздел списка.
    /// </summary>
    /// <param name="list">Описание списка.</param>
    /// <param name="entity">Редактируемый объект.</param>
    /// <param name="references">Перечни объектов для полей-ссылок.</param>
    /// <param name="changed">Обратный вызов при изменении значения.</param>
    public ContentListViewModel(
        IContentList list,
        EntityBase entity,
        IReadOnlyDictionary<string, IReadOnlyList<ContentReference>> references,
        Action changed)
    {
        _list = Guard.NotNull(list);
        _entity = Guard.NotNull(entity);
        _references = Guard.NotNull(references);
        _changed = Guard.NotNull(changed);

        Rows = new ObservableCollection<ContentListRowViewModel>(
            list.GetItems(entity).Select(CreateRow));
    }

    /// <summary>Записи списка.</summary>
    public ObservableCollection<ContentListRowViewModel> Rows { get; }

    /// <summary>Название раздела.</summary>
    public string Title => _list.DisplayName;

    /// <summary>Пояснение к списку.</summary>
    public string Description => _list.Description;

    /// <summary>Пояснение задано.</summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(_list.Description);

    /// <summary>Надпись на кнопке добавления записи.</summary>
    public string AddText => $"Добавить: {_list.SingularName.ToLowerInvariant()}";

    /// <summary>Список пуст.</summary>
    public bool IsEmpty => Rows.Count == 0;

    /// <summary>
    /// Переносит введённые значения всех записей в объект.
    /// </summary>
    /// <param name="error">Описание ошибки, если значение недопустимо.</param>
    /// <returns><see langword="true"/>, если все значения записаны.</returns>
    public bool TryApply(out string? error)
    {
        foreach (var field in Rows.SelectMany(row => row.Fields))
        {
            if (!field.TryApply(out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Добавляет в список новую запись.
    /// </summary>
    [RelayCommand]
    private void Add()
    {
        Rows.Add(CreateRow(_list.AddItem(_entity)));

        OnPropertyChanged(nameof(IsEmpty));
        _changed();
    }

    /// <summary>
    /// Удаляет запись из списка.
    /// </summary>
    /// <param name="row">Удаляемая строка.</param>
    [RelayCommand]
    private void Remove(ContentListRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        _list.RemoveItem(_entity, row.Item);
        Rows.Remove(row);

        OnPropertyChanged(nameof(IsEmpty));
        _changed();
    }

    private ContentListRowViewModel CreateRow(object item)
    {
        var fields = _list.Fields.Select(field => new ContentFieldViewModel(
            field,
            (EntityBase)item,
            field.ReferenceTypeId is { } referenceType && _references.TryGetValue(referenceType, out var list)
                ? list
                : [],
            _changed));

        return new ContentListRowViewModel(item, fields);
    }
}
