namespace RPGCharacterManager.Shared;

/// <summary>
/// Константы уровня приложения.
/// Используются вместо литералов, разбросанных по коду.
/// </summary>
public static class ApplicationConstants
{
    /// <summary>Отображаемое название приложения.</summary>
    public const string ApplicationName = "RPG Character Manager";

    /// <summary>Имя каталога с пользовательскими данными внутри профиля пользователя.</summary>
    public const string DataFolderName = "RPGCharacterManager";

    /// <summary>Имя подкаталога с журналами работы приложения.</summary>
    public const string LogsFolderName = "logs";

    /// <summary>Имя подкаталога с резервными копиями базы данных.</summary>
    public const string BackupsFolderName = "backups";

    /// <summary>
    /// Имя подкаталога с пользовательскими файлами: изображениями объектов
    /// и книгами для разбора помощником.
    ///
    /// Установленные расширения здесь не лежат: их содержимое попадает в базу
    /// данных, а исходный файл остаётся там, где его оставил пользователь
    /// (решение Р-102).
    /// </summary>
    public const string ContentFolderName = "content";

    /// <summary>Имя файла базы данных SQLite.</summary>
    public const string DatabaseFileName = "rpgmanager.db";

    /// <summary>Имя файла пользовательских настроек.</summary>
    public const string SettingsFileName = "settings.json";

    /// <summary>Код языка интерфейса по умолчанию.</summary>
    public const string DefaultLanguageCode = "ru-RU";

    /// <summary>
    /// Версия приложения в виде «1.0.0».
    ///
    /// Берётся из самой сборки, а не записана здесь числом: версия задана
    /// один раз в настройках сборки, и второе её объявление рано или поздно
    /// разошлось бы с первым. Расширения сверяются именно с этой версией.
    /// </summary>
    public static string Version { get; } =
        typeof(ApplicationConstants).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
}
