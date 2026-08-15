using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Content;

/// <summary>
/// Поле игрового объекта в форме редактирования.
///
/// Один класс обслуживает все виды полей: представление выбирает нужный элемент
/// ввода по признакам <see cref="IsText"/>, <see cref="IsBoolean"/> и остальным.
/// </summary>
public sealed partial class ContentFieldViewModel : ViewModelBase
{
    private readonly EntityBase _entity;
    private readonly Action _changed;

    private bool _isUpdating;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _booleanValue;

    [ObservableProperty]
    private ContentReference? _selectedReference;

    [ObservableProperty]
    private string? _selectedOption;

    [ObservableProperty]
    private string? _error;

    /// <summary>
    /// Создаёт модель представления поля.
    /// </summary>
    /// <param name="field">Описание поля.</param>
    /// <param name="entity">Редактируемый объект.</param>
    /// <param name="references">Перечень объектов для поля-ссылки.</param>
    /// <param name="changed">Обратный вызов при изменении значения.</param>
    public ContentFieldViewModel(
        IContentField field,
        EntityBase entity,
        IReadOnlyList<ContentReference> references,
        Action changed)
    {
        Field = Guard.NotNull(field);
        _entity = Guard.NotNull(entity);
        _changed = Guard.NotNull(changed);

        References = [EmptyReference, .. references];

        _isUpdating = true;

        try
        {
            switch (field.Kind)
            {
                case ContentFieldKind.Boolean:
                    _booleanValue = field.GetBoolean(entity);
                    break;

                case ContentFieldKind.Reference:
                    var current = field.GetReference(entity);
                    _selectedReference = current is null
                        ? EmptyReference
                        : References.FirstOrDefault(item => item.Id == current) ?? EmptyReference;
                    break;

                case ContentFieldKind.Enumeration:
                    var stored = field.GetText(entity);

                    // Значение, которого больше нет в перечне, заменяется первым
                    // допустимым: список вариантов задаёт игровая система и может меняться.
                    _selectedOption = field.Options.Contains(stored, StringComparer.CurrentCulture)
                        ? stored
                        : field.Options.Count > 0 ? field.Options[0] : null;
                    break;

                default:
                    _text = field.GetText(entity);
                    break;
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>Значение поля-ссылки, означающее «не задано».</summary>
    public static ContentReference EmptyReference { get; } = new(Guid.Empty, "— не задано —");

    /// <summary>Описание поля.</summary>
    public IContentField Field { get; }

    /// <summary>Отображаемое название поля.</summary>
    public string DisplayName => Field.DisplayName;

    /// <summary>Пояснение к полю.</summary>
    public string? Hint => Field.Hint;

    /// <summary>Пояснение задано.</summary>
    public bool HasHint => !string.IsNullOrWhiteSpace(Field.Hint);

    /// <summary>Ошибка ввода присутствует.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    /// <summary>Доступные значения поля-ссылки.</summary>
    public ObservableCollection<ContentReference> References { get; }

    /// <summary>Допустимые значения поля-перечня.</summary>
    public IReadOnlyList<string> Options => Field.Options;

    /// <summary>Поле вводится однострочным текстом.</summary>
    public bool IsText => Field.Kind is ContentFieldKind.Text
        or ContentFieldKind.WholeNumber
        or ContentFieldKind.Number
        or ContentFieldKind.Formula
        or ContentFieldKind.Color
        or ContentFieldKind.Image;

    /// <summary>Поле вводится многострочным текстом.</summary>
    public bool IsLongText => Field.Kind == ContentFieldKind.LongText;

    /// <summary>Поле является переключателем.</summary>
    public bool IsBoolean => Field.Kind == ContentFieldKind.Boolean;

    /// <summary>Поле является ссылкой на другой объект.</summary>
    public bool IsReference => Field.Kind == ContentFieldKind.Reference;

    /// <summary>Поле является выбором одного значения из перечня.</summary>
    public bool IsEnumeration => Field.Kind == ContentFieldKind.Enumeration;

    /// <summary>Поле содержит выражение движка формул.</summary>
    public bool IsFormula => Field.Kind == ContentFieldKind.Formula;

    /// <summary>
    /// Записывает введённые значения в объект.
    /// </summary>
    /// <param name="error">Описание ошибки, если значение недопустимо.</param>
    /// <returns><see langword="true"/>, если значение записано.</returns>
    public bool TryApply(out string? error)
    {
        switch (Field.Kind)
        {
            case ContentFieldKind.Boolean:
                Field.SetBoolean(_entity, BooleanValue);
                error = null;
                return true;

            case ContentFieldKind.Reference:
                var selected = SelectedReference;
                Field.SetReference(
                    _entity,
                    selected is null || selected.Id == Guid.Empty ? null : selected.Id);
                error = null;
                return true;

            case ContentFieldKind.Enumeration:
                return Field.TrySetText(_entity, SelectedOption, out error);

            default:
                var applied = Field.TrySetText(_entity, Text, out error);
                Error = error;
                OnPropertyChanged(nameof(HasError));
                return applied;
        }
    }

    /// <summary>
    /// Показывает ошибку проверки, обнаруженную вне поля, например ошибку формулы.
    /// </summary>
    /// <param name="message">Текст ошибки или <see langword="null"/>.</param>
    public void ShowError(string? message)
    {
        Error = message;
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnTextChanged(string value) => NotifyChanged();

    partial void OnBooleanValueChanged(bool value) => NotifyChanged();

    partial void OnSelectedReferenceChanged(ContentReference? value) => NotifyChanged();

    partial void OnSelectedOptionChanged(string? value) => NotifyChanged();

    private void NotifyChanged()
    {
        if (!_isUpdating)
        {
            _changed();
        }
    }
}

/// <summary>
/// Раздел формы редактирования, объединяющий поля одной группы.
/// </summary>
public sealed class ContentFieldGroupViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт раздел формы.
    /// </summary>
    /// <param name="title">Название раздела.</param>
    /// <param name="fields">Поля раздела.</param>
    public ContentFieldGroupViewModel(string title, IEnumerable<ContentFieldViewModel> fields)
    {
        Title = title;
        Fields = new ObservableCollection<ContentFieldViewModel>(fields);
    }

    /// <summary>Название раздела.</summary>
    public string Title { get; }

    /// <summary>Поля раздела.</summary>
    public ObservableCollection<ContentFieldViewModel> Fields { get; }
}
