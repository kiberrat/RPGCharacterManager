using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Data;

/// <summary>
/// Резервное копирование и восстановление базы данных.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Создаёт резервную копию базы данных.
    /// </summary>
    /// <param name="comment">Примечание пользователя к копии.</param>
    /// <param name="isAutomatic">Копия создаётся по расписанию, а не по команде пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сведения о созданной копии либо описание ошибки.</returns>
    Task<Result<BackupRecord>> CreateBackupAsync(
        string? comment = null,
        bool isAutomatic = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает список доступных резервных копий, начиная с самой новой.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список резервных копий.</returns>
    Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Восстанавливает базу данных из резервной копии.
    ///
    /// Перед восстановлением создаётся копия текущего состояния, поэтому операция
    /// обратима даже при выборе неверного файла.
    /// </summary>
    /// <param name="backupFilePath">Полный путь к файлу резервной копии.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат восстановления.</returns>
    Task<Result> RestoreAsync(string backupFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет резервные копии, созданные ранее указанного срока хранения.
    /// </summary>
    /// <param name="retentionPeriod">Срок хранения копий.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество удалённых копий.</returns>
    Task<int> RemoveObsoleteBackupsAsync(
        TimeSpan retentionPeriod,
        CancellationToken cancellationToken = default);
}
