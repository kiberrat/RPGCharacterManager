using RPGCharacterManager.Core.Models.Settings;

namespace RPGCharacterManager.Core.Abstractions.Presentation;

/// <summary>
/// Управление оформлением приложения: тема, акцентный цвет, размер шрифта и масштаб.
/// </summary>
public interface IThemeService
{
    /// <summary>Действующий режим оформления.</summary>
    ThemeMode CurrentTheme { get; }

    /// <summary>Действующий акцентный цвет.</summary>
    AccentColor CurrentAccent { get; }

    /// <summary>
    /// Применяет оформление в соответствии с настройками приложения.
    /// </summary>
    /// <param name="settings">Настройки, определяющие тему, акцент, шрифт и масштаб.</param>
    void Apply(AppSettings settings);
}
