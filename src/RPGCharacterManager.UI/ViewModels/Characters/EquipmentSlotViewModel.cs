using System.Collections.ObjectModel;
using System.Globalization;
using RPGCharacterManager.Core.Abstractions.Items;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Бонус надетого предмета на листе персонажа.
/// </summary>
public sealed class EquipmentBonusViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт строку бонуса.
    /// </summary>
    /// <param name="bonus">Вычисленный бонус.</param>
    public EquipmentBonusViewModel(EquipmentBonus bonus) => Bonus = Guard.NotNull(bonus);

    /// <summary>Вычисленный бонус.</summary>
    public EquipmentBonus Bonus { get; }

    /// <summary>Что изменяет бонус и на сколько.</summary>
    public string Text => Math.Abs(Bonus.Value) < double.Epsilon
        ? Bonus.Description
        : $"{Bonus.Description}: {SheetNumber.Format(Bonus.Value)}";

    /// <summary>
    /// Формула и условие бонуса. Постоянная формула не показывается: она повторила бы
    /// уже видимую величину.
    /// </summary>
    public string Hint => string.Join(
        " • ",
        new[]
        {
            string.Equals(Bonus.Formula?.Trim(), SheetNumber.Format(Bonus.Value), StringComparison.Ordinal)
                ? null
                : Bonus.Formula,
            string.IsNullOrWhiteSpace(Bonus.Condition) ? null : $"при условии: {Bonus.Condition}",
            Bonus.IsApplied ? null : "условие не выполнено, бонус не действует",
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>Пояснение задано.</summary>
    public bool HasHint => !string.IsNullOrWhiteSpace(Hint);

    /// <summary>Бонус действует.</summary>
    public bool IsApplied => Bonus.IsApplied;
}

/// <summary>
/// Надетый предмет на листе персонажа.
/// </summary>
public sealed class EquippedItemViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт строку надетого предмета.
    /// </summary>
    /// <param name="item">Надетый предмет.</param>
    public EquippedItemViewModel(EquippedItem item)
    {
        Item = Guard.NotNull(item);
        Bonuses = new ObservableCollection<EquipmentBonusViewModel>(
            item.Bonuses.Select(bonus => new EquipmentBonusViewModel(bonus)));
    }

    /// <summary>Надетый предмет.</summary>
    public EquippedItem Item { get; }

    /// <summary>Идентификатор записи инвентаря.</summary>
    public Guid InventoryItemId => Item.InventoryItemId;

    /// <summary>Название предмета.</summary>
    public string Name => Item.Name;

    /// <summary>Описание предмета.</summary>
    public string? Description => Item.Description;

    /// <summary>Описание задано.</summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(Item.Description);

    /// <summary>Тип предмета.</summary>
    public string? ItemType => Item.ItemType;

    /// <summary>Тип предмета задан.</summary>
    public bool HasItemType => !string.IsNullOrWhiteSpace(Item.ItemType);

    /// <summary>Бонусы предмета.</summary>
    public ObservableCollection<EquipmentBonusViewModel> Bonuses { get; }

    /// <summary>Предмет даёт бонусы.</summary>
    public bool HasBonuses => Bonuses.Count > 0;

    /// <summary>Требования предмета нарушены.</summary>
    public bool HasUnavailableReason => !string.IsNullOrWhiteSpace(Item.UnavailableReason);

    /// <summary>Причина, по которой требования не выполнены.</summary>
    public string? UnavailableReason => Item.UnavailableReason;
}

/// <summary>
/// Слот экипировки на листе персонажа.
/// </summary>
public sealed class EquipmentSlotViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт слот экипировки.
    /// </summary>
    /// <param name="slot">Состояние слота.</param>
    public EquipmentSlotViewModel(EquipmentSlotState slot)
    {
        Slot = Guard.NotNull(slot);
        Items = new ObservableCollection<EquippedItemViewModel>(
            slot.Items.Select(item => new EquippedItemViewModel(item)));
    }

    /// <summary>Состояние слота.</summary>
    public EquipmentSlotState Slot { get; }

    /// <summary>Идентификатор слота.</summary>
    public Guid SlotId => Slot.SlotId;

    /// <summary>Название слота.</summary>
    public string Name => Slot.Name;

    /// <summary>Надетые предметы.</summary>
    public ObservableCollection<EquippedItemViewModel> Items { get; }

    /// <summary>Слот пуст.</summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>В слот можно надеть ещё один предмет.</summary>
    public bool HasFreeSpace => Slot.HasFreeSpace;

    /// <summary>Заполненность слота и пояснение к нему.</summary>
    public string Hint
    {
        get
        {
            var capacity = Slot.MaximumItems.ToString(CultureInfo.CurrentCulture);
            var occupied = Items.Count.ToString(CultureInfo.CurrentCulture);
            var text = $"занято {occupied} из {capacity}";

            return string.IsNullOrWhiteSpace(Slot.Description)
                ? text
                : $"{Slot.Description} • {text}";
        }
    }
}
