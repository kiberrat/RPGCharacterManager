using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Distribution;

/// <summary>Сведения о доступном выпуске приложения.</summary>
/// <param name="Version">Номер версии.</param>
/// <param name="ReleaseNotes">Описание изменений.</param>
/// <param name="DownloadSizeBytes">Размер загружаемого пакета в байтах.</param>
public sealed record ApplicationUpdate(string Version, string? ReleaseNotes, long DownloadSizeBytes);

/// <summary>Проверяет, загружает и применяет обновления приложения.</summary>
public interface IApplicationUpdateService
{
    /// <summary>Текущая версия приложения.</summary>
    string CurrentVersion { get; }
    /// <summary>Настроен ли источник выпусков.</summary>
    bool IsConfigured { get; }
    /// <summary>Запущена ли установленная, а не переносная сборка.</summary>
    bool IsInstalled { get; }
    /// <summary>Последнее найденное обновление.</summary>
    ApplicationUpdate? AvailableUpdate { get; }
    /// <summary>Проверяет наличие более новой версии.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Найденный выпуск либо <see langword="null"/>.</returns>
    Task<Result<ApplicationUpdate?>> CheckAsync(CancellationToken cancellationToken = default);
    /// <summary>Загружает выпуск, найденный последней проверкой.</summary>
    /// <param name="progress">Получатель прогресса от 0 до 100.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат загрузки.</returns>
    Task<Result> DownloadAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default);
    /// <summary>Применяет загруженный выпуск, закрывает и заново запускает приложение.</summary>
    /// <returns>Результат запуска установщика.</returns>
    Result ApplyAndRestart();
}

/// <summary>Категория сообщения обратной связи.</summary>
public enum FeedbackKind
{
    /// <summary>Предложение новой функции или улучшения.</summary>
    Suggestion,
    /// <summary>Сообщение об ошибке.</summary>
    Bug,
    /// <summary>Вопрос разработчику.</summary>
    Question,
    /// <summary>Иное сообщение.</summary>
    Other,
}

/// <summary>Сообщение, отправляемое разработчику.</summary>
/// <param name="Kind">Категория.</param>
/// <param name="Message">Текст сообщения.</param>
/// <param name="Contact">Необязательный контакт автора для ответа.</param>
/// <param name="ApplicationVersion">Версия приложения.</param>
/// <param name="TechnicalInformation">Необязательные технические сведения.</param>
public sealed record FeedbackMessage(FeedbackKind Kind, string Message, string? Contact, string ApplicationVersion, string? TechnicalInformation);

/// <summary>Отправляет обратную связь через серверный обработчик.</summary>
public interface IFeedbackService
{
    /// <summary>Настроен ли адрес обработчика.</summary>
    bool IsConfigured { get; }
    /// <summary>Отправляет сообщение.</summary>
    /// <param name="message">Сообщение пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат отправки.</returns>
    Task<Result> SendAsync(FeedbackMessage message, CancellationToken cancellationToken = default);
}