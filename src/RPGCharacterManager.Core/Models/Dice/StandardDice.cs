namespace RPGCharacterManager.Core.Models.Dice;

/// <summary>
/// Кубики, доступные в приложении всегда.
///
/// Перечень намеренно не хранится в базе данных: это не контент игровой системы,
/// а набор привычных костей, который ожидает увидеть любой игрок. Кубики, которых
/// здесь нет, пользователь создаёт сам — см. <see cref="Entities.DieType"/>.
/// </summary>
public static class StandardDice
{
    /// <summary>Количество граней встроенных кубиков в порядке возрастания.</summary>
    public static readonly IReadOnlyList<int> Sides = [2, 3, 4, 6, 8, 10, 12, 20, 100];
}
