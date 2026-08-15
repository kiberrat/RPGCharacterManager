using System.Globalization;
using Avalonia.Media;
using RPGCharacterManager.Core.Abstractions.Dice;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.ViewModels.Characters;

namespace RPGCharacterManager.UI.ViewModels.Dice;

/// <summary>
/// Кнопка кубика в панели бросков.
/// </summary>
public sealed class DieButtonViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт кнопку кубика.
    /// </summary>
    /// <param name="die">Описание кубика.</param>
    public DieButtonViewModel(DieDefinition die)
    {
        Die = Guard.NotNull(die);

        // Имя свойства совпадает с именем типа, поэтому вызов уточнён пространством имён.
        if (!string.IsNullOrWhiteSpace(die.Color) && Avalonia.Media.Color.TryParse(die.Color, out var parsed))
        {
            Color = parsed;
        }
    }

    /// <summary>Описание кубика.</summary>
    public DieDefinition Die { get; }

    /// <summary>Цвет кубика, заданный пользователем.</summary>
    public Color? Color { get; }

    /// <summary>Надпись на кнопке.</summary>
    public string Caption => Die.IsCustom ? Die.Name : Die.Notation;

    /// <summary>Пояснение к кнопке.</summary>
    public string Hint => Die.IsCustom
        ? $"{Die.Name}: {Die.Sides} граней"
        : $"Бросить {Die.Notation}";

    /// <summary>У кубика задан свой цвет.</summary>
    public bool HasColor => Color is not null;
}

/// <summary>
/// Запись журнала бросков или любимый бросок.
/// </summary>
public sealed class RollRowViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт строку броска.
    /// </summary>
    /// <param name="outcome">Результат броска.</param>
    public RollRowViewModel(RollOutcome outcome) => Outcome = Guard.NotNull(outcome);

    /// <summary>Результат броска.</summary>
    public RollOutcome Outcome { get; }

    /// <summary>Идентификатор записи журнала.</summary>
    public Guid Id => Outcome.Id;

    /// <summary>Название броска либо его выражение.</summary>
    public string Title => string.IsNullOrWhiteSpace(Outcome.Title) ? Outcome.Expression : Outcome.Title;

    /// <summary>Выражение броска.</summary>
    public string Expression => Outcome.Expression;

    /// <summary>Итог броска.</summary>
    public string Total => SheetNumber.Format(Outcome.Total);

    /// <summary>Время броска.</summary>
    public string Time => Outcome.Timestamp.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);

    /// <summary>Бросок отмечен как любимый.</summary>
    public bool IsFavorite => Outcome.IsFavorite;

    /// <summary>Действие, которое выполнит кнопка отметки.</summary>
    public string FavoriteAction => IsFavorite ? "Убрать из любимых" : "В любимые";

    /// <summary>Способ броска и выпавшие кости одной строкой.</summary>
    public string Details => string.Join(
        " • ",
        new[] { DiceText.Mode(Outcome.Mode), DiceText.Dice(Outcome) }
            .Where(part => !string.IsNullOrEmpty(part)));

    /// <summary>Подробности броска заданы.</summary>
    public bool HasDetails => Details.Length > 0;
}

/// <summary>
/// Тексты, описывающие бросок.
/// Собраны в одном месте, чтобы журнал, любимые броски и итог последнего броска
/// описывали одно и то же одинаково.
/// </summary>
internal static class DiceText
{
    /// <summary>
    /// Возвращает название способа броска.
    /// </summary>
    /// <param name="mode">Способ броска.</param>
    /// <returns>Название способа либо пустая строка для обычного броска.</returns>
    public static string Mode(RollMode mode) => mode switch
    {
        RollMode.Advantage => "преимущество",
        RollMode.Disadvantage => "помеха",
        _ => string.Empty,
    };

    /// <summary>
    /// Перечисляет выпавшие кости броска.
    ///
    /// При преимуществе и помехе показаны обе попытки: игрок должен видеть,
    /// от чего именно его избавило преимущество.
    /// </summary>
    /// <param name="outcome">Результат броска.</param>
    /// <returns>Перечисление костей.</returns>
    public static string Dice(RollOutcome outcome)
    {
        Guard.NotNull(outcome);

        if (outcome.Attempts.Count == 0)
        {
            return string.Empty;
        }

        if (outcome.Attempts.Count == 1)
        {
            return Describe(outcome.Attempts[0]);
        }

        return string.Join(
            " → ",
            outcome.Attempts.Select(attempt => attempt.IsChosen
                ? $"[{SheetNumber.Format(attempt.Total)}]"
                : SheetNumber.Format(attempt.Total)));
    }

    /// <summary>
    /// Перечисляет кости одной попытки.
    /// </summary>
    /// <param name="attempt">Попытка броска.</param>
    /// <returns>Перечисление костей.</returns>
    private static string Describe(RollAttempt attempt) =>
        attempt.Dice.Count == 0
            ? string.Empty
            : string.Join(
                ", ",
                attempt.Dice.Select(cast =>
                    $"{DiceNotation.Die(cast.Sides)}: {cast.Value.ToString(CultureInfo.CurrentCulture)}"));
}
