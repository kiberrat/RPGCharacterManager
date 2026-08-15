using System.ComponentModel;

namespace RPGCharacterManager.Core.Abstractions.Presentation;

/// <summary>
/// Состояние сохранения пользовательских данных.
/// </summary>
public enum SaveState
{
    /// <summary>Несохранённых изменений нет.</summary>
    Saved = 0,

    /// <summary>Есть несохранённые изменения.</summary>
    Modified = 1,

    /// <summary>Выполняется сохранение.</summary>
    Saving = 2,

    /// <summary>Последняя попытка сохранения завершилась ошибкой.</summary>
    Failed = 3,
}

/// <summary>
/// Сведения, отображаемые в строке состояния главного окна.
/// Подсистемы публикуют сюда свой контекст, не обращаясь к интерфейсу напрямую.
/// </summary>
public interface IApplicationStatusService : INotifyPropertyChanged
{
    /// <summary>Имя текущего персонажа или <see langword="null"/>, если персонаж не выбран.</summary>
    string? CurrentCharacterName { get; set; }

    /// <summary>Название текущей игровой системы или <see langword="null"/>, если она не выбрана.</summary>
    string? CurrentGameSystemName { get; set; }

    /// <summary>Состояние сохранения пользовательских данных.</summary>
    SaveState SaveState { get; set; }
}
