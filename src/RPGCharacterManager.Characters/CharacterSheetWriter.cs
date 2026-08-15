using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Characters;

/// <summary>
/// Перенос изменений листа персонажа на сохранённую запись.
///
/// Лист владеет персонажем целиком, поэтому списки навыков и черт согласуются
/// полностью: удалённые на листе записи удаляются, добавленные — добавляются.
/// Переносятся только исходные значения; производные вычисляются пересчётом.
/// </summary>
internal static class CharacterSheetWriter
{
    /// <summary>
    /// Переносит изменения листа на сохранённого персонажа.
    /// </summary>
    /// <param name="source">Персонаж, изменённый на листе.</param>
    /// <param name="target">Сохранённая запись персонажа.</param>
    /// <param name="added">
    /// Список созданных записей. Вызывающий обязан передать их контексту базы данных:
    /// первичные ключи задаются в коде, поэтому запись, добавленную в список
    /// отслеживаемого персонажа, Entity Framework Core считает изменённой, а не новой.
    /// </param>
    public static void Apply(Character source, Character target, ICollection<object> added)
    {
        ApplyDescription(source, target);
        ApplyAttributes(source, target, added);
        ApplySkills(source, target, added);
        ApplyResources(source, target);
        ApplyTraits(source, target, added);
    }

    /// <summary>
    /// Переносит описание персонажа.
    /// </summary>
    /// <param name="source">Персонаж, изменённый на листе.</param>
    /// <param name="target">Сохранённая запись персонажа.</param>
    private static void ApplyDescription(Character source, Character target)
    {
        target.Name = source.Name;
        target.FullName = source.FullName;
        target.Gender = source.Gender;
        target.Age = source.Age;
        target.Height = source.Height;
        target.Weight = source.Weight;
        target.Alignment = source.Alignment;
        target.Languages = source.Languages;
        target.Level = source.Level;
        target.Experience = source.Experience;
        target.Portrait = source.Portrait;
        target.Description = source.Description;
        target.Biography = source.Biography;
        target.Notes = source.Notes;
    }

    /// <summary>
    /// Переносит базовые значения характеристик.
    /// Итоговое значение и модификатор не переносятся: их задаёт пересчёт.
    /// </summary>
    /// <param name="source">Персонаж, изменённый на листе.</param>
    /// <param name="target">Сохранённая запись персонажа.</param>
    /// <param name="added">Список созданных записей.</param>
    private static void ApplyAttributes(Character source, Character target, ICollection<object> added)
    {
        var stored = target.Attributes.ToDictionary(value => value.AttributeId);

        foreach (var value in source.Attributes)
        {
            if (stored.TryGetValue(value.AttributeId, out var existing))
            {
                existing.BaseValue = value.BaseValue;
                existing.TemporaryBonus = value.TemporaryBonus;
                existing.OverrideValue = value.OverrideValue;

                continue;
            }

            var created = new CharacterAttributeValue
            {
                CharacterId = target.Id,
                AttributeId = value.AttributeId,
                BaseValue = value.BaseValue,
                TemporaryBonus = value.TemporaryBonus,
                OverrideValue = value.OverrideValue,
            };

            target.Attributes.Add(created);
            added.Add(created);
        }
    }

    /// <summary>
    /// Согласует владение навыками.
    /// </summary>
    /// <param name="source">Персонаж, изменённый на листе.</param>
    /// <param name="target">Сохранённая запись персонажа.</param>
    /// <param name="added">Список созданных записей.</param>
    private static void ApplySkills(Character source, Character target, ICollection<object> added)
    {
        var edited = source.Skills.ToDictionary(skill => skill.SkillId);

        foreach (var removed in target.Skills.Where(skill => !edited.ContainsKey(skill.SkillId)).ToList())
        {
            target.Skills.Remove(removed);
        }

        var stored = target.Skills.ToDictionary(skill => skill.SkillId);

        foreach (var skill in edited.Values)
        {
            if (stored.TryGetValue(skill.SkillId, out var existing))
            {
                existing.ProficiencyLevel = skill.ProficiencyLevel;
                existing.Bonus = skill.Bonus;

                continue;
            }

            var created = new CharacterSkill
            {
                CharacterId = target.Id,
                SkillId = skill.SkillId,
                ProficiencyLevel = skill.ProficiencyLevel,
                Bonus = skill.Bonus,
            };

            target.Skills.Add(created);
            added.Add(created);
        }
    }

    /// <summary>
    /// Переносит текущие значения ресурсов.
    /// Максимум не переносится: его задаёт формула ресурса.
    /// </summary>
    /// <param name="source">Персонаж, изменённый на листе.</param>
    /// <param name="target">Сохранённая запись персонажа.</param>
    private static void ApplyResources(Character source, Character target)
    {
        var stored = target.Resources.ToDictionary(resource => resource.ResourceId);

        foreach (var resource in source.Resources)
        {
            if (stored.TryGetValue(resource.ResourceId, out var existing))
            {
                existing.Current = resource.Current;
            }
        }
    }

    /// <summary>
    /// Согласует полученные черты.
    /// </summary>
    /// <param name="source">Персонаж, изменённый на листе.</param>
    /// <param name="target">Сохранённая запись персонажа.</param>
    /// <param name="added">Список созданных записей.</param>
    private static void ApplyTraits(Character source, Character target, ICollection<object> added)
    {
        var edited = source.Traits.ToDictionary(trait => trait.TraitId);

        foreach (var removed in target.Traits.Where(trait => !edited.ContainsKey(trait.TraitId)).ToList())
        {
            target.Traits.Remove(removed);
        }

        var stored = target.Traits.ToDictionary(trait => trait.TraitId);

        foreach (var trait in edited.Values)
        {
            if (stored.TryGetValue(trait.TraitId, out var existing))
            {
                existing.IsActive = trait.IsActive;
                existing.RemainingUses = trait.RemainingUses;

                continue;
            }

            var created = new CharacterTrait
            {
                CharacterId = target.Id,
                TraitId = trait.TraitId,
                Source = trait.Source,
                IsActive = trait.IsActive,
                RemainingUses = trait.RemainingUses,
            };

            target.Traits.Add(created);
            added.Add(created);
        }
    }
}
