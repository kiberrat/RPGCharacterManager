using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Items;

/// <summary>
/// Ноша персонажа, посчитанная по дереву вместилищ.
///
/// Вместилище передаёт носителю не весь вес содержимого, а его долю: обычный мешок —
/// целиком, магическая сумка — часть, безразмерная — ничего. Доля берётся у самого
/// предмета, поэтому приложение не содержит перечня видов вместилищ.
/// </summary>
internal sealed class InventoryWeights
{
    private readonly Dictionary<Guid, List<InventoryItem>> _children = [];
    private readonly Dictionary<Guid, double> _carried = [];
    private readonly Dictionary<Guid, double> _content = [];

    /// <summary>Записи, обрабатываемые в текущий момент: защита от закольцованных вместилищ.</summary>
    private readonly HashSet<Guid> _visiting = [];

    /// <summary>
    /// Раскладывает записи инвентаря по дереву вместилищ.
    /// </summary>
    /// <param name="records">Записи инвентаря персонажа.</param>
    public InventoryWeights(IEnumerable<InventoryItem> records)
    {
        var all = records.ToList();
        var known = all.Select(record => record.Id).ToHashSet();

        foreach (var record in all)
        {
            // Запись, вместилище которой не найдено, считается лежащей отдельно:
            // потерять предмет из-за удалённой сумки нельзя.
            var container = record.ContainerId is { } id && known.Contains(id) ? id : (Guid?)null;

            if (container is { } parent)
            {
                if (!_children.TryGetValue(parent, out var list))
                {
                    list = [];
                    _children[parent] = list;
                }

                list.Add(record);
            }
        }

        Roots = all
            .Where(record => record.ContainerId is not { } id || !known.Contains(id))
            .ToList();

        Total = Roots.Sum(Carried);
    }

    /// <summary>Записи, лежащие вне вместилищ.</summary>
    public IReadOnlyList<InventoryItem> Roots { get; }

    /// <summary>Суммарный вес имущества, отнесённый на носителя.</summary>
    public double Total { get; }

    /// <summary>
    /// Возвращает записи, лежащие внутри указанного вместилища.
    /// </summary>
    /// <param name="containerId">Идентификатор записи вместилища.</param>
    /// <returns>Вложенные записи.</returns>
    public IReadOnlyList<InventoryItem> ChildrenOf(Guid containerId) =>
        _children.TryGetValue(containerId, out var list) ? list : [];

    /// <summary>
    /// Возвращает вес записи, отнесённый на носителя:
    /// собственный вес предметов вместе с долей веса содержимого.
    /// </summary>
    /// <param name="record">Запись инвентаря.</param>
    /// <returns>Вес записи.</returns>
    public double Carried(InventoryItem record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (_carried.TryGetValue(record.Id, out var cached))
        {
            return cached;
        }

        var factor = record.Item?.IsContainer == true ? record.Item.ContentWeightFactor : 1;
        var weight = OwnWeight(record) + (Content(record) * factor);

        _carried[record.Id] = weight;
        return weight;
    }

    /// <summary>
    /// Возвращает вес содержимого вместилища до его облегчения.
    /// Именно этот вес сравнивается с вместимостью.
    /// </summary>
    /// <param name="record">Запись инвентаря.</param>
    /// <returns>Вес содержимого.</returns>
    public double Content(InventoryItem record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (_content.TryGetValue(record.Id, out var cached))
        {
            return cached;
        }

        // Закольцованные вместилища невозможно создать через службу, но повреждённые
        // данные не должны приводить к бесконечной рекурсии.
        if (!_visiting.Add(record.Id))
        {
            return 0;
        }

        var weight = ChildrenOf(record.Id).Sum(Carried);

        _visiting.Remove(record.Id);
        _content[record.Id] = weight;

        return weight;
    }

    /// <summary>
    /// Возвращает собственный вес записи без содержимого.
    /// </summary>
    /// <param name="record">Запись инвентаря.</param>
    /// <returns>Вес предметов записи.</returns>
    public static double OwnWeight(InventoryItem record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return (record.Item?.Weight ?? 0) * record.Count;
    }
}
