using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Items;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Инвентарь персонажа на его листе.
///
/// Раздел вынесен в отдельную модель представления: у него собственные отбор, порядок
/// показа и набор действий, и они не имеют отношения к остальным разделам листа.
/// Никаких игровых правил здесь нет — всё считает служба инвентаря.
/// </summary>
public sealed partial class InventoryViewModel : ViewModelBase
{
    private readonly IInventoryService _inventory;
    private readonly IDialogService _dialogs;

    private Guid _characterId;

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private InventorySortOption? _selectedSort;

    [ObservableProperty]
    private bool _isDescending;

    [ObservableProperty]
    private InventoryCategoryViewModel? _selectedCategory;

    [ObservableProperty]
    private InventoryEntryViewModel? _selectedEntry;

    [ObservableProperty]
    private bool _isPickerOpen;

    [ObservableProperty]
    private string _pickerSearch = string.Empty;

    [ObservableProperty]
    private CharacterOptionViewModel? _selectedAvailableItem;

    [ObservableProperty]
    private int _addCount = 1;

    [ObservableProperty]
    private string _weightText = string.Empty;

    [ObservableProperty]
    private bool _isOverloaded;

    [ObservableProperty]
    private string _moneyText = string.Empty;

    [ObservableProperty]
    private string? _lastReport;

    /// <summary>
    /// Создаёт модель представления инвентаря.
    /// </summary>
    /// <param name="inventory">Служба инвентаря персонажа.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    public InventoryViewModel(IInventoryService inventory, IDialogService dialogs)
    {
        _inventory = Guard.NotNull(inventory);
        _dialogs = Guard.NotNull(dialogs);

        SortOptions =
        [
            new InventorySortOption(InventorySort.Name, "По названию"),
            new InventorySortOption(InventorySort.Weight, "По весу"),
            new InventorySortOption(InventorySort.Price, "По стоимости"),
            new InventorySortOption(InventorySort.Count, "По количеству"),
            new InventorySortOption(InventorySort.Rarity, "По редкости"),
            new InventorySortOption(InventorySort.Added, "По времени получения"),
        ];

        _selectedSort = SortOptions[0];
    }

    /// <summary>Предметы персонажа в выбранном порядке.</summary>
    public ObservableCollection<InventoryEntryViewModel> Entries { get; } = [];

    /// <summary>Разделы дерева категорий.</summary>
    public ObservableCollection<InventoryCategoryViewModel> Categories { get; } = [];

    /// <summary>Вместилища, в которые можно переложить предмет.</summary>
    public ObservableCollection<InventoryContainerOption> Containers { get; } = [];

    /// <summary>Предметы, доступные для получения.</summary>
    public ObservableCollection<CharacterOptionViewModel> AvailableItems { get; } = [];

    /// <summary>Способы упорядочивания предметов.</summary>
    public IReadOnlyList<InventorySortOption> SortOptions { get; }

    /// <summary>Инвентарь пуст.</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>
    /// Дерево категорий состоит из одного раздела «Все предметы»:
    /// пользователь ещё не создал ни одной категории или не назначил их предметам.
    /// </summary>
    public bool HasNoCategories => Categories.Count <= 1;

    /// <summary>Отбор скрыл все предметы.</summary>
    public bool IsFiltered =>
        !string.IsNullOrWhiteSpace(Search) || SelectedCategory?.CategoryId is not null;

    /// <summary>Отчёт о последнем действии показан.</summary>
    public bool HasReport => !string.IsNullOrWhiteSpace(LastReport);

    /// <summary>Предмет выбран.</summary>
    public bool HasSelection => SelectedEntry is not null;

    /// <summary>
    /// Привязывает инвентарь к персонажу и загружает его.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    public async Task InitializeAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        _characterId = characterId;

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Перечитывает инвентарь персонажа.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (_characterId == Guid.Empty)
        {
            return;
        }

        var query = new InventoryQuery(
            Search,
            SelectedCategory?.CategoryId,
            SelectedSort?.Sort ?? InventorySort.Name,
            IsDescending);

        var result = await _inventory.GetAsync(_characterId, query, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await ReportAsync(result).ConfigureAwait(true);
            return;
        }

        Fill(result.Value);
    }

    /// <summary>
    /// Показывает или скрывает список предметов, доступных для получения.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки списка.</returns>
    [RelayCommand]
    private async Task TogglePickerAsync(CancellationToken cancellationToken)
    {
        IsPickerOpen = !IsPickerOpen;

        if (IsPickerOpen)
        {
            await ReloadAvailableItemsAsync(cancellationToken).ConfigureAwait(true);
        }
        else
        {
            AvailableItems.Clear();
            SelectedAvailableItem = null;
        }
    }

    /// <summary>
    /// Выдаёт персонажу выбранный предмет.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после выдачи.</returns>
    [RelayCommand]
    private async Task AddAsync(CancellationToken cancellationToken)
    {
        if (SelectedAvailableItem is not { } option)
        {
            return;
        }

        var count = Math.Max(1, AddCount);
        var result = await _inventory
            .AddAsync(_characterId, option.Id, count, cancellationToken)
            .ConfigureAwait(true);

        if (!await ReportAsync(result).ConfigureAwait(true))
        {
            return;
        }

        LastReport = $"Получено: {option.Name} ×{count.ToString(CultureInfo.CurrentCulture)}.";
        OnPropertyChanged(nameof(HasReport));

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Добавляет один предмет к выбранной записи.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после изменения.</returns>
    [RelayCommand]
    private Task IncreaseCountAsync(CancellationToken cancellationToken) =>
        ChangeCountAsync(1, cancellationToken);

    /// <summary>
    /// Убирает один предмет из выбранной записи.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после изменения.</returns>
    [RelayCommand]
    private Task DecreaseCountAsync(CancellationToken cancellationToken) =>
        ChangeCountAsync(-1, cancellationToken);

    /// <summary>
    /// Изменяет количество предметов в выбранной записи.
    /// </summary>
    /// <param name="delta">Изменение количества.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после изменения.</returns>
    private async Task ChangeCountAsync(int delta, CancellationToken cancellationToken)
    {
        if (SelectedEntry is not { } entry)
        {
            return;
        }

        var result = await _inventory
            .ChangeCountAsync(_characterId, entry.InventoryItemId, delta, cancellationToken)
            .ConfigureAwait(true);

        if (await ReportAsync(result).ConfigureAwait(true))
        {
            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Убирает выбранную запись инвентаря.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task RemoveAsync(CancellationToken cancellationToken)
    {
        if (SelectedEntry is not { } entry)
        {
            return;
        }

        var confirmed = await _dialogs
            .ShowConfirmationAsync(
                "Убрать предмет",
                $"Убрать «{entry.Name}» из инвентаря? Содержимое вместилища останется у персонажа.")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var result = await _inventory
            .RemoveAsync(_characterId, entry.InventoryItemId, cancellationToken)
            .ConfigureAwait(true);

        if (await ReportAsync(result).ConfigureAwait(true))
        {
            LastReport = $"Убрано: {entry.Name}.";
            OnPropertyChanged(nameof(HasReport));

            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Перекладывает выбранный предмет во вместилище.
    /// </summary>
    /// <param name="container">Вместилище либо размещение вне вместилищ.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после перемещения.</returns>
    [RelayCommand]
    private async Task MoveAsync(InventoryContainerOption? container, CancellationToken cancellationToken)
    {
        if (SelectedEntry is not { } entry || container is null)
        {
            return;
        }

        var result = await _inventory
            .MoveAsync(_characterId, entry.InventoryItemId, container.InventoryItemId, cancellationToken)
            .ConfigureAwait(true);

        if (await ReportAsync(result).ConfigureAwait(true))
        {
            LastReport = $"«{entry.Name}» перемещён: {container.Name.ToLower(CultureInfo.CurrentCulture)}.";
            OnPropertyChanged(nameof(HasReport));

            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Использует выбранный предмет.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после использования.</returns>
    [RelayCommand]
    private async Task UseAsync(CancellationToken cancellationToken)
    {
        if (SelectedEntry is not { } entry)
        {
            return;
        }

        var result = await _inventory
            .UseAsync(_characterId, entry.InventoryItemId, cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs
                .ShowErrorAsync("Предмет не использован", result.Error!, null)
                .ConfigureAwait(true);

            return;
        }

        LastReport = Describe(result.Value);
        OnPropertyChanged(nameof(HasReport));

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Восстанавливает заряды выбранного предмета.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после восстановления.</returns>
    [RelayCommand]
    private async Task RestoreChargesAsync(CancellationToken cancellationToken)
    {
        if (SelectedEntry is not { } entry)
        {
            return;
        }

        var result = await _inventory
            .RestoreChargesAsync(_characterId, entry.InventoryItemId, cancellationToken)
            .ConfigureAwait(true);

        if (await ReportAsync(result).ConfigureAwait(true))
        {
            LastReport = $"Заряды предмета «{entry.Name}» восстановлены.";
            OnPropertyChanged(nameof(HasReport));

            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Показывает предметы выбранной категории.
    /// </summary>
    /// <param name="category">Раздел дерева категорий.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    [RelayCommand]
    private async Task SelectCategoryAsync(
        InventoryCategoryViewModel? category,
        CancellationToken cancellationToken)
    {
        SelectedCategory = category;

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    partial void OnSearchChanged(string value) => _ = ReloadAsync(CancellationToken.None);

    partial void OnSelectedSortChanged(InventorySortOption? value) => _ = ReloadAsync(CancellationToken.None);

    partial void OnIsDescendingChanged(bool value) => _ = ReloadAsync(CancellationToken.None);

    partial void OnPickerSearchChanged(string value) =>
        _ = ReloadAvailableItemsAsync(CancellationToken.None);

    partial void OnSelectedEntryChanged(InventoryEntryViewModel? value) =>
        OnPropertyChanged(nameof(HasSelection));

    /// <summary>
    /// Переносит состояние инвентаря в списки представления.
    /// </summary>
    /// <param name="state">Состояние инвентаря.</param>
    private void Fill(InventoryState state)
    {
        // Выбор сохраняется по идентификатору: перечитывание инвентаря не должно
        // сбрасывать выделенную строку, иначе действия над ней прерывались бы.
        var selectedId = SelectedEntry?.InventoryItemId;
        var selectedCategoryId = SelectedCategory?.CategoryId;

        Entries.Clear();

        foreach (var entry in state.Entries)
        {
            Entries.Add(new InventoryEntryViewModel(entry));
        }

        Categories.Clear();

        foreach (var category in state.Categories)
        {
            Categories.Add(new InventoryCategoryViewModel(category));
        }

        Containers.Clear();

        foreach (var container in state.Containers)
        {
            Containers.Add(container);
        }

        SelectedCategory = Categories.FirstOrDefault(category => category.CategoryId == selectedCategoryId)
            ?? Categories.FirstOrDefault();

        SelectedEntry = Entries.FirstOrDefault(entry => entry.InventoryItemId == selectedId);

        WeightText = FormatWeight(state.Weight);
        IsOverloaded = state.Weight.IsOverloaded;
        MoneyText = FormatMoney(state.Money);

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasNoCategories));
        OnPropertyChanged(nameof(IsFiltered));
        OnPropertyChanged(nameof(HasSelection));
    }

    private async Task ReloadAvailableItemsAsync(CancellationToken cancellationToken)
    {
        if (!IsPickerOpen || _characterId == Guid.Empty)
        {
            return;
        }

        var page = await _inventory
            .GetAvailableItemsAsync(_characterId, PickerSearch, cancellationToken)
            .ConfigureAwait(true);

        AvailableItems.Clear();

        foreach (var option in page.Options)
        {
            AvailableItems.Add(new CharacterOptionViewModel(option));
        }

        SelectedAvailableItem = AvailableItems.FirstOrDefault();
    }

    /// <summary>
    /// Показывает ошибку операции, если она произошла.
    /// </summary>
    /// <param name="result">Результат операции.</param>
    /// <returns><see langword="true"/>, если операция удалась.</returns>
    private async Task<bool> ReportAsync(Result result)
    {
        Guard.NotNull(result);

        if (result.IsSuccess)
        {
            return true;
        }

        await _dialogs.ShowErrorAsync("Инвентарь", result.Error!, null).ConfigureAwait(true);

        return false;
    }

    /// <summary>
    /// Описывает итог использования предмета одной строкой.
    /// </summary>
    /// <param name="result">Итог использования.</param>
    /// <returns>Текст отчёта.</returns>
    private static string Describe(ItemUseResult result)
    {
        var parts = new List<string> { $"Использовано: {result.ItemName}." };

        foreach (var effect in result.Effects)
        {
            parts.Add(effect.IsApplied
                ? $"{effect.Description}: {SheetNumber.Format(effect.Value)}"
                : effect.Description);
        }

        if (result.SpentCharge)
        {
            parts.Add($"Осталось зарядов: {result.RemainingCharges ?? 0}.");
        }

        if (result.SpentUnit)
        {
            parts.Add($"Осталось предметов: {result.RemainingCount}.");
        }

        parts.AddRange(result.Issues);

        return string.Join(" ", parts);
    }

    private static string FormatWeight(InventoryWeight weight)
    {
        var unit = string.IsNullOrWhiteSpace(weight.Unit) ? string.Empty : $" {weight.Unit}";
        var total = SheetNumber.Format(weight.Total);

        return weight.Capacity is { } capacity
            ? $"Ноша: {total} из {SheetNumber.Format(capacity)}{unit}"
            : $"Ноша: {total}{unit}";
    }

    private static string FormatMoney(IReadOnlyList<InventoryCurrencyTotal> money) =>
        money.Count == 0
            ? "Стоимость: не задана"
            : "Стоимость: " + string.Join(
                ", ",
                money.Select(total => $"{SheetNumber.Format(total.Amount)} {total.Currency}"));
}
