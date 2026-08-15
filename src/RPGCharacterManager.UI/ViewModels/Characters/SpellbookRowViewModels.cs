using System.Collections.ObjectModel;
using System.Globalization;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Заклинание в книге персонажа.
/// </summary>
public sealed class SpellbookEntryViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт строку заклинания.
    /// </summary>
    /// <param name="entry">Заклинание книги.</param>
    public SpellbookEntryViewModel(SpellbookEntry entry) => Entry = Guard.NotNull(entry);

    /// <summary>Заклинание книги.</summary>
    public SpellbookEntry Entry { get; }

    /// <summary>Идентификатор записи книги заклинаний.</summary>
    public Guid CharacterSpellId => Entry.CharacterSpellId;

    /// <summary>Название заклинания.</summary>
    public string Name => Entry.Name;

    /// <summary>Описание заклинания.</summary>
    public string? Description => Entry.Description;

    /// <summary>Описание задано.</summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(Entry.Description);

    /// <summary>Уровень заклинания.</summary>
    public int Level => Entry.Level;

    /// <summary>Заклинание — кантрип.</summary>
    public bool IsCantrip => Entry.Level == 0;

    /// <summary>Школа, категория и время применения одной строкой.</summary>
    public string Subtitle => string.Join(
        " • ",
        new[] { Entry.School, Entry.Category, Entry.CastingTime }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>Пояснение к заклинанию задано.</summary>
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    /// <summary>Заклинание подготовлено.</summary>
    public bool IsPrepared => Entry.IsPrepared;

    /// <summary>Персонаж концентрируется на этом заклинании.</summary>
    public bool IsConcentrating => Entry.IsConcentrating;

    /// <summary>Заклинание требует концентрации.</summary>
    public bool RequiresConcentration => Entry.RequiresConcentration;

    /// <summary>Заклинание можно применить как ритуал.</summary>
    public bool IsRitual => Entry.IsRitual;

    /// <summary>Стоимость применения: «Мана: 3».</summary>
    public string? CostText => Entry.ResourceName is null
        ? null
        : $"{Entry.ResourceName}: {SheetNumber.Format(Entry.ResourceCost ?? 0)}";

    /// <summary>Стоимость применения задана.</summary>
    public bool HasCost => Entry.ResourceName is not null;

    /// <summary>Границы результата на базовом уровне.</summary>
    public string? ExpectedRange => Entry.ExpectedRange;

    /// <summary>Границы результата известны.</summary>
    public bool HasExpectedRange => !string.IsNullOrWhiteSpace(Entry.ExpectedRange);

    /// <summary>Дальность применения.</summary>
    public string? Range => Entry.Range;

    /// <summary>Дальность задана.</summary>
    public bool HasRange => !string.IsNullOrWhiteSpace(Entry.Range);

    /// <summary>Длительность действия.</summary>
    public string? Duration => Entry.Duration;

    /// <summary>Длительность задана.</summary>
    public bool HasDuration => !string.IsNullOrWhiteSpace(Entry.Duration);

    /// <summary>Компоненты применения.</summary>
    public string? Components => Entry.Components;

    /// <summary>Компоненты заданы.</summary>
    public bool HasComponents => !string.IsNullOrWhiteSpace(Entry.Components);

    /// <summary>Источник получения заклинания.</summary>
    public string? Source => Entry.Source;

    /// <summary>Источник задан.</summary>
    public bool HasSource => !string.IsNullOrWhiteSpace(Entry.Source);

    /// <summary>Количество применений.</summary>
    public string TimesUsedText => Entry.TimesUsed.ToString(CultureInfo.CurrentCulture);

    /// <summary>Заклинание применялось.</summary>
    public bool WasUsed => Entry.TimesUsed > 0;

    /// <summary>У заклинания есть формула усиления.</summary>
    public bool HasScaling => Entry.HasScaling;

    /// <summary>Заклинание можно применить прямо сейчас.</summary>
    public bool CanCast => Entry.CanCast;

    /// <summary>Применение невозможно, и причина известна.</summary>
    public bool HasBlockedReason => !string.IsNullOrWhiteSpace(Entry.BlockedReason);

    /// <summary>Причина, по которой применение невозможно.</summary>
    public string? BlockedReason => Entry.BlockedReason;
}

/// <summary>
/// Уровень книги заклинаний вместе с его заклинаниями.
/// </summary>
public sealed class SpellbookLevelViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт раздел уровня.
    /// </summary>
    /// <param name="level">Уровень книги заклинаний.</param>
    public SpellbookLevelViewModel(SpellbookLevel level)
    {
        Guard.NotNull(level);

        Title = level.Title;
        Spells = new ObservableCollection<SpellbookEntryViewModel>(
            level.Spells.Select(entry => new SpellbookEntryViewModel(entry)));
    }

    /// <summary>Название раздела: «Кантрипы», «1 уровень» и далее.</summary>
    public string Title { get; }

    /// <summary>Заклинания уровня.</summary>
    public ObservableCollection<SpellbookEntryViewModel> Spells { get; }
}

/// <summary>
/// Запись истории применения заклинаний.
/// </summary>
public sealed class SpellCastRecordViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт строку истории.
    /// </summary>
    /// <param name="record">Запись истории применения.</param>
    public SpellCastRecordViewModel(SpellCastRecord record) => Record = Guard.NotNull(record);

    /// <summary>Запись истории применения.</summary>
    public SpellCastRecord Record { get; }

    /// <summary>Что произошло.</summary>
    public string Description => Record.Description;

    /// <summary>Итог применения.</summary>
    public string? Value => Record.Value;

    /// <summary>Итог применения известен.</summary>
    public bool HasValue => !string.IsNullOrWhiteSpace(Record.Value);

    /// <summary>Момент применения по местному времени.</summary>
    public string CastAtText =>
        Record.CastAt.ToLocalTime().ToString("dd.MM HH:mm", CultureInfo.CurrentCulture);
}
