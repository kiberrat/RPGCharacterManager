using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Shared;

namespace RPGCharacterManager.Infrastructure.Settings;

/// <summary>
/// Расположение пользовательских данных в профиле текущего пользователя Windows.
///
/// Пользовательские данные намеренно отделены от каталога установки приложения:
/// удаление или переустановка программы не должны затрагивать данные пользователя.
/// </summary>
public sealed class AppPathService : IAppPathService
{
    /// <summary>
    /// Создаёт службу путей и вычисляет расположение всех каталогов.
    /// </summary>
    public AppPathService()
    {
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ApplicationConstants.DataFolderName);

        LogsDirectory = Path.Combine(DataDirectory, ApplicationConstants.LogsFolderName);
        BackupsDirectory = Path.Combine(DataDirectory, ApplicationConstants.BackupsFolderName);
        ContentDirectory = Path.Combine(DataDirectory, ApplicationConstants.ContentFolderName);
        DatabaseFilePath = Path.Combine(DataDirectory, ApplicationConstants.DatabaseFileName);
        SettingsFilePath = Path.Combine(DataDirectory, ApplicationConstants.SettingsFileName);
    }

    /// <inheritdoc />
    public string DataDirectory { get; }

    /// <inheritdoc />
    public string LogsDirectory { get; }

    /// <inheritdoc />
    public string BackupsDirectory { get; }

    /// <inheritdoc />
    public string ContentDirectory { get; }

    /// <inheritdoc />
    public string DatabaseFilePath { get; }

    /// <inheritdoc />
    public string SettingsFilePath { get; }

    /// <inheritdoc />
    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(ContentDirectory);
    }
}
