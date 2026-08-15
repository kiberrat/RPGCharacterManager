using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Infrastructure.Logging;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Infrastructure.Diagnostics;

/// <summary>
/// Централизованная обработка необработанных исключений.
///
/// STYLE_GUIDE требует, чтобы приложение не завершалось аварийно и чтобы любое
/// исключение попадало в журнал. Обработчик перехватывает исключения из потоков,
/// не связанных с интерфейсом, журналирует их и публикует
/// <see cref="ApplicationErrorEvent"/> для отображения пользователю.
/// </summary>
public sealed class GlobalExceptionHandler : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private bool _isAttached;

    /// <summary>
    /// Создаёт обработчик исключений.
    /// </summary>
    /// <param name="eventBus">Шина событий.</param>
    /// <param name="logger">Журналировщик.</param>
    public GlobalExceptionHandler(IEventBus eventBus, ILogger<GlobalExceptionHandler> logger)
    {
        _eventBus = Guard.NotNull(eventBus);
        _logger = Guard.NotNull(logger);
    }

    /// <summary>
    /// Подключает обработчик к источникам необработанных исключений домена приложения.
    /// </summary>
    public void Attach()
    {
        if (_isAttached)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _isAttached = true;

        InfrastructureLog.ExceptionHandlerAttached(_logger);
    }

    /// <summary>
    /// Обрабатывает исключение вручную: журналирует и оповещает интерфейс.
    /// </summary>
    /// <param name="source">Источник ошибки: имя подсистемы или операции.</param>
    /// <param name="exception">Возникшее исключение.</param>
    /// <param name="isFatal">Ошибка не позволяет продолжить работу.</param>
    public void Handle(string source, Exception exception, bool isFatal = false)
    {
        Guard.NotNullOrWhiteSpace(source);
        Guard.NotNull(exception);

        InfrastructureLog.UnhandledError(
            _logger,
            isFatal ? LogLevel.Critical : LogLevel.Error,
            exception,
            source);

        // Публикация события не должна порождать новое исключение внутри обработчика ошибок.
        _ = PublishSafelyAsync(new ApplicationErrorEvent(source, exception, isFatal));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_isAttached)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _isAttached = false;
    }

    private async Task PublishSafelyAsync(ApplicationErrorEvent errorEvent)
    {
        try
        {
            await _eventBus.PublishAsync(errorEvent, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            InfrastructureLog.ErrorPublicationFailed(_logger, exception);
        }
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        var exception = args.ExceptionObject as Exception
            ?? new InvalidOperationException("Домен приложения сообщил об ошибке неизвестного типа.");

        Handle("Домен приложения", exception, args.IsTerminating);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        // Пометка исключения как обработанного не даёт среде выполнения завершить процесс.
        args.SetObserved();
        Handle("Фоновая задача", args.Exception);
    }
}
