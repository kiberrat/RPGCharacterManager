using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.Settings;
using RPGCharacterManager.Infrastructure.Logging;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Infrastructure.Settings;

/// <summary>
/// Хранение пользовательских настроек в файле JSON профиля пользователя.
///
/// Сохранение выполняется через временный файл с последующей заменой, поэтому
/// прерывание работы приложения во время записи не повреждает настройки.
/// </summary>
public sealed class JsonSettingsService : ISettingsService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IAppPathService _paths;
    private readonly IEventBus _eventBus;
    private readonly ILogger<JsonSettingsService> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private AppSettings _current = new();

    /// <summary>
    /// Создаёт службу настроек.
    /// </summary>
    /// <param name="paths">Служба путей пользовательских данных.</param>
    /// <param name="eventBus">Шина событий для оповещения об изменениях.</param>
    /// <param name="logger">Журналировщик.</param>
    public JsonSettingsService(
        IAppPathService paths,
        IEventBus eventBus,
        ILogger<JsonSettingsService> logger)
    {
        _paths = Guard.NotNull(paths);
        _eventBus = Guard.NotNull(eventBus);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public AppSettings Current => _current;

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = _paths.SettingsFilePath;

        if (!File.Exists(path))
        {
            InfrastructureLog.SettingsFileMissing(_logger, path);
            _current = new AppSettings();
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var loaded = await JsonSerializer
                .DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            _current = loaded ?? new AppSettings();
            _current.Normalize();

            InfrastructureLog.SettingsLoaded(_logger, path);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // Повреждённый файл настроек не должен препятствовать запуску приложения.
            InfrastructureLog.SettingsReadFailed(_logger, exception, path);
            _current = new AppSettings();
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Action<AppSettings> modify, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(modify);

        var draft = _current.Clone();
        modify(draft);
        draft.Normalize();

        _current = draft;

        await SaveAsync(cancellationToken).ConfigureAwait(false);
        await _eventBus.PublishAsync(new SettingsChangedEvent(draft), cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _paths.EnsureDirectoriesExist();

            var path = _paths.SettingsFilePath;
            var temporaryPath = path + ".tmp";

            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer
                    .SerializeAsync(stream, _current, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, overwrite: true);
            InfrastructureLog.SettingsSaved(_logger, path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            InfrastructureLog.SettingsSaveFailed(_logger, exception);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _writeLock.Dispose();
}
