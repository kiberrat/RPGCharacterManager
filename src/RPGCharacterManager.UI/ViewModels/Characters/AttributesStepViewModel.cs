using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Способ распределения характеристик в списке выбора.
/// </summary>
/// <param name="Method">Способ распределения.</param>
/// <param name="DisplayName">Отображаемое название способа.</param>
/// <param name="Description">Пояснение к способу.</param>
public sealed record AttributeMethodOption(
    AttributeAssignmentMethod Method,
    string DisplayName,
    string Description);

/// <summary>
/// Страница распределения характеристик.
///
/// Приложение не содержит правил конкретной игры, поэтому бюджет очков, набор
/// значений и формула броска задаются здесь же и сохраняются вместе с решением
/// пользователя. Все вычисления выполняет единый движок формул.
/// </summary>
public sealed partial class AttributesStepViewModel : WizardStepViewModel
{
    private readonly IFormulaEngine _formulas;
    private readonly AttributeStepOptions _options;

    private bool _isLoading;

    [ObservableProperty]
    private AttributeMethodOption? _selectedMethod;

    [ObservableProperty]
    private string _pointBudgetText = string.Empty;

    [ObservableProperty]
    private string _standardArrayText = string.Empty;

    [ObservableProperty]
    private string _rollFormula = string.Empty;

    [ObservableProperty]
    private string _methodSummary = string.Empty;

    [ObservableProperty]
    private string? _methodError;

    /// <summary>
    /// Создаёт страницу распределения характеристик.
    /// </summary>
    /// <param name="definition">Описание шага.</param>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="builder">Мастер создания персонажа.</param>
    /// <param name="formulas">Единый движок вычислений.</param>
    /// <param name="changed">Обратный вызов при изменении данных персонажа.</param>
    public AttributesStepViewModel(
        CharacterStepDefinition definition,
        CharacterDraft draft,
        ICharacterBuilderService builder,
        IFormulaEngine formulas,
        Action changed)
        : base(definition, draft, builder, changed)
    {
        _formulas = Guard.NotNull(formulas);
        _options = definition.AttributeOptions ?? new AttributeStepOptions();

        foreach (var method in _options.Methods)
        {
            Methods.Add(CreateMethodOption(method));
        }

        _pointBudgetText = _options.PointBudget.ToString(CultureInfo.CurrentCulture);
        _standardArrayText = _options.StandardArray;
        _rollFormula = _options.RollFormula;
        _selectedMethod = Methods.FirstOrDefault(option => option.Method == draft.AttributeMethod)
            ?? Methods.FirstOrDefault();
    }

    /// <summary>Доступные способы распределения.</summary>
    public ObservableCollection<AttributeMethodOption> Methods { get; } = [];

    /// <summary>Характеристики персонажа.</summary>
    public ObservableCollection<AttributeAssignmentViewModel> Attributes { get; } = [];

    /// <summary>Характеристики отсутствуют.</summary>
    public bool IsEmpty => Attributes.Count == 0;

    /// <summary>Выбрана покупка значений за очки.</summary>
    public bool IsPointBuy => SelectedMethod?.Method == AttributeAssignmentMethod.PointBuy;

    /// <summary>Выбрано распределение заданного набора значений.</summary>
    public bool IsStandardArray => SelectedMethod?.Method == AttributeAssignmentMethod.StandardArray;

    /// <summary>Выбран случайный бросок.</summary>
    public bool IsRandomRoll => SelectedMethod?.Method == AttributeAssignmentMethod.RandomRoll;

    /// <summary>Замечание по способу распределения присутствует.</summary>
    public bool HasMethodError => !string.IsNullOrWhiteSpace(MethodError);

    /// <inheritdoc />
    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        _isLoading = true;

        try
        {
            var definitions = await Builder.GetAttributesAsync(Draft, cancellationToken).ConfigureAwait(true);

            Attributes.Clear();

            foreach (var attribute in definitions)
            {
                Attributes.Add(new AttributeAssignmentViewModel(
                    attribute,
                    GetBaseValue(attribute),
                    OnAttributeChanged));
            }

            OnPropertyChanged(nameof(IsEmpty));
            ApplyMethodToRows();
        }
        finally
        {
            _isLoading = false;
            IsBusy = false;
        }

        await RecalculateAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Бросает значения всех характеристик по заданной формуле.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после броска и пересчёта.</returns>
    [RelayCommand]
    private async Task RollAsync(CancellationToken cancellationToken)
    {
        MethodError = null;

        foreach (var attribute in Attributes.Where(item => !item.IsDerived))
        {
            var result = _formulas.Evaluate(RollFormula);

            if (result.IsFailure)
            {
                MethodError = $"Формула броска: {result.Error}";
                OnPropertyChanged(nameof(HasMethodError));

                return;
            }

            attribute.SetValue(result.Value.AsNumber());
            Draft.AttributeBaseValues[attribute.Id] = result.Value.AsNumber();
        }

        OnPropertyChanged(nameof(HasMethodError));

        await RecalculateAsync(cancellationToken).ConfigureAwait(true);
        NotifyChanged();
    }

    /// <summary>
    /// Возвращает всем характеристикам значения по умолчанию.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сброса и пересчёта.</returns>
    [RelayCommand]
    private async Task ResetAsync(CancellationToken cancellationToken)
    {
        foreach (var attribute in Attributes.Where(item => !item.IsDerived))
        {
            attribute.SetValue(attribute.Definition.DefaultValue);
            Draft.AttributeBaseValues[attribute.Id] = attribute.Definition.DefaultValue;
        }

        await RecalculateAsync(cancellationToken).ConfigureAwait(true);
        NotifyChanged();
    }

    partial void OnSelectedMethodChanged(AttributeMethodOption? value)
    {
        Draft.AttributeMethod = value?.Method ?? AttributeAssignmentMethod.Manual;

        OnPropertyChanged(nameof(IsPointBuy));
        OnPropertyChanged(nameof(IsStandardArray));
        OnPropertyChanged(nameof(IsRandomRoll));

        ApplyMethodToRows();
        UpdateMethodSummary();
    }

    partial void OnPointBudgetTextChanged(string value) => UpdateMethodSummary();

    partial void OnStandardArrayTextChanged(string value)
    {
        ApplyMethodToRows();
        UpdateMethodSummary();
    }

    /// <summary>
    /// Обрабатывает изменение значения характеристики пользователем.
    /// </summary>
    /// <param name="attribute">Изменённая характеристика.</param>
    private void OnAttributeChanged(AttributeAssignmentViewModel attribute)
    {
        if (_isLoading)
        {
            return;
        }

        Draft.AttributeBaseValues[attribute.Id] = attribute.BaseValue;

        UpdateMethodSummary();
        NotifyChanged();

        _ = RecalculateAsync(CancellationToken.None);
    }

    /// <summary>
    /// Пересчитывает итоговые значения и модификаторы характеристик.
    /// Пересчёт выполняется после каждого изменения, как требует документ 006.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после пересчёта.</returns>
    private async Task RecalculateAsync(CancellationToken cancellationToken)
    {
        var calculation = await Builder.CalculateAsync(Draft, cancellationToken).ConfigureAwait(true);

        foreach (var calculated in calculation.Attributes)
        {
            var row = Attributes.FirstOrDefault(item => item.Id == calculated.Id);

            row?.UpdateCalculated(calculated.Value, calculated.Modifier);
        }

        UpdateMethodSummary();
    }

    /// <summary>
    /// Настраивает строки характеристик под выбранный способ распределения.
    /// </summary>
    private void ApplyMethodToRows()
    {
        var values = ParseStandardArray();

        foreach (var attribute in Attributes)
        {
            attribute.ApplyMethod(SelectedMethod?.Method ?? AttributeAssignmentMethod.Manual, values);
        }
    }

    /// <summary>
    /// Обновляет пояснение к выбранному способу распределения и сообщение об ошибке.
    /// </summary>
    private void UpdateMethodSummary()
    {
        MethodError = null;

        switch (SelectedMethod?.Method)
        {
            case AttributeAssignmentMethod.PointBuy:
                UpdatePointBuySummary();
                break;

            case AttributeAssignmentMethod.StandardArray:
                UpdateStandardArraySummary();
                break;

            default:
                MethodSummary = string.Empty;
                break;
        }

        OnPropertyChanged(nameof(HasMethodError));
    }

    private void UpdatePointBuySummary()
    {
        var budget = ParseNumber(PointBudgetText) ?? 0;
        var spent = Attributes
            .Where(attribute => !attribute.IsDerived)
            .Sum(attribute => Math.Max(0, attribute.BaseValue - attribute.Minimum));

        MethodSummary = string.Create(
            CultureInfo.CurrentCulture,
            $"Потрачено {Format(spent)} из {Format(budget)}");

        if (spent > budget)
        {
            MethodError = "Потрачено больше очков, чем позволяет бюджет. "
                + "Уменьшите значения либо увеличьте бюджет, если ваша игровая система его допускает.";
        }
    }

    private void UpdateStandardArraySummary()
    {
        var values = ParseStandardArray();

        if (values.Count == 0)
        {
            MethodSummary = string.Empty;
            MethodError = "Укажите набор значений, принятый в вашей игровой системе, "
                + "например: 15, 14, 13, 12, 10, 8.";

            return;
        }

        var assigned = Attributes
            .Where(attribute => !attribute.IsDerived && attribute.SelectedArrayValue is not null)
            .ToList();

        MethodSummary = string.Create(
            CultureInfo.CurrentCulture,
            $"Назначено значений: {assigned.Count} из {values.Count}");

        var duplicate = assigned
            .GroupBy(attribute => attribute.SelectedArrayValue!.Value)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();

        if (Math.Abs(duplicate) > double.Epsilon)
        {
            MethodError = $"Значение {Format(duplicate)} назначено нескольким характеристикам.";
        }
    }

    private List<double> ParseStandardArray() => StandardArrayText
        .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(ParseNumber)
        .Where(value => value is not null)
        .Select(value => value!.Value)
        .ToList();

    private double GetBaseValue(AttributeDefinition attribute) =>
        Draft.AttributeBaseValues.TryGetValue(attribute.Id, out var value)
            ? value
            : attribute.DefaultValue;

    private static AttributeMethodOption CreateMethodOption(AttributeAssignmentMethod method) => method switch
    {
        AttributeAssignmentMethod.PointBuy => new AttributeMethodOption(
            method,
            "Покупка очков",
            "Значения повышаются за очки. Стоимость повышения на единицу — одно очко."),

        AttributeAssignmentMethod.StandardArray => new AttributeMethodOption(
            method,
            "Стандартный набор",
            "Заданный набор значений распределяется между характеристиками."),

        AttributeAssignmentMethod.RandomRoll => new AttributeMethodOption(
            method,
            "Случайный бросок",
            "Значение каждой характеристики определяется броском кубиков."),

        _ => new AttributeMethodOption(
            AttributeAssignmentMethod.Manual,
            "Ручной ввод",
            "Значения вводятся вручную с учётом границ, заданных характеристикой."),
    };

    private static double? ParseNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // Пользователь может ввести дробную часть как через запятую, так и через точку.
        var normalized = text.Trim().Replace(',', '.');

        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string Format(double value) => value.ToString("0.####", CultureInfo.CurrentCulture);
}

/// <summary>
/// Строка характеристики на странице распределения.
/// </summary>
public sealed partial class AttributeAssignmentViewModel : ViewModelBase
{
    private readonly Action<AttributeAssignmentViewModel> _changed;

    private bool _isUpdating;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private double? _selectedArrayValue;

    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private double _modifier;

    [ObservableProperty]
    private bool _isArrayMode;

    /// <summary>
    /// Создаёт строку характеристики.
    /// </summary>
    /// <param name="definition">Описание характеристики.</param>
    /// <param name="baseValue">Базовое значение.</param>
    /// <param name="changed">Обратный вызов при изменении значения.</param>
    public AttributeAssignmentViewModel(
        AttributeDefinition definition,
        double baseValue,
        Action<AttributeAssignmentViewModel> changed)
    {
        Definition = Guard.NotNull(definition);
        _changed = Guard.NotNull(changed);

        BaseValue = baseValue;
        _text = Format(baseValue);
        _value = baseValue;
    }

    /// <summary>Описание характеристики.</summary>
    public AttributeDefinition Definition { get; }

    /// <summary>Идентификатор характеристики.</summary>
    public Guid Id => Definition.Id;

    /// <summary>Название характеристики.</summary>
    public string Name => Definition.Name;

    /// <summary>Внутреннее имя характеристики, используемое формулами.</summary>
    public string SystemName => Definition.SystemName;

    /// <summary>Базовое значение, заданное пользователем.</summary>
    public double BaseValue { get; private set; }

    /// <summary>Значение вычисляется формулой и не редактируется вручную.</summary>
    public bool IsDerived => !string.IsNullOrWhiteSpace(Definition.Formula);

    /// <summary>Значение доступно для ввода.</summary>
    public bool IsEditable => !IsDerived && !IsArrayMode;

    /// <summary>Наименьшее допустимое значение.</summary>
    public double Minimum => Definition.MinimumValue ?? Definition.DefaultValue;

    /// <summary>Пояснение к допустимым значениям.</summary>
    public string RangeHint => (Definition.MinimumValue, Definition.MaximumValue) switch
    {
        (null, null) => Definition.SystemName,
        ({ } minimum, null) => $"{Definition.SystemName}, не меньше {Format(minimum)}",
        (null, { } maximum) => $"{Definition.SystemName}, не больше {Format(maximum)}",
        ({ } minimum, { } maximum) => $"{Definition.SystemName}, от {Format(minimum)} до {Format(maximum)}",
    };

    /// <summary>Значения, доступные для назначения из стандартного набора.</summary>
    public ObservableCollection<double> ArrayValues { get; } = [];

    /// <summary>
    /// Задаёт значение характеристики извне: при броске или сбросе значений.
    /// </summary>
    /// <param name="value">Новое базовое значение.</param>
    public void SetValue(double value)
    {
        _isUpdating = true;

        try
        {
            BaseValue = value;
            Text = Format(value);
            SelectedArrayValue = ArrayValues.Contains(value) ? value : null;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>
    /// Записывает вычисленные итоговое значение и модификатор.
    /// </summary>
    /// <param name="value">Итоговое значение.</param>
    /// <param name="modifier">Модификатор характеристики.</param>
    public void UpdateCalculated(double value, double modifier)
    {
        Value = value;
        Modifier = modifier;
    }

    /// <summary>
    /// Настраивает строку под выбранный способ распределения.
    /// </summary>
    /// <param name="method">Способ распределения.</param>
    /// <param name="values">Значения стандартного набора.</param>
    public void ApplyMethod(AttributeAssignmentMethod method, IReadOnlyList<double> values)
    {
        _isUpdating = true;

        try
        {
            ArrayValues.Clear();

            foreach (var value in values)
            {
                ArrayValues.Add(value);
            }

            IsArrayMode = method == AttributeAssignmentMethod.StandardArray && !IsDerived;

            if (!IsArrayMode)
            {
                SelectedArrayValue = null;
            }

            OnPropertyChanged(nameof(IsEditable));
        }
        finally
        {
            _isUpdating = false;
        }
    }

    partial void OnTextChanged(string value)
    {
        if (_isUpdating)
        {
            return;
        }

        var parsed = Parse(value);

        if (parsed is null)
        {
            return;
        }

        BaseValue = parsed.Value;
        _changed(this);
    }

    partial void OnSelectedArrayValueChanged(double? value)
    {
        if (_isUpdating || value is null)
        {
            return;
        }

        BaseValue = value.Value;

        _isUpdating = true;

        try
        {
            Text = Format(value.Value);
        }
        finally
        {
            _isUpdating = false;
        }

        _changed(this);
    }

    private static double? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = text.Trim().Replace(',', '.');

        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string Format(double value) => value.ToString("0.####", CultureInfo.CurrentCulture);
}
