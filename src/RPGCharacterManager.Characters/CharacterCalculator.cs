using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Models.Engine;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.Rules;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Characters;

/// <summary>
/// Автоматический расчёт параметров персонажа.
///
/// Расчёт не содержит ни одной вшитой игровой формулы: значения характеристик,
/// модификаторов, навыков и ресурсов вычисляются выражениями, которые пользователь
/// задал в контенте, а затем изменяются правилами игровой системы.
/// </summary>
public sealed class CharacterCalculator : ICharacterCalculator
{
    private readonly IFormulaEngine _formulas;
    private readonly IRuleEngine _rules;

    /// <summary>
    /// Создаёт службу расчёта параметров персонажа.
    /// </summary>
    /// <param name="formulas">Единый движок вычислений.</param>
    /// <param name="rules">Движок игровых правил.</param>
    public CharacterCalculator(IFormulaEngine formulas, IRuleEngine rules)
    {
        _formulas = Guard.NotNull(formulas);
        _rules = Guard.NotNull(rules);
    }

    /// <inheritdoc />
    public CharacterCalculation Calculate(CharacterCalculationInput input)
    {
        Guard.NotNull(input);

        var issues = new List<CharacterIssue>();
        var appliedRules = new List<string>();

        var target = CreateTarget(input);

        // Порядок обязателен: сначала персонаж без экипировки — по нему вычисляются
        // формулы бонусов, — и лишь затем те же значения с учётом бонусов.
        SeedBaseValues(input, target, CharacterBonusSet.Empty);

        var bonuses = CharacterBonusSet.Create(input, target, _formulas, issues);

        SeedBaseValues(input, target, bonuses);
        bonuses.ApplyTo(target);

        EvaluateDerivedAttributes(input, target, bonuses, issues);
        ApplyRules(input, target, issues, appliedRules);

        var attributes = ReadAttributes(input, target, issues);
        var modifiers = attributes.ToDictionary(attribute => attribute.Id, attribute => attribute.Modifier);

        var skills = CalculateSkills(input, target, modifiers, issues);
        var resources = CalculateResources(input, target, bonuses, issues);

        return new CharacterCalculation(
            attributes,
            skills,
            resources,
            issues,
            appliedRules,
            bonuses.Applied);
    }

    /// <inheritdoc />
    public CharacterEventResult ApplyToBaseValues(
        CharacterCalculationInput input,
        RuleApplication application)
    {
        Guard.NotNull(input);
        Guard.NotNull(application);

        var issues = new List<CharacterIssue>();
        var appliedRules = new List<string>();

        var target = CreateTarget(input);

        // Событие изменяет собственные значения персонажа, поэтому бонусы экипировки
        // в расчёт не входят: иначе снятый предмет унёс бы с собой часть награды.
        SeedBaseValues(input, target, CharacterBonusSet.Empty);

        // Вычисляемые характеристики нужны правилам как источник значений,
        // но сами базовыми не являются и в результат не попадают.
        EvaluateDerivedAttributes(input, target, CharacterBonusSet.Empty, issues);
        ExecuteRules(application, target, issues, appliedRules);

        var baseValues = new Dictionary<Guid, double>();

        foreach (var attribute in input.Attributes.Where(item => !IsDerived(item)))
        {
            baseValues[attribute.Id] = target.TryGetVariable(attribute.SystemName, out var value)
                ? value.AsNumber()
                : GetBaseValue(input, attribute);
        }

        return new CharacterEventResult(baseValues, appliedRules, issues);
    }

    /// <summary>
    /// Создаёт объект правил персонажа: переменные формул и признаки.
    /// </summary>
    /// <param name="input">Исходные данные расчёта.</param>
    /// <returns>Объект, к которому применяются формулы и правила.</returns>
    private static RuleTarget CreateTarget(CharacterCalculationInput input)
    {
        var target = new RuleTarget(
            string.IsNullOrWhiteSpace(input.DisplayName) ? "Персонаж" : input.DisplayName);

        target.WithVariable(CharacterVariables.Level, input.Level);

        foreach (var pair in input.TextVariables)
        {
            target.SetVariable(pair.Key, FormulaValue.FromText(pair.Value));
        }

        foreach (var tag in input.Tags)
        {
            target.AddTag(tag);
        }

        return target;
    }

    /// <summary>
    /// Записывает базовые значения характеристик, не вычисляемых формулой,
    /// вместе с бонусами надетых предметов.
    /// </summary>
    /// <param name="input">Исходные данные расчёта.</param>
    /// <param name="target">Объект расчёта.</param>
    /// <param name="bonuses">Вычисленные бонусы экипировки.</param>
    private static void SeedBaseValues(
        CharacterCalculationInput input,
        RuleTarget target,
        CharacterBonusSet bonuses)
    {
        foreach (var attribute in input.Attributes.Where(attribute => !IsDerived(attribute)))
        {
            target.WithVariable(
                attribute.SystemName,
                GetBaseValue(input, attribute) + bonuses.ForAttribute(attribute.Id));
        }
    }

    /// <summary>
    /// Вычисляет характеристики, заданные формулой, в порядке их зависимостей.
    /// </summary>
    /// <param name="input">Исходные данные расчёта.</param>
    /// <param name="target">Объект расчёта.</param>
    /// <param name="bonuses">Вычисленные бонусы экипировки.</param>
    /// <param name="issues">Список замечаний.</param>
    private void EvaluateDerivedAttributes(
        CharacterCalculationInput input,
        RuleTarget target,
        CharacterBonusSet bonuses,
        List<CharacterIssue> issues)
    {
        var derived = input.Attributes.Where(IsDerived).ToList();

        foreach (var attribute in OrderByDependencies(derived, issues))
        {
            if (input.AttributeOverrides.TryGetValue(attribute.Id, out var overrideValue))
            {
                target.WithVariable(
                    attribute.SystemName,
                    overrideValue + bonuses.ForAttribute(attribute.Id));
                continue;
            }

            var result = _formulas.Evaluate(attribute.Formula!, target);

            if (result.IsFailure)
            {
                issues.Add(new CharacterIssue(
                    CharacterIssueSeverity.Warning,
                    CharacterStepIds.Attributes,
                    $"Характеристика «{attribute.Name}»: {result.Error}"));

                target.WithVariable(attribute.SystemName, 0);
                continue;
            }

            // Бонус прибавляется к значению, вычисленному формулой: для вычисляемой
            // характеристики её формула играет ту же роль, что базовое значение.
            target.WithVariable(
                attribute.SystemName,
                result.Value.AsNumber() + bonuses.ForAttribute(attribute.Id));
        }
    }

    /// <summary>
    /// Упорядочивает вычисляемые характеристики так, чтобы каждая вычислялась
    /// после тех, на которые она ссылается.
    /// </summary>
    /// <param name="derived">Характеристики, заданные формулой.</param>
    /// <param name="issues">Список замечаний.</param>
    /// <returns>Характеристики в порядке вычисления.</returns>
    private List<AttributeDefinition> OrderByDependencies(
        IReadOnlyList<AttributeDefinition> derived,
        List<CharacterIssue> issues)
    {
        var byName = new Dictionary<string, AttributeDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var attribute in derived)
        {
            byName[attribute.SystemName] = attribute;
        }

        var order = new List<AttributeDefinition>(derived.Count);
        var visited = new HashSet<Guid>();
        var visiting = new HashSet<Guid>();

        foreach (var attribute in derived)
        {
            Visit(attribute);
        }

        return order;

        void Visit(AttributeDefinition attribute)
        {
            if (!visited.Add(attribute.Id))
            {
                return;
            }

            visiting.Add(attribute.Id);

            foreach (var dependency in GetDependencies(attribute, byName))
            {
                if (visiting.Contains(dependency.Id))
                {
                    issues.Add(new CharacterIssue(
                        CharacterIssueSeverity.Warning,
                        CharacterStepIds.Attributes,
                        $"Характеристики «{attribute.Name}» и «{dependency.Name}» ссылаются друг на друга. "
                        + "Порядок вычисления выбран произвольно."));

                    continue;
                }

                Visit(dependency);
            }

            visiting.Remove(attribute.Id);
            order.Add(attribute);
        }
    }

    /// <summary>
    /// Возвращает вычисляемые характеристики, от которых зависит формула указанной характеристики.
    /// </summary>
    /// <param name="attribute">Характеристика с формулой.</param>
    /// <param name="byName">Вычисляемые характеристики по внутреннему имени.</param>
    /// <returns>Список зависимостей.</returns>
    private List<AttributeDefinition> GetDependencies(
        AttributeDefinition attribute,
        Dictionary<string, AttributeDefinition> byName)
    {
        var referenced = _formulas.GetReferencedVariables(attribute.Formula!);

        if (referenced.IsFailure)
        {
            return [];
        }

        return referenced.Value
            .Where(name => byName.ContainsKey(name))
            .Select(name => byName[name])
            .Where(dependency => dependency.Id != attribute.Id)
            .ToList();
    }

    /// <summary>
    /// Применяет наборы правил к объекту расчёта в заданном порядке.
    /// </summary>
    /// <param name="input">Исходные данные расчёта.</param>
    /// <param name="target">Объект расчёта.</param>
    /// <param name="issues">Список замечаний.</param>
    /// <param name="appliedRules">Список применённых правил.</param>
    private void ApplyRules(
        CharacterCalculationInput input,
        RuleTarget target,
        List<CharacterIssue> issues,
        List<string> appliedRules)
    {
        foreach (var application in input.RuleSets)
        {
            ExecuteRules(application, target, issues, appliedRules);
        }
    }

    /// <summary>
    /// Выполняет один набор правил и собирает сведения о его применении.
    /// </summary>
    /// <param name="application">Набор правил события.</param>
    /// <param name="target">Объект расчёта.</param>
    /// <param name="issues">Список замечаний.</param>
    /// <param name="appliedRules">Список применённых правил.</param>
    private void ExecuteRules(
        RuleApplication application,
        RuleTarget target,
        List<CharacterIssue> issues,
        List<string> appliedRules)
    {
        if (application.Rules.Count == 0)
        {
            return;
        }

        var report = _rules.Execute(application.Trigger, target, application.Rules);

        appliedRules.AddRange(report.ExecutedRules);

        foreach (var outcome in report.Outcomes.Where(outcome => !outcome.Succeeded))
        {
            issues.Add(new CharacterIssue(
                CharacterIssueSeverity.Warning,
                CharacterStepIds.Summary,
                $"Правило «{outcome.RuleName}»: {outcome.Description}"));
        }
    }

    /// <summary>
    /// Считывает итоговые значения характеристик и вычисляет их модификаторы.
    /// </summary>
    /// <param name="input">Исходные данные расчёта.</param>
    /// <param name="target">Объект расчёта.</param>
    /// <param name="issues">Список замечаний.</param>
    /// <returns>Вычисленные характеристики.</returns>
    private List<CalculatedAttributeValue> ReadAttributes(
        CharacterCalculationInput input,
        RuleTarget target,
        List<CharacterIssue> issues)
    {
        var result = new List<CalculatedAttributeValue>(input.Attributes.Count);

        foreach (var attribute in input.Attributes.OrderBy(item => item.SortOrder))
        {
            var value = target.TryGetVariable(attribute.SystemName, out var stored)
                ? stored.AsNumber()
                : GetBaseValue(input, attribute);

            result.Add(new CalculatedAttributeValue(
                attribute.Id,
                attribute.Name,
                attribute.SystemName,
                GetBaseValue(input, attribute),
                value,
                CalculateModifier(attribute, value, target, issues),
                IsDerived(attribute)));
        }

        return result;
    }

    /// <summary>
    /// Вычисляет модификатор характеристики по её собственной формуле.
    /// Формула получает значение характеристики в переменной «значение».
    /// </summary>
    /// <param name="attribute">Характеристика.</param>
    /// <param name="value">Итоговое значение характеристики.</param>
    /// <param name="target">Объект расчёта.</param>
    /// <param name="issues">Список замечаний.</param>
    /// <returns>Модификатор характеристики.</returns>
    private double CalculateModifier(
        AttributeDefinition attribute,
        double value,
        RuleTarget target,
        List<CharacterIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(attribute.ModifierFormula))
        {
            return 0;
        }

        var context = new LocalFormulaContext(target).With(CharacterVariables.Value, value);
        var result = _formulas.Evaluate(attribute.ModifierFormula, context);

        if (result.IsSuccess)
        {
            return result.Value.AsNumber();
        }

        issues.Add(new CharacterIssue(
            CharacterIssueSeverity.Warning,
            CharacterStepIds.Attributes,
            $"Модификатор характеристики «{attribute.Name}»: {result.Error}"));

        return 0;
    }

    /// <summary>
    /// Вычисляет значения навыков персонажа.
    /// </summary>
    /// <param name="input">Исходные данные расчёта.</param>
    /// <param name="target">Объект расчёта.</param>
    /// <param name="modifiers">Модификаторы характеристик по идентификатору.</param>
    /// <param name="issues">Список замечаний.</param>
    /// <returns>Вычисленные навыки.</returns>
    private List<CalculatedSkill> CalculateSkills(
        CharacterCalculationInput input,
        RuleTarget target,
        Dictionary<Guid, double> modifiers,
        List<CharacterIssue> issues)
    {
        var result = new List<CalculatedSkill>(input.Skills.Count);

        foreach (var skill in input.Skills.OrderBy(item => item.SortOrder))
        {
            var proficiency = input.SkillProficiencies.TryGetValue(skill.Id, out var level) ? level : 0;

            var linkedModifier = skill.LinkedAttributeId is { } attributeId
                && modifiers.TryGetValue(attributeId, out var modifier)
                    ? modifier
                    : 0;

            result.Add(new CalculatedSkill(
                skill.Id,
                skill.Name,
                EvaluateSkillValue(skill, proficiency, linkedModifier, target, issues),
                proficiency));
        }

        return result;
    }

    /// <summary>
    /// Вычисляет значение одного навыка.
    /// Формула навыка получает переменные «владение» и «характеристика»; при её
    /// отсутствии значением навыка становится модификатор связанной характеристики.
    /// </summary>
    /// <param name="skill">Навык.</param>
    /// <param name="proficiency">Уровень владения навыком.</param>
    /// <param name="linkedModifier">Модификатор связанной характеристики.</param>
    /// <param name="target">Объект расчёта.</param>
    /// <param name="issues">Список замечаний.</param>
    /// <returns>Значение навыка.</returns>
    private double EvaluateSkillValue(
        Skill skill,
        int proficiency,
        double linkedModifier,
        RuleTarget target,
        List<CharacterIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(skill.Formula))
        {
            return linkedModifier;
        }

        var context = new LocalFormulaContext(target)
            .With(CharacterVariables.Proficiency, proficiency)
            .With(CharacterVariables.LinkedModifier, linkedModifier);

        var result = _formulas.Evaluate(skill.Formula, context);

        if (result.IsSuccess)
        {
            return result.Value.AsNumber();
        }

        issues.Add(new CharacterIssue(
            CharacterIssueSeverity.Warning,
            CharacterStepIds.Skills,
            $"Навык «{skill.Name}»: {result.Error}"));

        return linkedModifier;
    }

    /// <summary>
    /// Вычисляет начальные и максимальные значения ресурсов персонажа.
    /// </summary>
    /// <param name="input">Исходные данные расчёта.</param>
    /// <param name="target">Объект расчёта.</param>
    /// <param name="bonuses">Вычисленные бонусы экипировки.</param>
    /// <param name="issues">Список замечаний.</param>
    /// <returns>Вычисленные ресурсы.</returns>
    private List<CalculatedResource> CalculateResources(
        CharacterCalculationInput input,
        RuleTarget target,
        CharacterBonusSet bonuses,
        List<CharacterIssue> issues)
    {
        var result = new List<CalculatedResource>(input.Resources.Count);

        foreach (var resource in input.Resources.OrderBy(item => item.SortOrder))
        {
            var maximum = EvaluateResourceFormula(
                resource.MaximumFormula,
                resource.Name,
                "максимум",
                target,
                issues) + bonuses.ForResource(resource.Id);

            var current = string.IsNullOrWhiteSpace(resource.StartingFormula)
                ? maximum
                : EvaluateResourceFormula(
                    resource.StartingFormula,
                    resource.Name,
                    "начальное значение",
                    target,
                    issues);

            result.Add(new CalculatedResource(resource.Id, resource.Name, current, maximum));
        }

        return result;
    }

    /// <summary>
    /// Вычисляет одну формулу ресурса.
    /// </summary>
    /// <param name="formula">Текст формулы.</param>
    /// <param name="resourceName">Название ресурса.</param>
    /// <param name="description">Назначение формулы для сообщения об ошибке.</param>
    /// <param name="target">Объект расчёта.</param>
    /// <param name="issues">Список замечаний.</param>
    /// <returns>Вычисленное значение либо ноль при ошибке.</returns>
    private double EvaluateResourceFormula(
        string? formula,
        string resourceName,
        string description,
        RuleTarget target,
        List<CharacterIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return 0;
        }

        var result = _formulas.Evaluate(formula, target);

        if (result.IsSuccess)
        {
            return result.Value.AsNumber();
        }

        issues.Add(new CharacterIssue(
            CharacterIssueSeverity.Warning,
            CharacterStepIds.Summary,
            $"Ресурс «{resourceName}», {description}: {result.Error}"));

        return 0;
    }

    private static bool IsDerived(AttributeDefinition attribute) =>
        !string.IsNullOrWhiteSpace(attribute.Formula);

    private static double GetBaseValue(CharacterCalculationInput input, AttributeDefinition attribute) =>
        input.BaseValues.TryGetValue(attribute.Id, out var value) ? value : attribute.DefaultValue;
}
