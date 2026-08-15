namespace RPGCharacterManager.Core.Models.Settings;

/// <summary>
/// Режим оформления интерфейса.
/// </summary>
public enum ThemeMode
{
    /// <summary>Тёмная тема. Используется по умолчанию.</summary>
    Dark = 0,

    /// <summary>Светлая тема.</summary>
    Light = 1,

    /// <summary>Тема выбирается автоматически в соответствии с настройками Windows.</summary>
    System = 2,
}
