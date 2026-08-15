using RPGCharacterManager.Core.Models.Settings;

namespace RPGCharacterManager.Core.Abstractions.Infrastructure;

/// <summary>
/// Управление пользовательскими настройками приложения.
/// </summary>
public interface ISettingsService
{
    /// <summary>Текущие настройки. Изменять напрямую запрещено, используйте <see cref="UpdateAsync"/>.</summary>
    AppSettings Current { get; }

    /// <summary>
    /// Загружает настройки из хранилища. При отсутствии файла применяются значения по умолчанию.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после загрузки настроек.</returns>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Изменяет настройки, сохраняет их и публикует <see cref="Events.SettingsChangedEvent"/>.
    /// </summary>
    /// <param name="modify">Действие, применяющее изменения к копии текущих настроек.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после сохранения настроек.</returns>
    Task UpdateAsync(Action<AppSettings> modify, CancellationToken cancellationToken = default);
}
