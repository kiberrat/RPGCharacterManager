using System.Globalization;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Characters;

/// <summary>
/// Перенос результата расчёта в персонажа.
///
/// Используется и при создании персонажа, и при его пересчёте, поэтому правило
/// «вычисленное значение записывается в запись персонажа» описано ровно один раз.
/// </summary>
internal static class CharacterWriter
{
    /// <summary>
    /// Записывает вычисленные значения в персонажа и описывает изменения.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="calculation">Результат расчёта.</param>
    /// <param name="added">
    /// Список созданных записей. Вызывающий обязан передать их контексту базы данных:
    /// запись, добавленную в список отслеживаемого персонажа, Entity Framework Core
    /// иначе считает изменённой, а не новой, — её первичный ключ задан в коде.
    /// </param>
    /// <returns>Описания изменившихся значений для отчёта и журнала.</returns>
    public static IReadOnlyList<string> ApplyCalculation(
        Character character,
        CharacterCalculation calculation,
        ICollection<object>? added = null)
    {
        var changes = new List<string>();

        ApplyAttributes(character, calculation, changes, added);
        ApplyResources(character, calculation, changes, added);
        ApplySkills(character, calculation);

        return changes;
    }

    /// <summary>
    /// Записывает значения характеристик, добавляя недостающие записи.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="calculation">Результат расчёта.</param>
    /// <param name="changes">Список описаний изменений.</param>
    /// <param name="added">Список созданных записей.</param>
    private static void ApplyAttributes(
        Character character,
        CharacterCalculation calculation,
        List<string> changes,
        ICollection<object>? added)
    {
        var stored = character.Attributes.ToDictionary(value => value.AttributeId);

        foreach (var attribute in calculation.Attributes)
        {
            if (!stored.TryGetValue(attribute.Id, out var value))
            {
                var created = new CharacterAttributeValue
                {
                    CharacterId = character.Id,
                    AttributeId = attribute.Id,
                    BaseValue = attribute.BaseValue,
                    CurrentValue = attribute.Value,
                    Modifier = attribute.Modifier,
                };

                character.Attributes.Add(created);
                added?.Add(created);

                continue;
            }

            if (!AreEqual(value.CurrentValue, attribute.Value))
            {
                changes.Add(
                    $"{attribute.Name}: {Format(value.CurrentValue)} → {Format(attribute.Value)}");
            }

            value.BaseValue = attribute.BaseValue;
            value.CurrentValue = attribute.Value;
            value.Modifier = attribute.Modifier;
        }
    }

    /// <summary>
    /// Записывает значения ресурсов.
    /// Текущее значение сохраняется, но не может превышать новый максимум.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="calculation">Результат расчёта.</param>
    /// <param name="changes">Список описаний изменений.</param>
    /// <param name="added">Список созданных записей.</param>
    private static void ApplyResources(
        Character character,
        CharacterCalculation calculation,
        List<string> changes,
        ICollection<object>? added)
    {
        var stored = character.Resources.ToDictionary(value => value.ResourceId);

        foreach (var resource in calculation.Resources)
        {
            if (!stored.TryGetValue(resource.Id, out var value))
            {
                var created = new CharacterResource
                {
                    CharacterId = character.Id,
                    ResourceId = resource.Id,
                    Current = resource.Current,
                    Maximum = resource.Maximum,
                };

                character.Resources.Add(created);
                added?.Add(created);

                continue;
            }

            if (!AreEqual(value.Maximum, resource.Maximum))
            {
                changes.Add(
                    $"{resource.Name}, максимум: {Format(value.Maximum)} → {Format(resource.Maximum)}");
            }

            value.Maximum = resource.Maximum;
            value.Current = Math.Min(value.Current, resource.Maximum);
        }
    }

    /// <summary>
    /// Записывает вычисленные значения навыков.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="calculation">Результат расчёта.</param>
    private static void ApplySkills(Character character, CharacterCalculation calculation)
    {
        var calculated = calculation.Skills.ToDictionary(skill => skill.Id);

        foreach (var skill in character.Skills)
        {
            if (calculated.TryGetValue(skill.SkillId, out var value))
            {
                skill.CurrentValue = value.Value;
            }
        }
    }

    private static bool AreEqual(double first, double second) =>
        Math.Abs(first - second) < 0.0001;

    private static string Format(double value) =>
        value.ToString("0.####", CultureInfo.CurrentCulture);
}
