namespace RPGCharacterManager.Core.Abstractions.Infrastructure;

/// <summary>
/// Пути к каталогам и файлам пользовательских данных приложения.
/// Все подсистемы обязаны получать расположение файлов только через этот сервис,
/// чтобы пользовательские данные хранились отдельно от файлов приложения.
/// </summary>
public interface IAppPathService
{
    /// <summary>Корневой каталог пользовательских данных.</summary>
    string DataDirectory { get; }

    /// <summary>Каталог журналов работы приложения.</summary>
    string LogsDirectory { get; }

    /// <summary>Каталог резервных копий базы данных.</summary>
    string BackupsDirectory { get; }

    /// <summary>Каталог пользовательского контента: контент-паки, плагины, изображения.</summary>
    string ContentDirectory { get; }

    /// <summary>Полный путь к файлу базы данных SQLite.</summary>
    string DatabaseFilePath { get; }

    /// <summary>Полный путь к файлу пользовательских настроек.</summary>
    string SettingsFilePath { get; }

    /// <summary>
    /// Создаёт все каталоги пользовательских данных, если они отсутствуют.
    /// </summary>
    void EnsureDirectoriesExist();
}
