using System.Collections.ObjectModel;
using System.Globalization;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Изменение, которое эффект вносит в параметры персонажа.
/// </summary>
public sealed class EffectChangeViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт строку изменения.
    /// </summary>
    /// <param name="change">Изменение эффекта.</param>
    public EffectChangeViewModel(EffectChange change) => Change = Guard.NotNull(change);

    /// <summary>Изменение эффекта.</summary>
    public EffectChange Change { get; }

    /// <summary>Что изменяется и на сколько.</summary>
    public string Text => Math.Abs(Change.Value) < double.Epsilon
        ? Change.Description
        : $"{Change.Description}: {SheetNumber.Format(Change.Value)}";

    /// <summary>Условие изменения и причина, по которой оно не действует.</summary>
    public string Hint => string.Join(
        " • ",
        new[]
        {
            string.IsNullOrWhiteSpace(Change.Condition) ? null : $"при условии: {Change.Condition}",
            Change.IsApplied ? null : "условие не выполнено, изменение не действует",
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>Пояснение задано.</summary>
    public bool HasHint => !string.IsNullOrWhiteSpace(Hint);
}

/// <summary>
/// Эффект, действующий на персонажа.
/// </summary>
public sealed class ActiveEffectViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт строку эффекта.
    /// </summary>
    /// <param name="effect">Действующий эффект.</param>
    public ActiveEffectViewModel(ActiveEffect effect)
    {
        Effect = Guard.NotNull(effect);
        Changes = new ObservableCollection<EffectChangeViewModel>(
            effect.Changes.Select(change => new EffectChangeViewModel(change)));
    }

    /// <summary>Действующий эффект.</summary>
    public ActiveEffect Effect { get; }

    /// <summary>Идентификатор наложения.</summary>
    public Guid CharacterEffectId => Effect.CharacterEffectId;

    /// <summary>Название эффекта.</summary>
    public string Name => Effect.Name;

    /// <summary>Описание эффекта.</summary>
    public string? Description => Effect.Description;

    /// <summary>Описание задано.</summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(Effect.Description);

    /// <summary>Эффект положительный.</summary>
    public bool IsPositive => Effect.Tone == EffectTone.Positive;

    /// <summary>Эффект отрицательный.</summary>
    public bool IsNegative => Effect.Tone == EffectTone.Negative;

    /// <summary>Эффект нейтральный.</summary>
    public bool IsNeutral => Effect.Tone == EffectTone.Neutral;

    /// <summary>Категория, область и источник одной строкой.</summary>
    public string Subtitle => string.Join(
        " • ",
        new[] { Effect.Category, Effect.Area, Effect.Source }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>Пояснение к эффекту задано.</summary>
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    /// <summary>Количество наложений.</summary>
    public string StacksText => Effect.MaximumStacks is { } maximum
        ? $"×{Effect.Stacks.ToString(CultureInfo.CurrentCulture)} из {maximum.ToString(CultureInfo.CurrentCulture)}"
        : $"×{Effect.Stacks.ToString(CultureInfo.CurrentCulture)}";

    /// <summary>Эффект наложен несколько раз.</summary>
    public bool IsStacked => Effect.IsStacked;

    /// <summary>Эффект складывается сам с собой.</summary>
    public bool IsStackable => Effect.Stacking == EffectStacking.Sum;

    /// <summary>Оставшаяся длительность вместе с единицей.</summary>
    public string RemainingText => Effect.Remaining is { } remaining
        ? $"{SheetNumber.Format(remaining)} {Effect.DurationUnit}".TrimEnd()
        : "без срока";

    /// <summary>Эффект действует ограниченное время.</summary>
    public bool HasTimer => Effect.HasTimer;

    /// <summary>Условие досрочного прекращения.</summary>
    public string? EndCondition => Effect.EndCondition;

    /// <summary>Условие досрочного прекращения задано.</summary>
    public bool HasEndCondition => !string.IsNullOrWhiteSpace(Effect.EndCondition);

    /// <summary>Момент наложения.</summary>
    public string AppliedAtText =>
        Effect.AppliedAt.ToLocalTime().ToString("dd.MM HH:mm", CultureInfo.CurrentCulture);

    /// <summary>Что эффект изменяет.</summary>
    public ObservableCollection<EffectChangeViewModel> Changes { get; }

    /// <summary>Эффект что-то изменяет.</summary>
    public bool HasChanges => Changes.Count > 0;
}

/// <summary>
/// Единица длительности, по которой можно продвинуть время.
/// </summary>
public sealed class EffectTimerUnitViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт кнопку продвижения времени.
    /// </summary>
    /// <param name="unit">Единица длительности.</param>
    public EffectTimerUnitViewModel(EffectTimerUnit unit) => Unit = Guard.NotNull(unit);

    /// <summary>Единица длительности.</summary>
    public EffectTimerUnit Unit { get; }

    /// <summary>Название единицы.</summary>
    public string Name => Unit.Unit;

    /// <summary>Подпись кнопки продвижения времени.</summary>
    public string Text => $"+1 {Unit.Unit}";

    /// <summary>Сколько эффектов измеряется этой единицей.</summary>
    public string CountText => Unit.Count.ToString(CultureInfo.CurrentCulture);
}
