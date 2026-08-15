using System.Globalization;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Core.Models.History;

/// <summary>
/// Создание записей журнала.
///
/// Запись создаёт та служба, которая выполнила действие: она одна знает и старое
/// значение, и новое, и причину изменения, а её запись сохраняется той же
/// операцией, что и само изменение. Здесь собраны только описания событий,
/// чтобы одинаковые события выглядели в журнале одинаково.
/// </summary>
public static class HistoryEntries
{
    /// <summary>
    /// Создаёт запись об изменении ресурса.
    ///
    /// Здоровье в приложении — такой же ресурс, как заряды или ячейки заклинаний,
    /// поэтому его изменения попадают в журнал этим же путём.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="resourceName">Название ресурса.</param>
    /// <param name="before">Значение до изменения.</param>
    /// <param name="after">Значение после изменения.</param>
    /// <param name="reason">Причина изменения: название предмета, заклинания или действия.</param>
    /// <returns>Запись журнала.</returns>
    public static HistoryEntry ResourceChanged(
        Guid characterId,
        string resourceName,
        double before,
        double after,
        string? reason = null) =>
        new()
        {
            // Числа хранятся отдельными значениями и показываются журналом
            // как «было → стало», поэтому в описании они не повторяются.
            CharacterId = characterId,
            Action = HistoryActions.ResourceChanged,
            Subject = resourceName,
            Description = string.IsNullOrWhiteSpace(reason)
                ? resourceName
                : $"{resourceName} ({reason})",
            OldValue = Format(before),
            NewValue = Format(after),
            Amount = after - before,
        };

    /// <summary>
    /// Создаёт запись об использовании предмета.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="itemName">Название предмета.</param>
    /// <param name="description">Что произошло при использовании.</param>
    /// <returns>Запись журнала.</returns>
    public static HistoryEntry ItemUsed(Guid characterId, string itemName, string? description = null) =>
        new()
        {
            CharacterId = characterId,
            Action = HistoryActions.ItemUsed,
            Subject = itemName,
            Description = string.IsNullOrWhiteSpace(description)
                ? $"Использован предмет «{itemName}»."
                : $"Использован предмет «{itemName}»: {description}",
        };

    /// <summary>
    /// Создаёт запись о надевании предмета.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="itemName">Название предмета.</param>
    /// <param name="slotName">Название слота экипировки.</param>
    /// <returns>Запись журнала.</returns>
    public static HistoryEntry ItemEquipped(Guid characterId, string itemName, string slotName) =>
        new()
        {
            CharacterId = characterId,
            Action = HistoryActions.ItemEquipped,
            Subject = itemName,
            Description = $"Надето «{itemName}» в слот «{slotName}».",
            NewValue = itemName,
        };

    /// <summary>
    /// Создаёт запись о снятии предмета.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="itemName">Название предмета.</param>
    /// <param name="slotName">Название слота экипировки.</param>
    /// <returns>Запись журнала.</returns>
    public static HistoryEntry ItemUnequipped(Guid characterId, string itemName, string? slotName) =>
        new()
        {
            CharacterId = characterId,
            Action = HistoryActions.ItemUnequipped,
            Subject = itemName,
            Description = string.IsNullOrWhiteSpace(slotName)
                ? $"Снято «{itemName}»."
                : $"Снято «{itemName}» из слота «{slotName}».",
            OldValue = itemName,
        };

    /// <summary>
    /// Создаёт записи об изменениях ресурсов персонажа.
    ///
    /// Сравниваются значения до и после действия, поэтому запись появляется только
    /// у тех ресурсов, которые действительно изменились: сохранение листа без
    /// правок ресурсов журнал не засоряет.
    /// </summary>
    /// <param name="character">Персонаж с уже изменёнными ресурсами.</param>
    /// <param name="before">Значения ресурсов до изменения по идентификатору ресурса.</param>
    /// <param name="reason">Причина изменения.</param>
    /// <param name="names">
    /// Названия ресурсов по идентификатору. Задаются, если у персонажа загружены
    /// только значения ресурсов, без их описаний: запись «Ресурс: 12 → 7»
    /// пользователю ничего не сообщает.
    /// </param>
    /// <returns>Записи журнала.</returns>
    public static List<HistoryEntry> ResourceChanges(
        Character character,
        IReadOnlyDictionary<Guid, double> before,
        string? reason = null,
        IReadOnlyDictionary<Guid, string>? names = null)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(before);

        var entries = new List<HistoryEntry>();

        foreach (var resource in character.Resources)
        {
            if (!before.TryGetValue(resource.ResourceId, out var previous)
                || Math.Abs(previous - resource.Current) < double.Epsilon)
            {
                continue;
            }

            var name = resource.Resource?.Name
                ?? (names is not null && names.TryGetValue(resource.ResourceId, out var found)
                    ? found
                    : "Ресурс");

            entries.Add(ResourceChanged(character.Id, name, previous, resource.Current, reason));
        }

        return entries;
    }

    /// <summary>
    /// Запоминает текущие значения ресурсов персонажа.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <returns>Значения ресурсов по идентификатору ресурса.</returns>
    public static Dictionary<Guid, double> SnapshotResources(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);

        return character.Resources.ToDictionary(
            resource => resource.ResourceId,
            resource => resource.Current);
    }

    private static string Format(double value) =>
        value.ToString("0.##", CultureInfo.CurrentCulture);
}
