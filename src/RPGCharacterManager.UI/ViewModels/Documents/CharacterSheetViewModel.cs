using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Items;
using RPGCharacterManager.Core.Abstractions.Layouts;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;
using RPGCharacterManager.UI.ViewModels.Characters;
using RPGCharacterManager.UI.ViewModels.Content;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Тип пользовательского поля в списке выбора.
/// </summary>
/// <param name="DataType">Тип данных.</param>
/// <param name="DisplayName">Отображаемое название типа.</param>
public sealed record CustomFieldTypeOption(GameValueType DataType, string DisplayName);

/// <summary>Вид зависимости авторской способности.</summary>
public sealed record AbilityDependencyOption(
    string Id,
    string DisplayName,
    string Hint,
    bool RequiresValue);

/// <summary>
/// Документ «Лист персонажа».
///
/// Пользователь изменяет только исходные значения: базовые значения характеристик,
/// уровни владения навыками, состояние черт и описание персонажа. Итоговые значения,
/// модификаторы и максимумы ресурсов вычисляются формулами игровой системы при
/// каждом сохранении, поэтому лист не может разойтись с её правилами.
/// </summary>
public sealed partial class CharacterSheetViewModel : DocumentViewModelBase, ICharacterDocument, IDisposable
{
    private const string ProficiencyBonusSystemName = "бонус_мастерства";

    private readonly Guid _characterId;
    private readonly IDisposable _characterSubscription;
    private readonly ICharacterSheetService _sheets;
    private readonly ICharacterBuilderService _builder;
    private readonly IWeaponService _weapons;
    private readonly IEquipmentService _equipment;
    private readonly ILayoutService _layouts;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly IBackgroundTaskService _backgroundTasks;

    private CharacterSheet? _sheet;
    private CharacterAttributeValue? _proficiencyBonusValue;
    private bool _isLoading;
    private CancellationTokenSource? _manaSaveCancellation;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _proficiencyBonusText = string.Empty;

    [ObservableProperty]
    private bool _hasProficiencyBonus;

    [ObservableProperty]
    private bool _hasCustomProficiencyBonus;

    /// <summary>Описание режима вычисления бонуса мастерства.</summary>
    public string ProficiencyBonusMode => HasCustomProficiencyBonus
        ? "Используется авторское значение. Сохранение пересчитает все зависимые показатели."
        : "Значение вычисляется по уровню. Введите своё число, чтобы переопределить его.";

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isTraitPickerOpen;

    [ObservableProperty]
    private string _traitSearch = string.Empty;

    [ObservableProperty]
    private CharacterOptionViewModel? _selectedAvailableTrait;

    [ObservableProperty]
    private bool _isSkillPickerOpen;

    [ObservableProperty]
    private string _skillSearch = string.Empty;

    [ObservableProperty]
    private CharacterOptionViewModel? _selectedAvailableSkill;

    [ObservableProperty]
    private EquipmentSlotViewModel? _selectedEquipmentSlot;

    [ObservableProperty]
    private CharacterOptionViewModel? _selectedAvailableEquipment;

    [ObservableProperty]
    private bool _isWeaponPickerOpen;

    [ObservableProperty]
    private string _weaponSearch = string.Empty;

    [ObservableProperty]
    private CharacterOptionViewModel? _selectedAvailableWeapon;

    [ObservableProperty]
    private bool _isCustomFieldEditorOpen;

    [ObservableProperty]
    private string _newFieldName = string.Empty;

    [ObservableProperty]
    private string _newFieldCategory = string.Empty;

    [ObservableProperty]
    private CustomFieldTypeOption? _newFieldType;

    [ObservableProperty]
    private bool _isCustomAbilityEditorOpen;

    [ObservableProperty]
    private string _newAbilityName = string.Empty;

    [ObservableProperty]
    private string _newAbilityDescription = string.Empty;

    [ObservableProperty]
    private string _newAbilityCategory = "Авторские способности";

    [ObservableProperty]
    private string _newAbilityFormula = string.Empty;

    [ObservableProperty]
    private AbilityDependencyOption? _newAbilityDependency;

    [ObservableProperty]
    private string _newAbilityDependencyValue = string.Empty;

    [ObservableProperty]
    private string _newCurrencyName = string.Empty;

    [ObservableProperty]
    private decimal _newCurrencyAmount;

    [ObservableProperty]
    private decimal _manaCurrent;

    [ObservableProperty]
    private decimal? _manaMaximum;

    [ObservableProperty]
    private string _manaSaveStatus = "Сохраняется автоматически";

    /// <summary>Для выбранной зависимости требуется поле значения.</summary>
    public bool IsAbilityDependencyValueVisible => NewAbilityDependency?.RequiresValue == true;

    /// <summary>Подсказка для выбранного вида зависимости.</summary>
    public string AbilityDependencyHint => NewAbilityDependency?.Hint ?? string.Empty;

    /// <summary>
    /// Создаёт модель представления листа персонажа.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="sheets">Служба листа персонажа.</param>
    /// <param name="builder">Мастер создания персонажа: источник описания полей.</param>
    /// <param name="weapons">Служба оружия персонажа.</param>
    /// <param name="equipment">Служба экипировки персонажа.</param>
    /// <param name="inventory">Служба инвентаря персонажа.</param>
    /// <param name="spellbook">Служба книги заклинаний персонажа.</param>
    /// <param name="effects">Служба эффектов персонажа.</param>
    /// <param name="rests">Служба отдыха персонажа.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    /// <param name="notifications">Служба всплывающих уведомлений.</param>
    /// <param name="backgroundTasks">Служба фоновых задач.</param>
    /// <param name="eventBus">Шина событий приложения.</param>
    /// <param name="dispatcher">Диспетчер потока пользовательского интерфейса.</param>
    /// <param name="layouts">Служба макетов интерфейса.</param>
    public CharacterSheetViewModel(
        Guid characterId,
        ICharacterSheetService sheets,
        ICharacterBuilderService builder,
        IWeaponService weapons,
        IEquipmentService equipment,
        IInventoryService inventory,
        ISpellbookService spellbook,
        IEffectService effects,
        IRestService rests,
        IDialogService dialogs,
        INotificationService notifications,
        IBackgroundTaskService backgroundTasks,
        IEventBus eventBus,
        IUiDispatcher dispatcher,
        ILayoutService layouts)
        : base(CharacterShellContributor.SheetDocumentId, "Лист персонажа")
    {
        _characterId = characterId;
        _sheets = Guard.NotNull(sheets);
        _builder = Guard.NotNull(builder);
        _weapons = Guard.NotNull(weapons);
        _equipment = Guard.NotNull(equipment);
        _layouts = Guard.NotNull(layouts);
        _dialogs = Guard.NotNull(dialogs);
        _notifications = Guard.NotNull(notifications);
        _backgroundTasks = Guard.NotNull(backgroundTasks);

        // Инвентарь, книга заклинаний и эффекты ведут собственный отбор и набор
        // действий, поэтому вынесены в отдельные модели представления
        // и лишь размещаются на листе.
        Inventory = new InventoryViewModel(inventory, dialogs);
        Spellbook = new SpellbookViewModel(spellbook, dialogs);
        Effects = new EffectsViewModel(effects, dialogs);
        Rest = new RestViewModel(rests, dialogs);

        Guard.NotNull(eventBus);

        // Наложенный эффект, надетый предмет и применённое заклинание изменяют
        // характеристики и ресурсы. Разделы листа не знают друг о друге и связаны
        // только шиной, поэтому вычисленные значения перечитываются по событию.
        _characterSubscription = eventBus.SubscribeOnUiThread<CharacterChangedEvent>(
            dispatcher,
            OnCharacterChanged);

        FieldTypes =
        [
            new CustomFieldTypeOption(GameValueType.Text, "Текст"),
            new CustomFieldTypeOption(GameValueType.LongText, "Большой текст"),
            new CustomFieldTypeOption(GameValueType.WholeNumber, "Целое число"),
            new CustomFieldTypeOption(GameValueType.FractionalNumber, "Дробное число"),
            new CustomFieldTypeOption(GameValueType.Boolean, "Логическое значение"),
            new CustomFieldTypeOption(GameValueType.Date, "Дата"),
            new CustomFieldTypeOption(GameValueType.Formula, "Формула"),
        ];

        AbilityDependencyOptions =
        [
            new("none", "Без зависимости", "Способность доступна всегда.", false),
            new("level", "От уровня", "Введите минимальный уровень, например 5.", true),
            new("proficiency", "От бонуса мастерства", "Введите минимальный бонус мастерства, например 4.", true),
            new("class", "От текущего класса", "Способность доступна, пока у персонажа выбран нынешний класс.", false),
            new("subclass", "От текущего подкласса", "Способность доступна, пока выбран нынешний подкласс.", false),
            new("race", "От текущей расы", "Способность доступна, пока выбрана нынешняя раса.", false),
            new("custom", "Своё условие", "Введите условие, например: харизма >= 16 и уровень >= 5.", true),
        ];

        _newFieldType = FieldTypes[0];
        _newAbilityDependency = AbilityDependencyOptions[0];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _manaSaveCancellation?.Cancel();
        if (_manaSaveCancellation is not null)
        {
            _sheets.SaveManaAsync(_characterId, ManaCurrent, ManaMaximum).GetAwaiter().GetResult();
        }
        _manaSaveCancellation?.Dispose();
        _characterSubscription.Dispose();
    }

    /// <inheritdoc />
    public Guid CharacterId => _characterId;

    /// <inheritdoc />
    public string CharacterName => Title;

    /// <summary>Инвентарь персонажа.</summary>
    public InventoryViewModel Inventory { get; }

    /// <summary>Книга заклинаний персонажа.</summary>
    public SpellbookViewModel Spellbook { get; }

    /// <summary>Эффекты персонажа.</summary>
    public EffectsViewModel Effects { get; }

    /// <summary>Отдых персонажа.</summary>
    public RestViewModel Rest { get; }

    /// <summary>Разделы характеристик.</summary>
    public ObservableCollection<SheetAttributeGroupViewModel> AttributeGroups { get; } = [];

    /// <summary>Разделы навыков.</summary>
    public ObservableCollection<SheetSkillGroupViewModel> SkillGroups { get; } = [];

    /// <summary>Ресурсы персонажа.</summary>
    public ObservableCollection<SheetResourceRowViewModel> Resources { get; } = [];

    /// <summary>Полученные черты.</summary>
    public ObservableCollection<SheetTraitRowViewModel> Traits { get; } = [];

    /// <summary>Разделы способностей.</summary>
    public ObservableCollection<SheetAbilityGroupViewModel> AbilityGroups { get; } = [];

    /// <summary>Деньги персонажа.</summary>
    public ObservableCollection<CharacterCurrencyRowViewModel> Currencies { get; } = [];

    /// <summary>Оружие персонажа.</summary>
    public ObservableCollection<WeaponCardViewModel> Weapons { get; } = [];

    /// <summary>Слоты экипировки персонажа.</summary>
    public ObservableCollection<EquipmentSlotViewModel> EquipmentSlots { get; } = [];

    /// <summary>Разделы формы описания персонажа.</summary>
    public ObservableCollection<ContentFieldGroupViewModel> FieldGroups { get; } = [];

    /// <summary>Пользовательские поля персонажа.</summary>
    public ObservableCollection<SheetCustomFieldRowViewModel> CustomFields { get; } = [];

    /// <summary>Замечания, найденные при расчёте.</summary>
    public ObservableCollection<string> Issues { get; } = [];

    /// <summary>Черты, которые персонаж может получить.</summary>
    public ObservableCollection<CharacterOptionViewModel> AvailableTraits { get; } = [];

    /// <summary>Навыки, которыми персонаж может овладеть.</summary>
    public ObservableCollection<CharacterOptionViewModel> AvailableSkills { get; } = [];

    /// <summary>Оружие, которое персонаж может получить.</summary>
    public ObservableCollection<CharacterOptionViewModel> AvailableWeapons { get; } = [];

    /// <summary>Предметы, которые персонаж может надеть в выбранный слот.</summary>
    public ObservableCollection<CharacterOptionViewModel> AvailableEquipment { get; } = [];

    /// <summary>Типы пользовательских полей.</summary>
    public IReadOnlyList<CustomFieldTypeOption> FieldTypes { get; }

    /// <summary>Доступные виды зависимостей авторской способности.</summary>
    public IReadOnlyList<AbilityDependencyOption> AbilityDependencyOptions { get; }

    /// <summary>Характеристики отсутствуют.</summary>
    public bool HasNoAttributes => AttributeGroups.Count == 0;

    /// <summary>Навыки отсутствуют.</summary>
    public bool HasNoSkills => SkillGroups.Count == 0;

    /// <summary>Ресурсы отсутствуют.</summary>
    public bool HasNoResources => Resources.Count == 0;

    /// <summary>Черты отсутствуют.</summary>
    public bool HasNoTraits => Traits.Count == 0;

    /// <summary>Способности отсутствуют.</summary>
    public bool HasNoAbilities => AbilityGroups.Count == 0;

    /// <summary>Деньги ещё не добавлены.</summary>
    public bool HasNoCurrencies => Currencies.Count == 0;

    /// <summary>Оружие отсутствует.</summary>
    public bool HasNoWeapons => Weapons.Count == 0;

    /// <summary>Слоты экипировки не созданы.</summary>
    public bool HasNoEquipmentSlots => EquipmentSlots.Count == 0;

    /// <summary>Пользовательские поля отсутствуют.</summary>
    public bool HasNoCustomFields => CustomFields.Count == 0;

    /// <summary>Найдены замечания расчёта.</summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>Вкладки листа, собранные по применяемому макету.</summary>
    public ObservableCollection<SheetTabViewModel> Tabs { get; } = [];

    /// <inheritdoc />
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await ReloadLayoutAsync(cancellationToken).ConfigureAwait(true);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Перечитывает применяемый макет и пересобирает вкладки листа.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сборки вкладок.</returns>
    [RelayCommand]
    private async Task ReloadLayoutAsync(CancellationToken cancellationToken)
    {
        var result = await _layouts.GetCurrentAsync(cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Макет листа", result.Error!).ConfigureAwait(true);
            return;
        }

        Tabs.Clear();

        foreach (var tab in result.Value.Tabs)
        {
            Tabs.Add(new SheetTabViewModel(
                tab,
                [.. tab.Panels.Select(panel => new SheetPanelViewModel(panel, this))]));
        }
    }

    /// <inheritdoc />
    public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
    {
        if (!HasUnsavedChanges)
        {
            return true;
        }

        return await _dialogs.ShowConfirmationAsync(
                "Несохранённый лист",
                "В листе персонажа есть несохранённые изменения. Закрыть вкладку и потерять их?")
            .ConfigureAwait(true);
    }

    /// <summary>
    /// Перечитывает лист персонажа, отбрасывая несохранённые изменения.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    [RelayCommand]
    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            var result = await _sheets.LoadAsync(_characterId, cancellationToken).ConfigureAwait(true);

            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync("Лист персонажа", result.Error ?? "Неизвестная ошибка.")
                    .ConfigureAwait(true);

                return;
            }

            Fill(result.Value);

            await ReloadWeaponsAsync(cancellationToken).ConfigureAwait(true);
            await ReloadEquipmentAsync(cancellationToken).ConfigureAwait(true);
            await Inventory.InitializeAsync(_characterId, cancellationToken).ConfigureAwait(true);
            await Spellbook.InitializeAsync(_characterId, cancellationToken).ConfigureAwait(true);
            await Effects.InitializeAsync(_characterId, cancellationToken).ConfigureAwait(true);
            await Rest.InitializeAsync(_characterId, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Перечитывает вычисленные значения листа, когда персонажа изменил другой раздел.
    ///
    /// Несохранённые правки не трогаются: перечитывание отбросило бы введённое
    /// пользователем значение. Такой лист обновится при ближайшем сохранении,
    /// которое и так перечитывает расчёт.
    /// </summary>
    /// <param name="payload">Событие изменения персонажа.</param>
    private void OnCharacterChanged(CharacterChangedEvent payload)
    {
        if (payload.CharacterId != _characterId || HasUnsavedChanges || IsBusy)
        {
            return;
        }

        _ = RefreshCalculatedAsync();
    }

    /// <summary>
    /// Перечитывает лист персонажа, не трогая разделы, которые обновляют себя сами.
    /// </summary>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    private async Task RefreshCalculatedAsync()
    {
        var result = await _sheets.LoadAsync(_characterId).ConfigureAwait(true);

        if (result.IsSuccess)
        {
            Fill(result.Value);
        }
    }

    /// <summary>
    /// Сохраняет лист и пересчитывает персонажа.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_sheet is not { } sheet)
        {
            return;
        }

        ApplyFields();

        var values = CustomFields.ToDictionary(field => field.DefinitionId, field => field.GetValue());

        var result = await _backgroundTasks
            .RunAsync(
                "Сохранение листа персонажа",
                token => _sheets.SaveAsync(sheet.Character, values, token),
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Сохранение листа", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);

            return;
        }

        Fill(result.Value);

        // Изменение характеристик меняет бонус попадания и урон оружия,
        // переносимый вес и количество зарядов предметов, поэтому разделы
        // перечитываются вместе с листом.
        await ReloadWeaponsAsync(cancellationToken).ConfigureAwait(true);
        await ReloadEquipmentAsync(cancellationToken).ConfigureAwait(true);
        await Inventory.ReloadAsync(cancellationToken).ConfigureAwait(true);
        await Spellbook.ReloadAsync(cancellationToken).ConfigureAwait(true);
        await Effects.ReloadAsync(cancellationToken).ConfigureAwait(true);
        await Rest.ReloadAsync(cancellationToken).ConfigureAwait(true);

        _notifications.Show($"Лист персонажа «{result.Value.Character.Name}» сохранён", NotificationKind.Success);
    }

    /// <summary>
    /// Открывает или закрывает список черт, доступных персонажу.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки списка.</returns>
    [RelayCommand]
    private async Task ToggleTraitPickerAsync(CancellationToken cancellationToken)
    {
        IsTraitPickerOpen = !IsTraitPickerOpen;

        if (IsTraitPickerOpen)
        {
            await ReloadAvailableTraitsAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Добавляет персонажу выбранную черту.
    /// </summary>
    [RelayCommand]
    private void AddTrait()
    {
        if (_sheet is not { } sheet || SelectedAvailableTrait is not { IsAvailable: true } option)
        {
            return;
        }

        if (sheet.Character.Traits.Any(trait => trait.TraitId == option.Id))
        {
            _notifications.Show($"Черта «{option.Name}» уже получена", NotificationKind.Warning);
            return;
        }

        sheet.Character.Traits.Add(new CharacterTrait
        {
            CharacterId = sheet.Character.Id,
            TraitId = option.Id,
            Source = "Лист персонажа",
            IsActive = true,
        });

        MarkChanged();
        _notifications.Show($"Черта «{option.Name}» добавлена. Сохраните лист.", NotificationKind.Information);
    }

    /// <summary>
    /// Убирает у персонажа выбранную черту.
    /// </summary>
    /// <param name="row">Строка черты.</param>
    [RelayCommand]
    private void RemoveTrait(SheetTraitRowViewModel? row)
    {
        if (_sheet is not { } sheet || row is null)
        {
            return;
        }

        var stored = sheet.Character.Traits.FirstOrDefault(trait => trait.TraitId == row.TraitId);

        if (stored is not null)
        {
            sheet.Character.Traits.Remove(stored);
            Traits.Remove(row);

            OnPropertyChanged(nameof(HasNoTraits));
            MarkChanged();
        }
    }

    /// <summary>
    /// Открывает или закрывает список навыков, доступных персонажу.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки списка.</returns>
    [RelayCommand]
    private async Task ToggleSkillPickerAsync(CancellationToken cancellationToken)
    {
        IsSkillPickerOpen = !IsSkillPickerOpen;

        if (IsSkillPickerOpen)
        {
            await ReloadAvailableSkillsAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Добавляет персонажу владение выбранным навыком.
    /// </summary>
    [RelayCommand]
    private void AddSkill()
    {
        if (_sheet is not { } sheet || SelectedAvailableSkill is not { IsAvailable: true } option)
        {
            return;
        }

        if (sheet.Character.Skills.Any(skill => skill.SkillId == option.Id))
        {
            _notifications.Show($"Навык «{option.Name}» уже освоен", NotificationKind.Warning);
            return;
        }

        sheet.Character.Skills.Add(new CharacterSkill
        {
            CharacterId = sheet.Character.Id,
            SkillId = option.Id,
            ProficiencyLevel = 1,
        });

        MarkChanged();
        _notifications.Show($"Навык «{option.Name}» добавлен. Сохраните лист.", NotificationKind.Information);
    }

    /// <summary>
    /// Убирает у персонажа владение навыком.
    /// </summary>
    /// <param name="row">Строка навыка.</param>
    [RelayCommand]
    private void RemoveSkill(SheetSkillRowViewModel? row)
    {
        if (_sheet is not { } sheet || row is null)
        {
            return;
        }

        var stored = sheet.Character.Skills.FirstOrDefault(skill => skill.SkillId == row.Id);

        if (stored is null)
        {
            return;
        }

        sheet.Character.Skills.Remove(stored);

        foreach (var group in SkillGroups)
        {
            group.Rows.Remove(row);
        }

        MarkChanged();
    }

    /// <summary>
    /// Открывает или закрывает список оружия, доступного персонажу.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки списка.</returns>
    [RelayCommand]
    private async Task ToggleWeaponPickerAsync(CancellationToken cancellationToken)
    {
        IsWeaponPickerOpen = !IsWeaponPickerOpen;

        if (IsWeaponPickerOpen)
        {
            await ReloadAvailableWeaponsAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Выдаёт персонажу выбранное оружие.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после выдачи оружия.</returns>
    [RelayCommand]
    private async Task AddWeaponAsync(CancellationToken cancellationToken)
    {
        if (SelectedAvailableWeapon is not { IsAvailable: true } option)
        {
            return;
        }

        var result = await _weapons.AddAsync(_characterId, option.Id, cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Оружие", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);

            return;
        }

        await ReloadWeaponsAsync(cancellationToken).ConfigureAwait(true);

        _notifications.Show($"Оружие «{option.Name}» выдано", NotificationKind.Success);
    }

    /// <summary>
    /// Убирает оружие у персонажа.
    /// </summary>
    /// <param name="card">Карточка оружия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task RemoveWeaponAsync(WeaponCardViewModel? card, CancellationToken cancellationToken)
    {
        if (card is null)
        {
            return;
        }

        var confirmed = await _dialogs
            .ShowConfirmationAsync("Оружие", $"Убрать оружие «{card.Name}» у персонажа?")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var result = await _weapons.RemoveAsync(_characterId, card.InventoryItemId, cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Оружие", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);

            return;
        }

        await ReloadWeaponsAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Выполняет атаку оружием: бросок попадания, урон и расход боеприпасов.
    /// </summary>
    /// <param name="card">Карточка оружия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после атаки.</returns>
    [RelayCommand]
    private async Task AttackAsync(WeaponCardViewModel? card, CancellationToken cancellationToken)
    {
        if (card is null)
        {
            return;
        }

        var result = await _weapons.AttackAsync(_characterId, card.InventoryItemId, cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            card.LastResult = result.Error ?? "Неизвестная ошибка.";
            return;
        }

        // Атака расходует боеприпасы, поэтому карточки перечитываются:
        // результат уже сохранён и не зависит от кнопки «Сохранить».
        var description = result.Value.Description;

        await ReloadWeaponsAsync(cancellationToken).ConfigureAwait(true);

        ShowResult(card.InventoryItemId, description);
    }

    /// <summary>
    /// Перезаряжает оружие, перенося боеприпасы из запаса в магазин.
    /// </summary>
    /// <param name="card">Карточка оружия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после перезарядки.</returns>
    [RelayCommand]
    private async Task ReloadWeaponAsync(WeaponCardViewModel? card, CancellationToken cancellationToken)
    {
        if (card is null)
        {
            return;
        }

        var result = await _weapons.ReloadAsync(_characterId, card.InventoryItemId, cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            card.LastResult = result.Error ?? "Неизвестная ошибка.";
            return;
        }

        var description = result.Value.Description;

        await ReloadWeaponsAsync(cancellationToken).ConfigureAwait(true);

        ShowResult(card.InventoryItemId, description);
    }

    /// <summary>
    /// Записывает количество боеприпасов, имеющихся у персонажа.
    /// </summary>
    /// <param name="card">Карточка оружия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения.</returns>
    [RelayCommand]
    private async Task SaveAmmunitionReserveAsync(
        WeaponCardViewModel? card,
        CancellationToken cancellationToken)
    {
        if (card is null)
        {
            return;
        }

        if (card.GetReserve() is not { } reserve)
        {
            card.LastResult = "Запас боеприпасов задаётся целым числом не меньше нуля.";
            return;
        }

        var result = await _weapons
            .SetAmmunitionReserveAsync(_characterId, card.InventoryItemId, reserve, cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            card.LastResult = result.Error ?? "Неизвестная ошибка.";
            return;
        }

        await ReloadWeaponsAsync(cancellationToken).ConfigureAwait(true);

        ShowResult(card.InventoryItemId, "Запас боеприпасов изменён.");
    }

    /// <summary>
    /// Открывает список предметов, которые персонаж может надеть в слот.
    /// </summary>
    /// <param name="slot">Слот экипировки.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки списка.</returns>
    [RelayCommand]
    private async Task SelectEquipmentSlotAsync(
        EquipmentSlotViewModel? slot,
        CancellationToken cancellationToken)
    {
        // Повторный щелчок по тому же слоту закрывает список.
        SelectedEquipmentSlot = SelectedEquipmentSlot?.SlotId == slot?.SlotId ? null : slot;

        AvailableEquipment.Clear();

        if (SelectedEquipmentSlot is null)
        {
            return;
        }

        var page = await _equipment
            .GetAvailableItemsAsync(
                _characterId,
                SelectedEquipmentSlot.SlotId,
                search: null,
                includeUnavailable: true,
                cancellationToken)
            .ConfigureAwait(true);

        foreach (var option in page.Options)
        {
            AvailableEquipment.Add(new CharacterOptionViewModel(option));
        }
    }

    /// <summary>
    /// Надевает выбранный предмет и пересчитывает персонажа.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после надевания.</returns>
    [RelayCommand]
    private async Task EquipAsync(CancellationToken cancellationToken)
    {
        if (SelectedAvailableEquipment is not { IsAvailable: true } option)
        {
            return;
        }

        var result = await _equipment.EquipAsync(_characterId, option.Id, cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Экипировка", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);

            return;
        }

        SelectedEquipmentSlot = null;
        AvailableEquipment.Clear();

        // Бонусы предмета изменяют характеристики и ресурсы, поэтому перечитывается
        // весь лист, а не только раздел экипировки.
        await ReloadAsync(cancellationToken).ConfigureAwait(true);

        _notifications.Show($"Предмет «{option.Name}» надет", NotificationKind.Success);
    }

    /// <summary>
    /// Снимает предмет и пересчитывает персонажа.
    /// </summary>
    /// <param name="item">Надетый предмет.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после снятия.</returns>
    [RelayCommand]
    private async Task UnequipAsync(EquippedItemViewModel? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        var result = await _equipment.UnequipAsync(_characterId, item.InventoryItemId, cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Экипировка", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);

            return;
        }

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Открывает или закрывает форму создания пользовательского поля.
    /// </summary>
    [RelayCommand]
    private void ToggleCustomFieldEditor() => IsCustomFieldEditorOpen = !IsCustomFieldEditorOpen;

    /// <summary>
    /// Создаёт пользовательское поле персонажей.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после создания поля.</returns>
    [RelayCommand]
    private async Task CreateCustomFieldAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(NewFieldName))
        {
            await _dialogs.ShowErrorAsync("Пользовательское поле", "Не задано название поля.")
                .ConfigureAwait(true);

            return;
        }

        var definition = new PropertyDefinition
        {
            DisplayName = NewFieldName.Trim(),
            DataType = NewFieldType?.DataType ?? GameValueType.Text,
            Category = string.IsNullOrWhiteSpace(NewFieldCategory) ? null : NewFieldCategory.Trim(),
        };

        var result = await _sheets.SaveCustomFieldAsync(definition, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Пользовательское поле", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);

            return;
        }

        NewFieldName = string.Empty;
        NewFieldCategory = string.Empty;
        IsCustomFieldEditorOpen = false;

        _notifications.Show($"Поле «{definition.DisplayName}» создано", NotificationKind.Success);

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Удаляет пользовательское поле вместе со значениями всех персонажей.
    /// </summary>
    /// <param name="row">Строка поля.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task DeleteCustomFieldAsync(
        SheetCustomFieldRowViewModel? row,
        CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return;
        }

        var confirmed = await _dialogs
            .ShowConfirmationAsync(
                "Удаление поля",
                $"Удалить поле «{row.DisplayName}»? Его значения будут удалены у всех персонажей.")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var result = await _sheets.DeleteCustomFieldAsync(row.DefinitionId, cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Удаление поля", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);

            return;
        }

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private void ToggleCustomAbilityEditor() =>
        IsCustomAbilityEditorOpen = !IsCustomAbilityEditorOpen;

    [RelayCommand]
    private async Task CreateCustomAbilityAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(NewAbilityName))
        {
            await _dialogs.ShowErrorAsync("Авторская способность", "Введите название способности.")
                .ConfigureAwait(true);
            return;
        }

        var dependency = BuildAbilityDependency();
        if (dependency.Error is not null)
        {
            await _dialogs.ShowErrorAsync("Авторская способность", dependency.Error)
                .ConfigureAwait(true);
            return;
        }

        var ability = new CharacterCustomAbility
        {
            Name = NewAbilityName.Trim(),
            Description = NewAbilityDescription,
            Category = NewAbilityCategory,
            Formula = NewAbilityFormula,
            Requirements = dependency.Requirement,
            DependencyDescription = dependency.Description,
        };

        var result = await _sheets.SaveCustomAbilityAsync(_characterId, ability, cancellationToken)
            .ConfigureAwait(true);
        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Авторская способность", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);
            return;
        }

        NewAbilityName = string.Empty;
        NewAbilityDescription = string.Empty;
        NewAbilityCategory = "Авторские способности";
        NewAbilityFormula = string.Empty;
        NewAbilityDependencyValue = string.Empty;
        NewAbilityDependency = AbilityDependencyOptions[0];
        IsCustomAbilityEditorOpen = false;
        _notifications.Show($"Способность «{ability.Name}» добавлена", NotificationKind.Success);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteCustomAbilityAsync(
        SheetAbilityRowViewModel? row,
        CancellationToken cancellationToken)
    {
        if (row is null || !row.IsCustom)
        {
            return;
        }

        var confirmed = await _dialogs.ShowConfirmationAsync(
            "Удаление способности", $"Удалить авторскую способность «{row.Name}»?")
            .ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        var result = await _sheets.DeleteCustomAbilityAsync(_characterId, row.Ability.Id, cancellationToken)
            .ConfigureAwait(true);
        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Удаление способности", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);
            return;
        }

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AddCurrencyAsync(CancellationToken cancellationToken)
    {
        var currency = new CharacterCurrency { Name = NewCurrencyName, Amount = NewCurrencyAmount };
        var result = await _sheets.SaveCurrencyAsync(_characterId, currency, cancellationToken)
            .ConfigureAwait(true);
        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Деньги", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);
            return;
        }

        NewCurrencyName = string.Empty;
        NewCurrencyAmount = 0;
        _notifications.Show("Валюта добавлена", NotificationKind.Success);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveCurrencyAsync(CharacterCurrencyRowViewModel? row,
        CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return;
        }

        var result = await _sheets.SaveCurrencyAsync(_characterId, row.ToEntity(), cancellationToken)
            .ConfigureAwait(true);
        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Деньги", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);
            return;
        }

        _notifications.Show($"«{row.Name}» сохранено", NotificationKind.Success);
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteCurrencyAsync(CharacterCurrencyRowViewModel? row,
        CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return;
        }

        var confirmed = await _dialogs.ShowConfirmationAsync(
            "Удаление денег", $"Удалить строку «{row.Name}»?").ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        var result = await _sheets.DeleteCurrencyAsync(_characterId, row.Id, cancellationToken)
            .ConfigureAwait(true);
        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Деньги", result.Error ?? "Неизвестная ошибка.")
                .ConfigureAwait(true);
            return;
        }

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private void IncreaseMana() => ManaCurrent += 1;

    [RelayCommand]
    private void DecreaseMana() => ManaCurrent = Math.Max(0, ManaCurrent - 1);

    [RelayCommand]
    private void ClearManaMaximum() => ManaMaximum = null;

    partial void OnManaCurrentChanged(decimal value) => QueueManaSave();

    partial void OnManaMaximumChanged(decimal? value) => QueueManaSave();

    private void QueueManaSave()
    {
        if (_isLoading)
        {
            return;
        }

        _manaSaveCancellation?.Cancel();
        _manaSaveCancellation?.Dispose();
        _manaSaveCancellation = new CancellationTokenSource();
        ManaSaveStatus = "Сохранение…";
        _ = SaveManaAfterDelayAsync(_manaSaveCancellation.Token);
    }

    private async Task SaveManaAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(500, cancellationToken).ConfigureAwait(true);
            var result = await _sheets
                .SaveManaAsync(_characterId, ManaCurrent, ManaMaximum, cancellationToken)
                .ConfigureAwait(true);
            ManaSaveStatus = result.IsSuccess
                ? "Сохранено автоматически"
                : result.Error ?? "Не удалось сохранить ману";
        }
        catch (OperationCanceledException)
        {
            // Новое изменение отменяет только отложенную запись предыдущего значения.
        }
    }

    private (string? Requirement, string? Description, string? Error) BuildAbilityDependency()
    {
        var option = NewAbilityDependency ?? AbilityDependencyOptions[0];
        var value = NewAbilityDependencyValue.Trim();
        var character = _sheet?.Character;

        if (option.RequiresValue && string.IsNullOrWhiteSpace(value))
        {
            return (null, null, "Заполните значение зависимости.");
        }

        return option.Id switch
        {
            "none" => (null, "Без зависимости", null),
            "level" when int.TryParse(value, out var level) && level > 0 =>
                ($"уровень >= {level}", $"Минимальный уровень: {level}", null),
            "level" => (null, null, "Уровень должен быть целым положительным числом."),
            "proficiency" when SheetNumber.Parse(value) is { } bonus =>
                ($"бонус_мастерства >= {bonus.ToString(CultureInfo.InvariantCulture)}",
                    $"Бонус мастерства не ниже {SheetNumber.Format(bonus)}", null),
            "proficiency" => (null, null, "Введите числовой бонус мастерства."),
            "class" when character?.Class is { } characterClass =>
                ($"класс = \"{EscapeFormulaText(characterClass.SystemName)}\"",
                    $"Класс: {characterClass.Name}", null),
            "class" => (null, null, "Сначала выберите класс персонажа."),
            "subclass" when character?.Subclass is { } subclass =>
                ($"подкласс = \"{EscapeFormulaText(subclass.SystemName)}\"",
                    $"Подкласс: {subclass.Name}", null),
            "subclass" => (null, null, "Сначала выберите подкласс персонажа."),
            "race" when character?.Race is { } race =>
                ($"раса = \"{EscapeFormulaText(race.SystemName)}\"", $"Раса: {race.Name}", null),
            "race" => (null, null, "Сначала выберите расу персонажа."),
            "custom" => (value, $"Своё условие: {value}", null),
            _ => (null, null, "Неизвестный вид зависимости."),
        };
    }

    private static string EscapeFormulaText(string value) => value.Replace("\"", "", StringComparison.Ordinal);

    partial void OnNewAbilityDependencyChanged(AbilityDependencyOption? value)
    {
        OnPropertyChanged(nameof(IsAbilityDependencyValueVisible));
        OnPropertyChanged(nameof(AbilityDependencyHint));
    }

    partial void OnTraitSearchChanged(string value) =>
        _ = ReloadAvailableTraitsAsync(CancellationToken.None);

    partial void OnSkillSearchChanged(string value) =>
        _ = ReloadAvailableSkillsAsync(CancellationToken.None);

    partial void OnWeaponSearchChanged(string value) =>
        _ = ReloadAvailableWeaponsAsync(CancellationToken.None);

    partial void OnProficiencyBonusTextChanged(string value)
    {
        if (_isLoading || _proficiencyBonusValue is null || SheetNumber.Parse(value) is not { } parsed)
        {
            return;
        }

        _proficiencyBonusValue.OverrideValue = parsed;
        HasCustomProficiencyBonus = true;
        MarkChanged();
    }

    partial void OnHasCustomProficiencyBonusChanged(bool value) =>
        OnPropertyChanged(nameof(ProficiencyBonusMode));

    /// <summary>
    /// Возвращает бонус мастерства к автоматическому расчёту игровой системы.
    /// </summary>
    [RelayCommand]
    private async Task ResetProficiencyBonusAsync(CancellationToken cancellationToken)
    {
        if (_proficiencyBonusValue is null)
        {
            return;
        }

        _proficiencyBonusValue.OverrideValue = null;
        HasCustomProficiencyBonus = false;
        MarkChanged();
        await SaveAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Заполняет разделы листа по результату расчёта.
    /// </summary>
    /// <param name="sheet">Лист персонажа.</param>
    private void Fill(CharacterSheet sheet)
    {
        _isLoading = true;

        try
        {
            _sheet = sheet;
            Title = sheet.Character.Name;
            ManaCurrent = sheet.Character.Mana;
            ManaMaximum = sheet.Character.ManaMaximum;
            ManaSaveStatus = "Сохраняется автоматически";

            FillProficiencyBonus(sheet);
            FillAttributes(sheet);
            FillSkills(sheet);
            FillResources(sheet);
            FillTraits(sheet);
            FillAbilities(sheet);
            FillCurrencies(sheet);
            FillFields(sheet);
            FillCustomFields(sheet);
            FillIssues(sheet);

            Summary = BuildSummary(sheet.Character);
            HasUnsavedChanges = false;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void FillProficiencyBonus(CharacterSheet sheet)
    {
        var definition = sheet.Attributes.FirstOrDefault(attribute =>
            string.Equals(attribute.SystemName, ProficiencyBonusSystemName, StringComparison.OrdinalIgnoreCase));

        _proficiencyBonusValue = definition is null
            ? null
            : sheet.Character.Attributes.FirstOrDefault(value => value.AttributeId == definition.Id);

        HasProficiencyBonus = definition is not null && _proficiencyBonusValue is not null;
        HasCustomProficiencyBonus = _proficiencyBonusValue?.OverrideValue is not null;
        ProficiencyBonusText = definition is null
            ? string.Empty
            : SheetNumber.Format(_proficiencyBonusValue?.OverrideValue ?? definition.Value);
    }

    private void FillAttributes(CharacterSheet sheet)
    {
        var stored = sheet.Character.Attributes.ToDictionary(value => value.AttributeId);

        AttributeGroups.Clear();

        foreach (var group in sheet.Attributes
                     .Where(attribute => !attribute.IsHidden)
                     .GroupBy(attribute => attribute.Category))
        {
            var rows = group
                .Where(attribute => stored.ContainsKey(attribute.Id))
                .Select(attribute => new SheetAttributeRowViewModel(
                    attribute,
                    stored[attribute.Id],
                    MarkChanged));

            AttributeGroups.Add(new SheetAttributeGroupViewModel(group.Key, rows));
        }

        OnPropertyChanged(nameof(HasNoAttributes));
    }

    private void FillSkills(CharacterSheet sheet)
    {
        var stored = sheet.Character.Skills.ToDictionary(skill => skill.SkillId);

        SkillGroups.Clear();

        // Спасброски — те же проверки, что и навыки, поэтому они образуют
        // обычный раздел, но показываются первыми: игрок обращается к ним чаще.
        var groups = sheet.Skills
            .Where(skill => stored.ContainsKey(skill.Id))
            .GroupBy(skill => skill.Category)
            .OrderByDescending(group => string.Equals(
                group.Key,
                SheetCategories.SavingThrows,
                StringComparison.OrdinalIgnoreCase))
            .ThenBy(group => group.Key, StringComparer.CurrentCulture);

        foreach (var group in groups)
        {
            var rows = group.Select(skill => new SheetSkillRowViewModel(
                skill,
                stored[skill.Id],
                MarkChanged));

            SkillGroups.Add(new SheetSkillGroupViewModel(group.Key, rows));
        }

        OnPropertyChanged(nameof(HasNoSkills));
    }

    private void FillResources(CharacterSheet sheet)
    {
        var stored = sheet.Character.Resources.ToDictionary(resource => resource.ResourceId);

        Resources.Clear();

        foreach (var resource in sheet.Resources.Where(item => stored.ContainsKey(item.Id)))
        {
            Resources.Add(new SheetResourceRowViewModel(resource, stored[resource.Id], MarkChanged));
        }

        OnPropertyChanged(nameof(HasNoResources));
    }

    private void FillTraits(CharacterSheet sheet)
    {
        var stored = sheet.Character.Traits.ToDictionary(trait => trait.TraitId);

        Traits.Clear();

        foreach (var trait in sheet.Traits.Where(item => stored.ContainsKey(item.TraitId)))
        {
            Traits.Add(new SheetTraitRowViewModel(trait, stored[trait.TraitId], MarkChanged));
        }

        OnPropertyChanged(nameof(HasNoTraits));
    }

    private void FillAbilities(CharacterSheet sheet)
    {
        AbilityGroups.Clear();

        foreach (var group in sheet.Abilities.GroupBy(ability => ability.Category))
        {
            AbilityGroups.Add(new SheetAbilityGroupViewModel(
                group.Key,
                group.Select(ability => new SheetAbilityRowViewModel(ability))));
        }

        OnPropertyChanged(nameof(HasNoAbilities));
    }

    private void FillCurrencies(CharacterSheet sheet)
    {
        Currencies.Clear();
        foreach (var currency in sheet.Character.Currencies.OrderBy(item => item.Name))
        {
            Currencies.Add(new CharacterCurrencyRowViewModel(currency));
        }

        OnPropertyChanged(nameof(HasNoCurrencies));
    }

    /// <summary>
    /// Строит форму описания персонажа по тем же полям, что использует мастер создания.
    /// Собственное поле игровой системы появляется и в мастере, и на листе.
    /// </summary>
    /// <param name="sheet">Лист персонажа.</param>
    private void FillFields(CharacterSheet sheet)
    {
        FieldGroups.Clear();

        var fields = _builder.Steps
            .Where(step => step.Kind == CharacterStepKind.Fields)
            .SelectMany(step => step.Fields);

        foreach (var group in fields.GroupBy(field => field.Group))
        {
            var rows = group.Select(field => new ContentFieldViewModel(
                field,
                sheet.Character,
                [],
                MarkChanged));

            FieldGroups.Add(new ContentFieldGroupViewModel(group.Key, rows));
        }
    }

    private void FillCustomFields(CharacterSheet sheet)
    {
        CustomFields.Clear();

        foreach (var field in sheet.CustomFields)
        {
            CustomFields.Add(new SheetCustomFieldRowViewModel(field, MarkChanged));
        }

        OnPropertyChanged(nameof(HasNoCustomFields));
    }

    private void FillIssues(CharacterSheet sheet)
    {
        Issues.Clear();

        foreach (var issue in sheet.Issues)
        {
            Issues.Add(issue.Message);
        }

        OnPropertyChanged(nameof(HasIssues));
    }

    /// <summary>
    /// Перечитывает оружие персонажа вместе с вычисленными боевыми значениями.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    private async Task ReloadWeaponsAsync(CancellationToken cancellationToken)
    {
        var result = await _weapons.GetWeaponsAsync(_characterId, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            return;
        }

        Weapons.Clear();

        foreach (var weapon in result.Value)
        {
            Weapons.Add(new WeaponCardViewModel(weapon));
        }

        OnPropertyChanged(nameof(HasNoWeapons));
    }

    /// <summary>
    /// Перечитывает слоты экипировки вместе с вычисленными бонусами надетых предметов.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки.</returns>
    private async Task ReloadEquipmentAsync(CancellationToken cancellationToken)
    {
        var result = await _equipment.GetSlotsAsync(_characterId, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            return;
        }

        EquipmentSlots.Clear();

        foreach (var slot in result.Value)
        {
            EquipmentSlots.Add(new EquipmentSlotViewModel(slot));
        }

        OnPropertyChanged(nameof(HasNoEquipmentSlots));
    }

    /// <summary>
    /// Показывает результат действия в карточке оружия, пережившей перечитывание списка.
    /// </summary>
    /// <param name="inventoryItemId">Идентификатор записи инвентаря.</param>
    /// <param name="description">Описание произошедшего.</param>
    private void ShowResult(Guid inventoryItemId, string description)
    {
        var card = Weapons.FirstOrDefault(item => item.InventoryItemId == inventoryItemId);

        if (card is not null)
        {
            card.LastResult = description;
        }
        else
        {
            _notifications.Show(description, NotificationKind.Information);
        }
    }

    private async Task ReloadAvailableWeaponsAsync(CancellationToken cancellationToken)
    {
        var page = await _weapons
            .GetAvailableWeaponsAsync(_characterId, WeaponSearch, includeUnavailable: true, cancellationToken)
            .ConfigureAwait(true);

        // Одно и то же оружие может быть выдано несколько раз — например, пара кинжалов,
        // поэтому уже полученное из списка не исключается.
        Replace(AvailableWeapons, page, []);
    }

    private async Task ReloadAvailableTraitsAsync(CancellationToken cancellationToken)
    {
        if (_sheet is not { } sheet)
        {
            return;
        }

        var page = await _sheets
            .GetAvailableTraitsAsync(sheet.Character, TraitSearch, includeUnavailable: true, cancellationToken)
            .ConfigureAwait(true);

        Replace(AvailableTraits, page, sheet.Character.Traits.Select(trait => trait.TraitId));
    }

    private async Task ReloadAvailableSkillsAsync(CancellationToken cancellationToken)
    {
        if (_sheet is not { } sheet)
        {
            return;
        }

        var page = await _sheets
            .GetAvailableSkillsAsync(sheet.Character, SkillSearch, includeUnavailable: true, cancellationToken)
            .ConfigureAwait(true);

        Replace(AvailableSkills, page, sheet.Character.Skills.Select(skill => skill.SkillId));
    }

    /// <summary>
    /// Заменяет содержимое списка выбора, исключая уже полученные объекты.
    /// </summary>
    /// <param name="target">Изменяемый список.</param>
    /// <param name="page">Страница вариантов.</param>
    /// <param name="taken">Идентификаторы уже полученных объектов.</param>
    private static void Replace(
        ObservableCollection<CharacterOptionViewModel> target,
        CharacterOptionPage page,
        IEnumerable<Guid> taken)
    {
        var owned = taken.ToHashSet();

        target.Clear();

        foreach (var option in page.Options.Where(option => !owned.Contains(option.Id)))
        {
            target.Add(new CharacterOptionViewModel(option));
        }
    }

    /// <summary>
    /// Переносит значения полей формы в персонажа.
    /// </summary>
    private void ApplyFields()
    {
        foreach (var field in FieldGroups.SelectMany(group => group.Fields))
        {
            field.TryApply(out _);
        }
    }

    private void MarkChanged()
    {
        if (!_isLoading)
        {
            HasUnsavedChanges = true;
        }
    }

    private static string BuildSummary(Character character)
    {
        var parts = new List<string>
        {
            $"Уровень {character.Level.ToString(CultureInfo.CurrentCulture)}",
        };

        if (character.Race is { } race)
        {
            parts.Add(race.Name);
        }

        if (character.Class is { } characterClass)
        {
            parts.Add(characterClass.Name);
        }

        if (character.Subclass is { } subclass)
        {
            parts.Add(subclass.Name);
        }

        if (character.Background is { } background)
        {
            parts.Add(background.Name);
        }

        return string.Join(", ", parts);
    }
}
