using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Items;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Models.Engine;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Items;

/// <summary>
/// Значения, от которых масштабируются формулы оружия.
///
/// Документ 012_Оружие.md называет масштабирование ключевой возможностью подсистемы:
/// пользователь выбирает характеристику и навык, а формула ссылается на них общими
/// именами. Благодаря этому одна формула «кость + характеристика + владение»
/// подходит и мечу, и винтовке, и жезлу.
/// </summary>
/// <param name="Value">Значение характеристики масштабирования.</param>
/// <param name="Modifier">Модификатор характеристики масштабирования.</param>
/// <param name="ProficiencyLevel">Уровень владения навыком оружия.</param>
/// <param name="SkillValue">Итоговое значение навыка оружия.</param>
/// <param name="AttributeName">Название характеристики масштабирования.</param>
/// <param name="SkillName">Название навыка владения оружием.</param>
internal sealed record WeaponScaling(
    double Value,
    double Modifier,
    int ProficiencyLevel,
    double SkillValue,
    string? AttributeName,
    string? SkillName)
{
    /// <summary>Масштабирование не задано.</summary>
    public static WeaponScaling Empty { get; } = new(0, 0, 0, 0, null, null);

    /// <summary>
    /// Определяет значения масштабирования оружия по результату расчёта персонажа.
    /// </summary>
    /// <param name="weapon">Оружие.</param>
    /// <param name="calculation">Результат расчёта параметров персонажа.</param>
    /// <returns>Значения масштабирования.</returns>
    public static WeaponScaling Create(Weapon weapon, CharacterCalculation calculation)
    {
        var attribute = weapon.ScalingAttributeId is { } attributeId
            ? calculation.Attributes.FirstOrDefault(item => item.Id == attributeId)
            : null;

        // Навык отсутствует в расчёте, если персонаж им не владеет: тогда уровень
        // владения оружием равен нулю, а формула сама решает, что это означает.
        var skill = weapon.ProficiencySkillId is { } skillId
            ? calculation.Skills.FirstOrDefault(item => item.Id == skillId)
            : null;

        return new WeaponScaling(
            attribute?.Value ?? 0,
            attribute?.Modifier ?? 0,
            skill?.ProficiencyLevel ?? 0,
            skill?.Value ?? 0,
            attribute?.Name ?? weapon.ScalingAttribute?.Name,
            skill?.Name ?? weapon.ProficiencySkill?.Name);
    }

    /// <summary>
    /// Создаёт источник значений переменных для формул оружия.
    /// </summary>
    /// <param name="character">Источник значений переменных персонажа.</param>
    /// <returns>Источник значений с добавленными переменными оружия.</returns>
    public IFormulaContext CreateContext(IFormulaContext character) =>
        new LocalFormulaContext(character)
            .With(WeaponVariables.ScalingValue, Value)
            .With(WeaponVariables.ScalingModifier, Modifier)
            .With(WeaponVariables.Proficiency, ProficiencyLevel)
            .With(WeaponVariables.SkillValue, SkillValue);

    /// <summary>
    /// Записывает переменные оружия в объект правил, чтобы условия правил боя
    /// могли обращаться к ним так же, как формулы оружия.
    /// </summary>
    /// <param name="target">Объект правил персонажа.</param>
    public void ApplyTo(IRuleTarget target)
    {
        target.SetVariable(WeaponVariables.ScalingValue, FormulaValue.FromNumber(Value));
        target.SetVariable(WeaponVariables.ScalingModifier, FormulaValue.FromNumber(Modifier));
        target.SetVariable(WeaponVariables.Proficiency, FormulaValue.FromNumber(ProficiencyLevel));
        target.SetVariable(WeaponVariables.SkillValue, FormulaValue.FromNumber(SkillValue));
    }
}
