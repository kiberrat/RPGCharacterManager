using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using RPGCharacterManager.Core.Abstractions.Items;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Карточка оружия персонажа.
///
/// Показывает то, что оружие даёт именно этому персонажу: бонус попадания уже
/// вычислен, диапазон урона учитывает характеристику масштабирования и владение,
/// а состояние боеприпасов взято из инвентаря.
/// </summary>
public sealed partial class WeaponCardViewModel : ViewModelBase
{
    /// <summary>Текст, отображаемый вместо незаданной формулы.</summary>
    public const string NotConfigured = "не задана";

    [ObservableProperty]
    private string _reserveText = string.Empty;

    [ObservableProperty]
    private string _lastResult = string.Empty;

    /// <summary>
    /// Создаёт карточку оружия.
    /// </summary>
    /// <param name="weapon">Оружие персонажа с вычисленными значениями.</param>
    public WeaponCardViewModel(CharacterWeapon weapon)
    {
        Weapon = Guard.NotNull(weapon);

        _reserveText = weapon.Ammunition is { } ammunition
            ? ammunition.Reserve.ToString(CultureInfo.CurrentCulture)
            : string.Empty;
    }

    /// <summary>Оружие персонажа.</summary>
    public CharacterWeapon Weapon { get; }

    /// <summary>Идентификатор записи инвентаря.</summary>
    public Guid InventoryItemId => Weapon.InventoryItemId;

    /// <summary>Название оружия.</summary>
    public string Name => Weapon.Name;

    /// <summary>Описание оружия.</summary>
    public string? Description => Weapon.Description;

    /// <summary>Описание задано.</summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(Weapon.Description);

    /// <summary>Категория, тип и дальность оружия.</summary>
    public string Hint => string.Join(" • ", new[]
        {
            Weapon.Category,
            Weapon.WeaponType,
            string.IsNullOrWhiteSpace(Weapon.Range) ? null : $"дальность: {Weapon.Range}",
        }
        .Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>Пояснение задано.</summary>
    public bool HasHint => !string.IsNullOrWhiteSpace(Hint);

    /// <summary>Бросок попадания: кость и вычисленный бонус.</summary>
    public string AttackText
    {
        get
        {
            var bonus = FormatSigned(Weapon.AttackBonus);

            if (string.IsNullOrWhiteSpace(Weapon.AttackDiceFormula))
            {
                return Math.Abs(Weapon.AttackBonus) < double.Epsilon
                    && string.IsNullOrWhiteSpace(Weapon.AttackBonusFormula)
                        ? NotConfigured
                        : bonus;
            }

            return Math.Abs(Weapon.AttackBonus) < double.Epsilon
                ? Weapon.AttackDiceFormula
                : $"{Weapon.AttackDiceFormula} {bonus}";
        }
    }

    /// <summary>Урон: формула и диапазон значений с учётом характеристик персонажа.</summary>
    public string DamageText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Weapon.DamageFormula))
            {
                return NotConfigured;
            }

            if (Weapon.Damage is not { } range)
            {
                return Weapon.DamageFormula;
            }

            return range.IsExact
                ? $"{Weapon.DamageFormula} = {SheetNumber.Format(range.Minimum)}"
                : $"{Weapon.DamageFormula} = "
                    + $"{SheetNumber.Format(range.Minimum)}–{SheetNumber.Format(range.Maximum)}";
        }
    }

    /// <summary>Тип наносимого урона.</summary>
    public string? DamageType => Weapon.DamageType;

    /// <summary>Тип урона задан.</summary>
    public bool HasDamageType => !string.IsNullOrWhiteSpace(Weapon.DamageType);

    /// <summary>Условие критического попадания и формула критического урона.</summary>
    public string CriticalText
    {
        get
        {
            if (Weapon.CriticalThreshold is not { } threshold)
            {
                return string.Empty;
            }

            var text = $"критическое попадание при {threshold.ToString(CultureInfo.CurrentCulture)} и выше";

            return string.IsNullOrWhiteSpace(Weapon.CriticalFormula)
                ? text
                : $"{text}, урон: {Weapon.CriticalFormula}";
        }
    }

    /// <summary>Оружие наносит критические попадания.</summary>
    public bool HasCritical => Weapon.HasCritical;

    /// <summary>Характеристика масштабирования и навык владения.</summary>
    public string ScalingText => string.Join(" • ", new[]
        {
            string.IsNullOrWhiteSpace(Weapon.ScalingAttributeName)
                ? null
                : $"масштабирование: {Weapon.ScalingAttributeName}",
            string.IsNullOrWhiteSpace(Weapon.ProficiencySkillName)
                ? null
                : $"владение: {Weapon.ProficiencySkillName} "
                    + $"({Weapon.ProficiencyLevel.ToString(CultureInfo.CurrentCulture)})",
        }
        .Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>Масштабирование или владение заданы.</summary>
    public bool HasScaling => !string.IsNullOrWhiteSpace(ScalingText);

    /// <summary>Свойства оружия.</summary>
    public string PropertiesText => string.Join(", ", Weapon.Properties);

    /// <summary>Свойства заданы.</summary>
    public bool HasProperties => Weapon.Properties.Count > 0;

    /// <summary>Оружие расходует боеприпасы.</summary>
    public bool UsesAmmunition => Weapon.UsesAmmunition;

    /// <summary>Состояние боеприпасов.</summary>
    public string AmmunitionText
    {
        get
        {
            if (Weapon.Ammunition is not { } ammunition)
            {
                return string.Empty;
            }

            var perShot = $"расход {ammunition.PerShot.ToString(CultureInfo.CurrentCulture)} за атаку";

            return ammunition is { HasMagazine: true, MagazineSize: { } size }
                ? $"{ammunition.Name}: в магазине "
                    + $"{(ammunition.Loaded ?? 0).ToString(CultureInfo.CurrentCulture)} "
                    + $"из {size.ToString(CultureInfo.CurrentCulture)}, {perShot}"
                : $"{ammunition.Name}: {perShot}";
        }
    }

    /// <summary>Время перезарядки по правилам игровой системы.</summary>
    public string? ReloadTime => Weapon.ReloadTime;

    /// <summary>Оружие использует магазин и может быть перезаряжено.</summary>
    public bool HasMagazine => Weapon.Ammunition is { HasMagazine: true };

    /// <summary>Требования оружия выполнены.</summary>
    public bool IsAvailable => Weapon.IsAvailable;

    /// <summary>Причина, по которой персонаж не может применить оружие.</summary>
    public string? UnavailableReason => Weapon.UnavailableReason;

    /// <summary>Требования оружия нарушены.</summary>
    public bool HasUnavailableReason => !string.IsNullOrWhiteSpace(Weapon.UnavailableReason);

    /// <summary>Замечания расчёта формул оружия.</summary>
    public string IssuesText => string.Join(" ", Weapon.Issues);

    /// <summary>Найдены замечания расчёта.</summary>
    public bool HasIssues => Weapon.Issues.Count > 0;

    /// <summary>Атака возможна: требования выполнены и боеприпасов хватает.</summary>
    public bool CanAttack => Weapon.IsAvailable && Weapon.Ammunition is not { IsReady: false };

    /// <summary>Результат последнего действия задан.</summary>
    public bool HasLastResult => !string.IsNullOrWhiteSpace(LastResult);

    /// <summary>
    /// Возвращает введённый пользователем запас боеприпасов.
    /// </summary>
    /// <returns>Количество боеприпасов либо <see langword="null"/>, если введено не число.</returns>
    public int? GetReserve() =>
        int.TryParse(ReserveText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value)
            && value >= 0
                ? value
                : null;

    private static string FormatSigned(double value) =>
        value < 0 ? SheetNumber.Format(value) : $"+{SheetNumber.Format(value)}";

    partial void OnLastResultChanged(string value) => OnPropertyChanged(nameof(HasLastResult));
}
