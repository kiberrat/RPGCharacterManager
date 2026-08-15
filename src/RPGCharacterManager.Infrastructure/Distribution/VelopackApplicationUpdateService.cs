using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPGCharacterManager.Core.Abstractions.Distribution;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;
using Velopack;
using Velopack.Sources;

namespace RPGCharacterManager.Infrastructure.Distribution;

/// <summary>Реализация автоматического обновления на основе Velopack.</summary>
public sealed class VelopackApplicationUpdateService : IApplicationUpdateService, IDisposable
{
    private readonly ILogger<VelopackApplicationUpdateService> _logger;
    private readonly UpdateManager? _manager;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private UpdateInfo? _pendingUpdate;
    private bool _isDownloaded;

    /// <summary>Создаёт службу обновлений.</summary>
    /// <param name="options">Параметры доставки.</param>
    /// <param name="logger">Журналировщик.</param>
    public VelopackApplicationUpdateService(
        IOptions<DistributionOptions> options,
        ILogger<VelopackApplicationUpdateService> logger)
    {
        var distribution = Guard.NotNull(options).Value;
        _logger = Guard.NotNull(logger);

        if (string.IsNullOrWhiteSpace(distribution.UpdateSource))
        {
            return;
        }

        var source = distribution.UpdateSource.Trim();
        _manager = IsGithubRepository(source)
            ? new UpdateManager(new GithubSource(source, accessToken: null, prerelease: false))
            : new UpdateManager(source);
    }

    /// <inheritdoc />
    public string CurrentVersion => _manager?.CurrentVersion?.ToString()
        ?? typeof(VelopackApplicationUpdateService).Assembly.GetName().Version?.ToString(3)
        ?? "1.0.0";

    /// <inheritdoc />
    public bool IsConfigured => _manager is not null;

    /// <inheritdoc />
    public bool IsInstalled => _manager?.IsInstalled ?? false;

    /// <inheritdoc />
    public ApplicationUpdate? AvailableUpdate { get; private set; }

    /// <inheritdoc />
    public async Task<Result<ApplicationUpdate?>> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_manager is null)
        {
            return Result.Failure<ApplicationUpdate?>("Источник обновлений ещё не настроен.");
        }

        if (!_manager.IsInstalled)
        {
            return Result.Failure<ApplicationUpdate?>(
                "Автообновление работает после установки приложения через Setup.exe.");
        }

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _pendingUpdate = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            _isDownloaded = false;
            AvailableUpdate = _pendingUpdate is null
                ? null
                : new ApplicationUpdate(
                    _pendingUpdate.TargetFullRelease.Version.ToString(),
                    _pendingUpdate.TargetFullRelease.NotesMarkdown,
                    _pendingUpdate.TargetFullRelease.Size);
            return Result.Success(AvailableUpdate);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            DistributionLog.UpdateCheckFailed(_logger, exception);
            return Result.Failure<ApplicationUpdate?>(
                "Не удалось проверить обновления. Проверьте подключение к интернету.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result> DownloadAsync(
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_manager is null || _pendingUpdate is null)
        {
            return Result.Failure("Сначала проверьте наличие новой версии.");
        }

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _manager.DownloadUpdatesAsync(
                _pendingUpdate,
                value => progress?.Report(value),
                cancellationToken).ConfigureAwait(false);
            _isDownloaded = true;
            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            DistributionLog.UpdateDownloadFailed(_logger, exception);
            return Result.Failure("Не удалось загрузить обновление. Попробуйте ещё раз.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <inheritdoc />
    public Result ApplyAndRestart()
    {
        if (_manager is null || _pendingUpdate is null || !_isDownloaded)
        {
            return Result.Failure("Обновление ещё не загружено.");
        }

        try
        {
            _manager.ApplyUpdatesAndRestart(_pendingUpdate.TargetFullRelease);
            return Result.Success();
        }
        catch (Exception exception)
        {
            DistributionLog.UpdateApplyFailed(_logger, exception);
            return Result.Failure("Не удалось запустить установку обновления.");
        }
    }

    /// <inheritdoc />
    public void Dispose() => _operationLock.Dispose();

    private static bool IsGithubRepository(string source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase);
}