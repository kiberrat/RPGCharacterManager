using RPGCharacterManager.Core.Abstractions.Dice;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Models.Dice;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Dice;

/// <summary>
/// Источник случайных значений, запоминающий каждый выпавший кубик.
///
/// Движок формул возвращает только итог выражения, а панель бросков должна показать
/// сами кости. Записывать их в движке значило бы научить его подсчёту бросков —
/// вместо этого запись ведёт источник значений: он один знает и количество граней,
/// и выпавшее число.
/// </summary>
public sealed class RecordingRandomSource : IRandomSource
{
    /// <summary>
    /// Наименьшая граница, с которой запрос считается броском кубика.
    /// Функция «Случайное» с другой нижней границей выдаёт произвольное число,
    /// и показывать его костью было бы неверно.
    /// </summary>
    private const int DieMinimum = 1;

    private readonly IRandomSource _inner;
    private readonly List<DieCast> _casts = [];

    /// <summary>
    /// Создаёт записывающий источник значений.
    /// </summary>
    /// <param name="inner">Источник, выполняющий сам бросок.</param>
    public RecordingRandomSource(IRandomSource inner) => _inner = Guard.NotNull(inner);

    /// <summary>Выпавшие кубики в порядке броска.</summary>
    public IReadOnlyList<DieCast> Casts => _casts;

    /// <inheritdoc />
    public int Next(int minimumInclusive, int maximumInclusive)
    {
        var value = _inner.Next(minimumInclusive, maximumInclusive);

        if (minimumInclusive == DieMinimum && maximumInclusive >= DieMesh.MinimumSides)
        {
            _casts.Add(new DieCast(maximumInclusive, value));
        }

        return value;
    }
}
