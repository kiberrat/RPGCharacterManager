using Microsoft.EntityFrameworkCore;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database;

namespace RPGCharacterManager.Items;

/// <summary>
/// Запас боеприпасов персонажа, хранимый обычными записями инвентаря.
///
/// Боеприпас — это предмет, поэтому отдельного хранилища у него нет: патроны, стрелы,
/// заряды и любой другой расходуемый предмет лежат в инвентаре и учитываются одинаково.
/// </summary>
internal static class AmmunitionStore
{
    /// <summary>
    /// Возвращает количество боеприпасов, имеющихся у персонажа.
    /// </summary>
    /// <param name="character">Персонаж с загруженным инвентарём.</param>
    /// <param name="ammunitionItemId">Идентификатор предмета-боеприпаса.</param>
    /// <returns>Общее количество боеприпасов.</returns>
    public static int CountReserve(Character character, Guid ammunitionItemId) =>
        character.Inventory
            .Where(record => record.ItemId == ammunitionItemId)
            .Sum(record => record.Count);

    /// <summary>
    /// Расходует боеприпасы из запаса персонажа.
    /// </summary>
    /// <param name="character">Персонаж с загруженным инвентарём.</param>
    /// <param name="ammunitionItemId">Идентификатор предмета-боеприпаса.</param>
    /// <param name="count">Расходуемое количество.</param>
    /// <param name="context">Контекст базы данных.</param>
    /// <returns><see langword="true"/>, если боеприпасов хватило.</returns>
    public static bool TryConsume(
        Character character,
        Guid ammunitionItemId,
        int count,
        RpgDbContext context)
    {
        if (CountReserve(character, ammunitionItemId) < count)
        {
            return false;
        }

        var remaining = count;

        foreach (var record in character.Inventory
                     .Where(item => item.ItemId == ammunitionItemId)
                     .OrderBy(item => item.Count)
                     .ToList())
        {
            if (remaining <= 0)
            {
                break;
            }

            var taken = Math.Min(record.Count, remaining);

            record.Count -= taken;
            remaining -= taken;

            // Опустевшая стопка удаляется: инвентарь не должен накапливать
            // записи с нулевым количеством.
            if (record.Count <= 0)
            {
                character.Inventory.Remove(record);
                context.Remove(record);
            }
        }

        return true;
    }

    /// <summary>
    /// Задаёт количество боеприпасов в запасе персонажа.
    /// </summary>
    /// <param name="character">Персонаж с загруженным инвентарём.</param>
    /// <param name="ammunitionItemId">Идентификатор предмета-боеприпаса.</param>
    /// <param name="count">Требуемое количество.</param>
    /// <param name="context">Контекст базы данных.</param>
    public static void SetReserve(
        Character character,
        Guid ammunitionItemId,
        int count,
        RpgDbContext context)
    {
        var records = character.Inventory
            .Where(item => item.ItemId == ammunitionItemId)
            .OrderByDescending(item => item.Count)
            .ToList();

        // Лишние стопки убираются, а требуемое количество остаётся в одной записи:
        // подробное размещение по контейнерам относится к подсистеме инвентаря.
        foreach (var extra in records.Skip(1))
        {
            character.Inventory.Remove(extra);
            context.Remove(extra);
        }

        var first = records.FirstOrDefault();

        if (count <= 0)
        {
            if (first is not null)
            {
                character.Inventory.Remove(first);
                context.Remove(first);
            }

            return;
        }

        if (first is null)
        {
            var created = new InventoryItem
            {
                CharacterId = character.Id,
                ItemId = ammunitionItemId,
                Count = count,
            };

            character.Inventory.Add(created);

            // Запись создаётся с уже заданным идентификатором, поэтому передаётся
            // контексту явно: иначе она была бы принята за изменение (решение Р-28).
            context.Add(created);

            return;
        }

        first.Count = count;
    }
}
