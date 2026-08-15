using RPGCharacterManager.Core.Models.Settings;

namespace RPGCharacterManager.Core.Events;

/// <summary>
/// Событие изменения пользовательских настроек.
/// Публикуется после успешного сохранения настроек.
/// </summary>
/// <param name="Settings">Актуальные настройки приложения.</param>
public sealed record SettingsChangedEvent(AppSettings Settings);
