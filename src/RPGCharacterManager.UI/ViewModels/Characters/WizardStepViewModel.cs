using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Страница мастера создания персонажа.
///
/// Наследники соответствуют видам шагов, описанным <see cref="CharacterStepKind"/>.
/// Мастер не знает конкретных типов страниц: он получает список описаний и создаёт
/// подходящую страницу для каждого, поэтому новый вид шага не требует изменения мастера.
/// </summary>
public abstract partial class WizardStepViewModel : ViewModelBase
{
    private readonly Action _changed;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Со страницы можно уйти дальше по мастеру.
    /// Переопределяется страницами, где выбор обязателен (например, игровая
    /// система): пока он не сделан, переход вперёд заблокирован, а не молча
    /// приводит к пустым спискам на следующих страницах.
    /// </summary>
    [ObservableProperty]
    private bool _canLeave = true;

    /// <summary>
    /// Создаёт страницу мастера.
    /// </summary>
    /// <param name="definition">Описание шага.</param>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="builder">Мастер создания персонажа.</param>
    /// <param name="changed">Обратный вызов при изменении данных персонажа.</param>
    protected WizardStepViewModel(
        CharacterStepDefinition definition,
        CharacterDraft draft,
        ICharacterBuilderService builder,
        Action changed)
    {
        Definition = Guard.NotNull(definition);
        Draft = Guard.NotNull(draft);
        Builder = Guard.NotNull(builder);
        _changed = Guard.NotNull(changed);
    }

    /// <summary>Описание шага.</summary>
    public CharacterStepDefinition Definition { get; }

    /// <summary>Название шага.</summary>
    public string Title => Definition.Title;

    /// <summary>Пояснение к шагу.</summary>
    public string Description => Definition.Description;

    /// <summary>Создаваемый персонаж.</summary>
    protected CharacterDraft Draft { get; }

    /// <summary>Мастер создания персонажа.</summary>
    protected ICharacterBuilderService Builder { get; }

    /// <summary>
    /// Загружает данные страницы при переходе на неё.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    public virtual Task ActivateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Сообщает мастеру об изменении данных персонажа.
    /// </summary>
    protected void NotifyChanged() => _changed();
}

/// <summary>
/// Вариант выбора на странице мастера: раса, класс, черта, заклинание.
/// </summary>
public sealed partial class CharacterOptionViewModel : ViewModelBase
{
    private readonly Action<CharacterOptionViewModel>? _toggled;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Создаёт вариант выбора.
    /// </summary>
    /// <param name="option">Описание варианта.</param>
    /// <param name="isSelected">Вариант уже выбран.</param>
    /// <param name="toggled">Обратный вызов при изменении отметки.</param>
    public CharacterOptionViewModel(
        CharacterOption option,
        bool isSelected = false,
        Action<CharacterOptionViewModel>? toggled = null)
    {
        Option = Guard.NotNull(option);
        Details = new ObservableCollection<CharacterOptionDetail>(option.Details);

        // Значение записывается в поле напрямую: начальная отметка не должна
        // выглядеть как выбор пользователя и изменять состав персонажа.
        _isSelected = isSelected;
        _toggled = toggled;
    }

    /// <summary>Описание варианта.</summary>
    public CharacterOption Option { get; }

    /// <summary>Идентификатор объекта.</summary>
    public Guid Id => Option.Id;

    /// <summary>Название объекта.</summary>
    public string Name => Option.Name;

    /// <summary>Описание объекта.</summary>
    public string? Description => Option.Description;

    /// <summary>Описание задано.</summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(Option.Description);

    /// <summary>Объект доступен для выбора.</summary>
    public bool IsAvailable => Option.IsAvailable;

    /// <summary>Причина недоступности объекта.</summary>
    public string? UnavailableReason => Option.UnavailableReason;

    /// <summary>Причина недоступности задана.</summary>
    public bool HasUnavailableReason => !string.IsNullOrWhiteSpace(Option.UnavailableReason);

    /// <summary>Дополнительные сведения карточки.</summary>
    public ObservableCollection<CharacterOptionDetail> Details { get; }

    /// <summary>Карточка содержит дополнительные сведения.</summary>
    public bool HasDetails => Details.Count > 0;

    /// <summary>Дополнительные сведения одной строкой — для компактных списков выбора.</summary>
    public string DetailsText => string.Join(
        " • ",
        Details.Select(detail => $"{detail.Label}: {detail.Value}"));

    partial void OnIsSelectedChanged(bool value) => _toggled?.Invoke(this);
}

/// <summary>
/// Источник контента в списке источников мастера.
/// </summary>
public sealed partial class ContentSourceViewModel : ViewModelBase
{
    private readonly Action<ContentSourceViewModel>? _toggled;

    [ObservableProperty]
    private bool _isEnabled;

    /// <summary>
    /// Создаёт источник контента.
    /// </summary>
    /// <param name="source">Описание контент-пака.</param>
    /// <param name="isEnabled">Источник разрешён.</param>
    /// <param name="toggled">Обратный вызов при изменении отметки.</param>
    public ContentSourceViewModel(
        ContentSourceOption source,
        bool isEnabled,
        Action<ContentSourceViewModel>? toggled = null)
    {
        Source = Guard.NotNull(source);
        _isEnabled = isEnabled;
        _toggled = toggled;
    }

    /// <summary>Описание контент-пака.</summary>
    public ContentSourceOption Source { get; }

    /// <summary>Идентификатор контент-пака.</summary>
    public Guid Id => Source.Id;

    /// <summary>Название контент-пака вместе с версией.</summary>
    public string Name => $"{Source.Name} ({Source.Version})";

    /// <summary>Описание контент-пака.</summary>
    public string? Description => Source.Description;

    partial void OnIsEnabledChanged(bool value) => _toggled?.Invoke(this);
}
