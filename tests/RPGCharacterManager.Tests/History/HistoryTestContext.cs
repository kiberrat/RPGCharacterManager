using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.History;
using RPGCharacterManager.Tests.Dice;

namespace RPGCharacterManager.Tests.History;

/// <summary>
/// Журнал событий, собранный поверх временной базы данных вместе с подсистемами,
/// которые в него пишут: персонажами, предметами и бросками.
/// </summary>
internal sealed class HistoryTestContext : IAsyncDisposable
{
    private readonly DiceTestContext _dice;

    private HistoryTestContext(DiceTestContext dice, HistoryService history)
    {
        _dice = dice;
        Service = history;
    }

    /// <summary>Служба журнала событий.</summary>
    public HistoryService Service { get; }

    /// <summary>Подсистема бросков.</summary>
    public DiceTestContext Dice => _dice;

    /// <summary>Подсистема персонажей и предметов.</summary>
    public Characters.CharacterTestContext Characters => _dice.Characters;

    /// <summary>
    /// Создаёт журнал событий с заданной последовательностью выпадающих значений.
    /// </summary>
    /// <param name="values">Значения, выпадающие на кубиках по очереди.</param>
    /// <returns>Готовое окружение теста.</returns>
    public static async Task<HistoryTestContext> CreateAsync(params int[] values)
    {
        const int HistoryLimit = 1000;

        var dice = await DiceTestContext.CreateAsync(HistoryLimit, values);

        var history = new HistoryService(
            dice.Characters.ContextFactory,
            dice.Service,
            NullLogger<HistoryService>.Instance);

        return new HistoryTestContext(dice, history);
    }

    public ValueTask DisposeAsync() => _dice.DisposeAsync();
}
