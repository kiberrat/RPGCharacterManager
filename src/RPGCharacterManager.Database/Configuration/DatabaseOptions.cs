namespace RPGCharacterManager.Database.Configuration;

/// <summary>
/// Параметры подключения к базе данных.
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>Имя секции конфигурации, из которой читаются параметры.</summary>
    public const string SectionName = "Database";

    /// <summary>Время ожидания выполнения команды по умолчанию в секундах.</summary>
    public const int DefaultCommandTimeoutSeconds = 30;

    /// <summary>
    /// Полный путь к файлу базы данных.
    /// Если значение не задано, используется путь из <see cref="Core.Abstractions.Infrastructure.IAppPathService"/>.
    /// </summary>
    public string? DatabaseFilePath { get; set; }

    /// <summary>Время ожидания выполнения команды в секундах.</summary>
    public int CommandTimeoutSeconds { get; set; } = DefaultCommandTimeoutSeconds;

    /// <summary>
    /// Записывать в журнал текст выполняемых SQL-запросов.
    /// Включается только при диагностике: сильно увеличивает объём журнала.
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; }
}
