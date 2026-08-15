using System.Globalization;
using RPGCharacterManager.Core.Abstractions.Items;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Раздел дерева категорий инвентаря.
/// </summary>
public sealed class InventoryCategoryViewModel : ViewModelBase
{
    /// <summary>Отступ одного уровня вложенности в единицах разметки.</summary>
    private const double IndentStep = 14;

    /// <summary>
    /// Создаёт раздел дерева категорий.
    /// </summary>
    /// <param name="node">Раздел дерева.</param>
    public InventoryCategoryViewModel(InventoryCategoryNode node) => Node = Guard.NotNull(node);

    /// <summary>Раздел дерева.</summary>
    public InventoryCategoryNode Node { get; }

    /// <summary>Идентификатор категории.</summary>
    public Guid? CategoryId => Node.CategoryId;

    /// <summary>Название раздела.</summary>
    public string Name => Node.Name;

    /// <summary>Количество предметов в разделе.</summary>
    public string CountText => Node.Count.ToString(CultureInfo.CurrentCulture);

    /// <summary>Отступ, показывающий вложенность категории.</summary>
    public Avalonia.Thickness Indent => new(Node.Depth * IndentStep, 0, 0, 0);
}

/// <summary>
/// Способ упорядочивания предметов инвентаря.
/// </summary>
/// <param name="Sort">Способ упорядочивания.</param>
/// <param name="DisplayName">Название для списка выбора.</param>
public sealed record InventorySortOption(InventorySort Sort, string DisplayName);

/// <summary>
/// Запись инвентаря на листе персонажа.
/// </summary>
public sealed class InventoryEntryViewModel : ViewModelBase
{
    /// <summary>Отступ одного уровня вложенности во вместилищах.</summary>
    private const double IndentStep = 18;

    /// <summary>
    /// Создаёт строку инвентаря.
    /// </summary>
    /// <param name="entry">Запись инвентаря.</param>
    public InventoryEntryViewModel(InventoryEntry entry) => Entry = Guard.NotNull(entry);

    /// <summary>Запись инвентаря.</summary>
    public InventoryEntry Entry { get; }

    /// <summary>Идентификатор записи инвентаря.</summary>
    public Guid InventoryItemId => Entry.InventoryItemId;

    /// <summary>Название предмета.</summary>
    public string Name => Entry.Name;

    /// <summary>Описание предмета.</summary>
    public string? Description => Entry.Description;

    /// <summary>Описание задано.</summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(Entry.Description);

    /// <summary>Отступ, показывающий вложенность во вместилищах.</summary>
    public Avalonia.Thickness Indent => new(Entry.Depth * IndentStep, 0, 0, 0);

    /// <summary>Количество предметов в записи.</summary>
    public string CountText => Entry.Count.ToString(CultureInfo.CurrentCulture);

    /// <summary>Количество показывается: одиночный предмет в нём не нуждается.</summary>
    public bool HasCount => Entry.Count > 1;

    /// <summary>Вес записи.</summary>
    public string WeightText => SheetNumber.Format(Entry.Weight);

    /// <summary>Запись что-то весит.</summary>
    public bool HasWeight => Math.Abs(Entry.Weight) > double.Epsilon;

    /// <summary>Стоимость записи вместе с валютой.</summary>
    public string PriceText => string.IsNullOrWhiteSpace(Entry.Currency)
        ? SheetNumber.Format(Entry.Price)
        : $"{SheetNumber.Format(Entry.Price)} {Entry.Currency}";

    /// <summary>Запись что-то стоит.</summary>
    public bool HasPrice => Math.Abs(Entry.Price) > double.Epsilon;

    /// <summary>Категория, тип и редкость предмета одной строкой.</summary>
    public string Subtitle => string.Join(
        " • ",
        new[] { Entry.CategoryName, Entry.ItemType, Entry.Rarity }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>Пояснение к предмету задано.</summary>
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    /// <summary>У предмета есть заряды.</summary>
    public bool HasCharges => Entry.HasCharges;

    /// <summary>Оставшиеся и наибольшие заряды предмета.</summary>
    public string ChargesText =>
        $"{Entry.RemainingCharges ?? 0} / {Entry.MaximumCharges ?? 0}";

    /// <summary>Предмет можно использовать прямо сейчас.</summary>
    public bool CanUse => Entry.CanUse;

    /// <summary>Использование предмета сейчас невозможно, и причина известна.</summary>
    public bool HasUnusableReason => !string.IsNullOrWhiteSpace(Entry.UnusableReason);

    /// <summary>Причина, по которой предмет нельзя использовать.</summary>
    public string? UnusableReason => Entry.UnusableReason;

    /// <summary>Предмет можно использовать или его использование только сорвано.</summary>
    public bool IsUsable => Entry.CanUse || HasUnusableReason;

    /// <summary>Предмет вмещает другие предметы.</summary>
    public bool IsContainer => Entry.IsContainer;

    /// <summary>Заполненность вместилища.</summary>
    public string ContainerText => Entry.Capacity is { } capacity
        ? $"вмещает {SheetNumber.Format(Entry.ContentWeight)} из {SheetNumber.Format(capacity)}"
        : $"вмещает {SheetNumber.Format(Entry.ContentWeight)}";

    /// <summary>Во вместилище лежит больше, чем оно вмещает.</summary>
    public bool IsOverfilled => Entry.IsOverfilled;

    /// <summary>Предмет надет.</summary>
    public bool IsEquipped => Entry.IsEquipped;

    /// <summary>Пометка пользователя.</summary>
    public string? Note => Entry.Note;

    /// <summary>Пометка задана.</summary>
    public bool HasNote => !string.IsNullOrWhiteSpace(Entry.Note);
}
