using CommunityToolkit.Mvvm.ComponentModel;
using RPGCharacterManager.Core.Abstractions.Items;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>Ссылка на характеристику, навык или ресурс в локальном редакторе.</summary>
/// <param name="Id">Идентификатор объекта.</param>
/// <param name="Name">Отображаемое название.</param>
public sealed record LocalContentReferenceOption(Guid Id, string Name);

/// <summary>Вариант цели бонуса экипировки.</summary>
/// <param name="Target">Вид цели.</param>
/// <param name="Name">Отображаемое название.</param>
public sealed record LocalBonusTargetOption(BonusTargetKind Target, string Name);

/// <summary>Редактируемая строка бонуса авторской экипировки.</summary>
public sealed partial class LocalEquipmentBonusViewModel : ViewModelBase
{
    [ObservableProperty]
    private LocalBonusTargetOption? _selectedTarget;

    [ObservableProperty]
    private LocalContentReferenceOption? _selectedAttribute;

    [ObservableProperty]
    private LocalContentReferenceOption? _selectedResource;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _formula = "0";

    [ObservableProperty]
    private string _condition = string.Empty;

    /// <summary>Создаёт строку бонуса.</summary>
    /// <param name="targets">Доступные виды целей.</param>
    /// <param name="attributes">Характеристики персонажа.</param>
    /// <param name="resources">Ресурсы персонажа.</param>
    public LocalEquipmentBonusViewModel(
        IReadOnlyList<LocalBonusTargetOption> targets,
        IReadOnlyList<LocalContentReferenceOption> attributes,
        IReadOnlyList<LocalContentReferenceOption> resources)
    {
        TargetOptions = Guard.NotNull(targets);
        AttributeOptions = Guard.NotNull(attributes);
        ResourceOptions = Guard.NotNull(resources);
        _selectedTarget = TargetOptions.Count > 0 ? TargetOptions[0] : null;
    }

    /// <summary>Доступные виды целей.</summary>
    public IReadOnlyList<LocalBonusTargetOption> TargetOptions { get; }

    /// <summary>Доступные характеристики.</summary>
    public IReadOnlyList<LocalContentReferenceOption> AttributeOptions { get; }

    /// <summary>Доступные ресурсы.</summary>
    public IReadOnlyList<LocalContentReferenceOption> ResourceOptions { get; }

    /// <summary>Выбрана характеристика.</summary>
    public bool IsAttribute => SelectedTarget?.Target == BonusTargetKind.Attribute;

    /// <summary>Выбран максимум ресурса.</summary>
    public bool IsResource => SelectedTarget?.Target == BonusTargetKind.Resource;

    /// <summary>Требуется собственное имя переменной или тега.</summary>
    public bool IsNamed => SelectedTarget?.Target is BonusTargetKind.Variable or BonusTargetKind.Tag;

    /// <summary>Для цели требуется числовая формула.</summary>
    public bool IsFormulaVisible => SelectedTarget?.Target != BonusTargetKind.Tag;

    /// <summary>Преобразует строку в данные службы экипировки.</summary>
    public LocalEquipmentBonusDraft ToDraft() => new(
        SelectedTarget?.Target ?? BonusTargetKind.Attribute,
        IsAttribute ? SelectedAttribute?.Id : null,
        IsResource ? SelectedResource?.Id : null,
        IsNamed ? Name : null,
        IsFormulaVisible ? Formula : null,
        Condition);

    partial void OnSelectedTargetChanged(LocalBonusTargetOption? value)
    {
        SelectedAttribute = null;
        SelectedResource = null;
        OnPropertyChanged(nameof(IsAttribute));
        OnPropertyChanged(nameof(IsResource));
        OnPropertyChanged(nameof(IsNamed));
        OnPropertyChanged(nameof(IsFormulaVisible));
    }
}