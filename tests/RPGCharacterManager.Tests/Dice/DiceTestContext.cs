using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.Settings;
using RPGCharacterManager.Dice;
using RPGCharacterManager.Tests.Characters;

namespace RPGCharacterManager.Tests.Dice;

/// <summary>
/// Источник случайных значений, выдающий заранее заданную последовательность.
///
/// Броску с преимуществом нужны два разных исхода, поэтому постоянного источника
/// недостаточно: значения выдаются по очереди и повторяются по кругу.
/// </summary>
internal sealed class SequenceRandomSource : IRandomSource
{
    private readonly IReadOnlyList<int> _values;
    private int _position;

    public SequenceRandomSource(params int[] values) => _values = values;

    /// <summary>Количество выданных значений.</summary>
    public int Count => _position;

    public int Next(int minimumInclusive, int maximumInclusive)
    {
        var value = _values[_position % _values.Count];
        _position++;

        return Math.Clamp(value, minimumInclusive, maximumInclusive);
    }
}

/// <summary>
/// Служба настроек с заданными значениями.
/// </summary>
internal sealed class FixedSettingsService : ISettingsService
{
    public FixedSettingsService(AppSettings settings) => Current = settings;

    public AppSettings Current { get; }

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdateAsync(Action<AppSettings> modify, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modify);
        modify(Current);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Подсистема бросков, собранная поверх временной базы данных.
/// </summary>
internal sealed class DiceTestContext : IAsyncDisposable
{
    private readonly CharacterTestContext _characters;

    private DiceTestContext(CharacterTestContext characters, DiceService dice, SequenceRandomSource random)
    {
        _characters = characters;
        Service = dice;
        Random = random;
    }

    /// <summary>Служба бросков.</summary>
    public DiceService Service { get; }

    /// <summary>Источник значений кубиков.</summary>
    public SequenceRandomSource Random { get; }

    /// <summary>Подсистема персонажей, разделяющая базу данных со службой бросков.</summary>
    public CharacterTestContext Characters => _characters;

    /// <summary>
    /// Создаёт подсистему бросков с заданной последовательностью выпадающих значений.
    /// </summary>
    /// <param name="historyLimit">Предел размера журнала бросков.</param>
    /// <param name="values">Значения, выпадающие на кубиках по очереди.</param>
    /// <returns>Готовое окружение теста.</returns>
    public static async Task<DiceTestContext> CreateAsync(int historyLimit, params int[] values)
    {
        var characters = await CharacterTestContext.CreateAsync();
        var random = new SequenceRandomSource(values.Length > 0 ? values : [1]);

        var settings = new FixedSettingsService(new AppSettings { DiceHistoryLimit = historyLimit });

        var dice = new DiceService(
            characters.ContextFactory,
            characters.Builder,
            characters.Formulas,
            random,
            settings,
            NullLogger<DiceService>.Instance);

        return new DiceTestContext(characters, dice, random);
    }

    /// <summary>
    /// Сохраняет пользовательский кубик.
    /// </summary>
    /// <param name="name">Название кубика.</param>
    /// <param name="sides">Количество граней.</param>
    /// <returns>Сохранённый кубик.</returns>
    public async Task<DieType> AddDieAsync(string name, int sides)
    {
        var die = new DieType
        {
            Name = name,
            SystemName = name,
            Sides = sides,
        };

        await _characters.AddAsync(die);

        return die;
    }

    public ValueTask DisposeAsync() => _characters.DisposeAsync();
}
