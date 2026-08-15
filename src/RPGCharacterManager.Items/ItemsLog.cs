using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.Items;

/// <summary>
/// Сообщения журнала подсистемы предметов.
/// </summary>
internal static partial class ItemsLog
{
    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Information,
        Message = "Персонаж «{CharacterName}» получил оружие «{WeaponName}».")]
    public static partial void WeaponAdded(ILogger logger, string weaponName, string characterName);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Information,
        Message = "Персонаж «{CharacterName}» атаковал оружием «{WeaponName}», урон {Damage}.")]
    public static partial void WeaponAttacked(
        ILogger logger,
        string characterName,
        string weaponName,
        double damage);

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Information,
        Message = "Персонаж «{CharacterName}» перезарядил оружие «{WeaponName}»: заряжено {Count}.")]
    public static partial void WeaponReloaded(
        ILogger logger,
        string characterName,
        string weaponName,
        int count);

    [LoggerMessage(
        EventId = 7004,
        Level = LogLevel.Error,
        Message = "Не удалось выполнить действие с оружием персонажа {CharacterId}.")]
    public static partial void WeaponOperationFailed(
        ILogger logger,
        Exception exception,
        Guid characterId);

    [LoggerMessage(
        EventId = 7005,
        Level = LogLevel.Information,
        Message = "Персонаж «{CharacterName}» надел «{ItemName}» в слот «{SlotName}».")]
    public static partial void ItemEquipped(
        ILogger logger,
        string characterName,
        string itemName,
        string slotName);

    [LoggerMessage(
        EventId = 7006,
        Level = LogLevel.Information,
        Message = "Персонаж {CharacterId} снял предмет.")]
    public static partial void ItemUnequipped(ILogger logger, Guid characterId);

    [LoggerMessage(
        EventId = 7007,
        Level = LogLevel.Error,
        Message = "Не удалось выполнить действие с экипировкой персонажа {CharacterId}.")]
    public static partial void EquipmentOperationFailed(
        ILogger logger,
        Exception exception,
        Guid characterId);

    [LoggerMessage(
        EventId = 7008,
        Level = LogLevel.Information,
        Message = "Персонаж «{CharacterName}» получил «{ItemName}» в количестве {Count}.")]
    public static partial void ItemAdded(
        ILogger logger,
        string characterName,
        string itemName,
        int count);

    [LoggerMessage(
        EventId = 7009,
        Level = LogLevel.Information,
        Message = "Персонаж «{CharacterName}» использовал «{ItemName}».")]
    public static partial void ItemUsed(ILogger logger, string characterName, string itemName);

    [LoggerMessage(
        EventId = 7010,
        Level = LogLevel.Error,
        Message = "Не удалось выполнить действие с инвентарём персонажа {CharacterId}.")]
    public static partial void InventoryOperationFailed(
        ILogger logger,
        Exception exception,
        Guid characterId);
}
