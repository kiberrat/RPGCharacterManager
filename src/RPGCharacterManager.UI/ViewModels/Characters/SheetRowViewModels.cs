using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Разбор и форматирование чисел листа персонажа.
/// Пользователь может вводить дробную часть и через запятую, и через точку.
/// </summary>
internal static class SheetNumber
{
    /// <summary>
    /// Преобразует число в текст для поля ввода.
    /// </summary>
    /// <param name="value">Число.</param>
    /// <returns>Текстовое представление.</returns>
    public static string Format(double value) => value.ToString("0.####", CultureInfo.CurrentCulture);

    /// <summary>
    /// Разбирает введённое пользователем число.
    /// </summary>
    /// <param name="text">Введённый текст.</param>
    /// <returns>Число либо <see langword="null"/>, если текст не является числом.</returns>
    public static double? Parse(string? text)
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
}

/// <summary>Редактируемая строка денег персонажа.</summary>
public sealed partial class CharacterCurrencyRowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private decimal _amount;

    /// <summary>Создаёт строку из сохранённой валюты.</summary>
    public CharacterCurrencyRowViewModel(CharacterCurrency currency)
    {
        Id = currency.Id;
        _name = currency.Name;
        _amount = currency.Amount;
    }

    /// <summary>Идентификатор записи.</summary>
    public Guid Id { get; }

    /// <summary>Запись для сохранения текущих значений.</summary>
    public CharacterCurrency ToEntity() => new()
    {
        Id = Id,
        Name = Name,
        Amount = Amount,
    };
}

/// <summary>
/// Раздел листа персонажа, объединяющий строки одной категории.
/// </summary>
/// <typeparam name="TRow">Тип строк раздела.</typeparam>
public abstract class SheetGroupViewModel<TRow> : ViewModelBase
{
    /// <summary>
    /// Создаёт раздел листа.
    /// </summary>
    /// <param name="title">Название раздела.</param>
    /// <param name="rows">Строки раздела.</param>
    protected SheetGroupViewModel(string title, IEnumerable<TRow> rows)
    {
        Title = title;
        Rows = new ObservableCollection<TRow>(rows);
    }

    /// <summary>Название раздела.</summary>
    public string Title { get; }

    /// <summary>Строки раздела.</summary>
    public ObservableCollection<TRow> Rows { get; }
}

/// <summary>Раздел характеристик.</summary>
public sealed class SheetAttributeGroupViewModel : SheetGroupViewModel<SheetAttributeRowViewModel>
{
    /// <summary>
    /// Создаёт раздел характеристик.
    /// </summary>
    /// <param name="title">Название раздела.</param>
    /// <param name="rows">Строки раздела.</param>
    public SheetAttributeGroupViewModel(string title, IEnumerable<SheetAttributeRowViewModel> rows)
        : base(title, rows)
    {
    }
}

/// <summary>Раздел навыков.</summary>
public sealed class SheetSkillGroupViewModel : SheetGroupViewModel<SheetSkillRowViewModel>
{
    /// <summary>
    /// Создаёт раздел навыков.
    /// </summary>
    /// <param name="title">Название раздела.</param>
    /// <param name="rows">Строки раздела.</param>
    public SheetSkillGroupViewModel(string title, IEnumerable<SheetSkillRowViewModel> rows)
        : base(title, rows)
    {
    }
}

/// <summary>Раздел способностей.</summary>
public sealed class SheetAbilityGroupViewModel : SheetGroupViewModel<SheetAbilityRowViewModel>
{
    /// <summary>
    /// Создаёт раздел способностей.
    /// </summary>
    /// <param name="title">Название раздела.</param>
    /// <param name="rows">Строки раздела.</param>
    public SheetAbilityGroupViewModel(string title, IEnumerable<SheetAbilityRowViewModel> rows)
        : base(title, rows)
    {
    }
}

/// <summary>
/// Строка характеристики на листе персонажа.
/// Редактируется только базовое значение: итог и модификатор вычисляются.
/// </summary>
public sealed partial class SheetAttributeRowViewModel : ViewModelBase
{
    private readonly CharacterAttributeValue _value;
    private readonly Action _changed;

    [ObservableProperty]
    private string _baseText = string.Empty;

    /// <summary>
    /// Создаёт строку характеристики.
    /// </summary>
    /// <param name="attribute">Вычисленная характеристика.</param>
    /// <param name="value">Запись значения характеристики персонажа.</param>
    /// <param name="changed">Обратный вызов при изменении значения.</param>
    public SheetAttributeRowViewModel(
        SheetAttributeValue attribute,
        CharacterAttributeValue value,
        Action changed)
    {
        Attribute = Guard.NotNull(attribute);
        _value = Guard.NotNull(value);
        _changed = Guard.NotNull(changed);

        _baseText = SheetNumber.Format(attribute.BaseValue);
    }

    /// <summary>Вычисленная характеристика.</summary>
    public SheetAttributeValue Attribute { get; }

    /// <summary>Название характеристики.</summary>
    public string Name => Attribute.Name;

    /// <summary>Итоговое значение.</summary>
    public string Value => SheetNumber.Format(Attribute.Value);

    /// <summary>Модификатор характеристики.</summary>
    public string Modifier => SheetNumber.Format(Attribute.Modifier);

    /// <summary>Значение вычисляется формулой.</summary>
    public bool IsDerived => Attribute.IsDerived;

    /// <summary>Базовое значение доступно для ввода.</summary>
    public bool IsEditable => !Attribute.IsDerived;

    /// <summary>Пояснение к характеристике: внутреннее имя, границы и формула.</summary>
    public string Hint => Attribute.IsDerived
        ? $"{Attribute.SystemName} = {Attribute.Formula}"
        : (Attribute.Minimum, Attribute.Maximum) switch
        {
            (null, null) => Attribute.SystemName,
            ({ } minimum, null) => $"{Attribute.SystemName}, не меньше {SheetNumber.Format(minimum)}",
            (null, { } maximum) => $"{Attribute.SystemName}, не больше {SheetNumber.Format(maximum)}",
            ({ } minimum, { } maximum) =>
                $"{Attribute.SystemName}, от {SheetNumber.Format(minimum)} до {SheetNumber.Format(maximum)}",
        };

    partial void OnBaseTextChanged(string value)
    {
        if (SheetNumber.Parse(value) is not { } parsed)
        {
            return;
        }

        _value.BaseValue = parsed;
        _changed();
    }
}

/// <summary>
/// Строка навыка на листе персонажа.
/// </summary>
public sealed partial class SheetSkillRowViewModel : ViewModelBase
{
    private readonly CharacterSkill _skill;
    private readonly Action _changed;

    [ObservableProperty]
    private string _proficiencyText = string.Empty;

    [ObservableProperty]
    private string _bonusText = string.Empty;

    /// <summary>
    /// Создаёт строку навыка.
    /// </summary>
    /// <param name="skill">Вычисленный навык.</param>
    /// <param name="value">Запись владения навыком.</param>
    /// <param name="changed">Обратный вызов при изменении значения.</param>
    public SheetSkillRowViewModel(SheetSkill skill, CharacterSkill value, Action changed)
    {
        Skill = Guard.NotNull(skill);
        _skill = Guard.NotNull(value);
        _changed = Guard.NotNull(changed);

        _proficiencyText = skill.ProficiencyLevel.ToString(CultureInfo.CurrentCulture);
        _bonusText = SheetNumber.Format(skill.Bonus);
    }

    /// <summary>Вычисленный навык.</summary>
    public SheetSkill Skill { get; }

    /// <summary>Идентификатор навыка.</summary>
    public Guid Id => Skill.Id;

    /// <summary>Название навыка.</summary>
    public string Name => Skill.Name;

    /// <summary>Итоговое значение навыка.</summary>
    public string Value => SheetNumber.Format(Skill.Value);

    /// <summary>Пояснение: связанная характеристика, формула и предельный уровень владения.</summary>
    public string Hint
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Skill.LinkedAttributeName))
            {
                parts.Add(Skill.LinkedAttributeName);
            }

            if (!string.IsNullOrWhiteSpace(Skill.Formula))
            {
                parts.Add(Skill.Formula);
            }

            if (Skill.MaximumLevel is { } maximum && maximum > 0)
            {
                parts.Add($"владение не выше {maximum.ToString(CultureInfo.CurrentCulture)}");
            }

            return string.Join(", ", parts);
        }
    }

    /// <summary>Пояснение задано.</summary>
    public bool HasHint => !string.IsNullOrWhiteSpace(Hint);

    partial void OnProficiencyTextChanged(string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var level) && level >= 0)
        {
            _skill.ProficiencyLevel = level;
            _changed();
        }
    }

    partial void OnBonusTextChanged(string value)
    {
        if (SheetNumber.Parse(value) is { } bonus)
        {
            _skill.Bonus = bonus;
            _changed();
        }
    }
}

/// <summary>
/// Строка ресурса на листе персонажа.
/// Максимум вычисляется формулой, текущее значение задаёт игрок.
/// </summary>
public sealed partial class SheetResourceRowViewModel : ViewModelBase
{
    private readonly CharacterResource _resource;
    private readonly Action _changed;

    [ObservableProperty]
    private string _currentText = string.Empty;

    /// <summary>
    /// Создаёт строку ресурса.
    /// </summary>
    /// <param name="resource">Вычисленный ресурс.</param>
    /// <param name="value">Запись состояния ресурса персонажа.</param>
    /// <param name="changed">Обратный вызов при изменении значения.</param>
    public SheetResourceRowViewModel(SheetResource resource, CharacterResource value, Action changed)
    {
        Resource = Guard.NotNull(resource);
        _resource = Guard.NotNull(value);
        _changed = Guard.NotNull(changed);

        _currentText = SheetNumber.Format(resource.Current);
    }

    /// <summary>Вычисленный ресурс.</summary>
    public SheetResource Resource { get; }

    /// <summary>Название ресурса.</summary>
    public string Name => Resource.Name;

    /// <summary>Максимальное значение.</summary>
    public string Maximum => SheetNumber.Format(Resource.Maximum);

    /// <summary>Правило восстановления ресурса.</summary>
    public string? RestoreRule => Resource.RestoreRule;

    /// <summary>Правило восстановления задано.</summary>
    public bool HasRestoreRule => !string.IsNullOrWhiteSpace(Resource.RestoreRule);

    partial void OnCurrentTextChanged(string value)
    {
        if (SheetNumber.Parse(value) is { } current)
        {
            _resource.Current = Math.Clamp(current, 0, Resource.Maximum);
            _changed();
        }
    }
}

/// <summary>
/// Строка черты на листе персонажа.
/// </summary>
public sealed partial class SheetTraitRowViewModel : ViewModelBase
{
    private readonly CharacterTrait _trait;
    private readonly Action _changed;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _usesText = string.Empty;

    /// <summary>
    /// Создаёт строку черты.
    /// </summary>
    /// <param name="trait">Полученная черта.</param>
    /// <param name="value">Запись о полученной черте.</param>
    /// <param name="changed">Обратный вызов при изменении значения.</param>
    public SheetTraitRowViewModel(SheetTrait trait, CharacterTrait value, Action changed)
    {
        Trait = Guard.NotNull(trait);
        _trait = Guard.NotNull(value);
        _changed = Guard.NotNull(changed);

        _isActive = trait.IsActive;
        _usesText = trait.RemainingUses.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>Полученная черта.</summary>
    public SheetTrait Trait { get; }

    /// <summary>Идентификатор черты.</summary>
    public Guid TraitId => Trait.TraitId;

    /// <summary>Название черты.</summary>
    public string Name => Trait.Name;

    /// <summary>Описание черты.</summary>
    public string? Description => Trait.Description;

    /// <summary>Описание задано.</summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(Trait.Description);

    /// <summary>Пояснение: раздел, источник получения и формула эффекта.</summary>
    public string Hint => string.Join(
        ", ",
        new[] { Trait.Category, Trait.Source, Trait.Formula }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>Требования черты по-прежнему выполняются.</summary>
    public bool IsAvailable => Trait.IsAvailable;

    /// <summary>Причина, по которой требования черты не выполняются.</summary>
    public string? UnavailableReason => Trait.UnavailableReason;

    /// <summary>Требования черты нарушены.</summary>
    public bool HasUnavailableReason => !string.IsNullOrWhiteSpace(Trait.UnavailableReason);

    partial void OnIsActiveChanged(bool value)
    {
        _trait.IsActive = value;
        _changed();
    }

    partial void OnUsesTextChanged(string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var uses) && uses >= 0)
        {
            _trait.RemainingUses = uses;
            _changed();
        }
    }
}

/// <summary>
/// Строка способности на листе персонажа.
/// Способности не редактируются: персонаж получает их по требованиям.
/// </summary>
public sealed class SheetAbilityRowViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт строку способности.
    /// </summary>
    /// <param name="ability">Доступная способность.</param>
    public SheetAbilityRowViewModel(SheetAbility ability) => Ability = Guard.NotNull(ability);

    /// <summary>Доступная способность.</summary>
    public SheetAbility Ability { get; }

    /// <summary>Название способности.</summary>
    public string Name => Ability.Name;

    /// <summary>Описание способности.</summary>
    public string? Description => Ability.Description;

    /// <summary>Описание задано.</summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(Ability.Description);

    /// <summary>Способность создана пользователем для этого персонажа.</summary>
    public bool IsCustom => Ability.IsCustom;

    /// <summary>Условие способности сейчас выполнено.</summary>
    public bool IsAvailable => Ability.IsAvailable;

    /// <summary>Есть пояснение о невыполненном условии.</summary>
    public bool HasUnavailableReason => !string.IsNullOrWhiteSpace(Ability.UnavailableReason);

    /// <summary>Причина, по которой способность сейчас недоступна.</summary>
    public string? UnavailableReason => Ability.UnavailableReason;

    /// <summary>Понятное описание выбранной зависимости.</summary>
    public string? DependencyDescription => Ability.DependencyDescription;

    /// <summary>У способности задана зависимость.</summary>
    public bool HasDependencyDescription => !string.IsNullOrWhiteSpace(Ability.DependencyDescription);

    /// <summary>Пояснение: формула, расход ресурса, восстановление и условие получения.</summary>
    public string Hint
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Ability.Formula))
            {
                parts.Add(Ability.Formula);
            }

            if (!string.IsNullOrWhiteSpace(Ability.ResourceName))
            {
                parts.Add(string.IsNullOrWhiteSpace(Ability.ResourceCostFormula)
                    ? Ability.ResourceName
                    : $"{Ability.ResourceName}: {Ability.ResourceCostFormula}");
            }

            if (!string.IsNullOrWhiteSpace(Ability.RechargeRule))
            {
                parts.Add(Ability.RechargeRule);
            }

            if (!string.IsNullOrWhiteSpace(Ability.Requirements))
            {
                parts.Add($"получена по условию: {Ability.Requirements}");
            }

            return string.Join(" • ", parts);
        }
    }

    /// <summary>Пояснение задано.</summary>
    public bool HasHint => !string.IsNullOrWhiteSpace(Hint);
}

/// <summary>
/// Строка пользовательского поля на листе персонажа.
/// </summary>
public sealed partial class SheetCustomFieldRowViewModel : ViewModelBase
{
    private readonly Action _changed;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _booleanValue;

    /// <summary>
    /// Создаёт строку пользовательского поля.
    /// </summary>
    /// <param name="field">Пользовательское поле.</param>
    /// <param name="changed">Обратный вызов при изменении значения.</param>
    public SheetCustomFieldRowViewModel(SheetCustomField field, Action changed)
    {
        Field = Guard.NotNull(field);
        _changed = Guard.NotNull(changed);

        if (IsBoolean)
        {
            _booleanValue = string.Equals(field.Value, "да", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            _text = field.Value ?? string.Empty;
        }
    }

    /// <summary>Пользовательское поле.</summary>
    public SheetCustomField Field { get; }

    /// <summary>Идентификатор описания свойства.</summary>
    public Guid DefinitionId => Field.DefinitionId;

    /// <summary>Отображаемое название поля.</summary>
    public string DisplayName => Field.DisplayName;

    /// <summary>Пояснение к полю.</summary>
    public string? Description => Field.Description;

    /// <summary>Пояснение задано.</summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(Field.Description);

    /// <summary>Поле является переключателем.</summary>
    public bool IsBoolean => Field.DataType == GameValueType.Boolean;

    /// <summary>Поле вводится многострочным текстом.</summary>
    public bool IsLongText => Field.DataType
        is GameValueType.LongText or GameValueType.Markdown or GameValueType.Json;

    /// <summary>Поле вводится однострочным текстом.</summary>
    public bool IsText => !IsBoolean && !IsLongText;

    /// <summary>
    /// Возвращает текущее значение поля для сохранения.
    /// </summary>
    /// <returns>Значение поля.</returns>
    public string? GetValue() => IsBoolean ? (BooleanValue ? "да" : "нет") : Text;

    partial void OnTextChanged(string value) => _changed();

    partial void OnBooleanValueChanged(bool value) => _changed();
}
