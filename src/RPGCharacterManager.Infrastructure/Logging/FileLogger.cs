using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.Infrastructure.Logging;

/// <summary>
/// Журналировщик, записывающий сообщения в файл через <see cref="FileLogSink"/>.
/// </summary>
internal sealed class FileLogger : ILogger
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    private readonly string _categoryName;
    private readonly FileLogSink _sink;
    private readonly Func<FileLoggerOptions> _optionsAccessor;

    /// <summary>
    /// Создаёт журналировщик для указанной категории.
    /// </summary>
    /// <param name="categoryName">Категория журналирования, как правило — полное имя класса.</param>
    /// <param name="sink">Приёмник записей.</param>
    /// <param name="optionsAccessor">Доступ к актуальным параметрам журналирования.</param>
    public FileLogger(string categoryName, FileLogSink sink, Func<FileLoggerOptions> optionsAccessor)
    {
        _categoryName = categoryName;
        _sink = sink;
        _optionsAccessor = optionsAccessor;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) =>
        logLevel != LogLevel.None && logLevel >= _optionsAccessor().MinimumLevel;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel) || formatter is null)
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        var builder = new StringBuilder()
            .Append(DateTimeOffset.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture))
            .Append(" [").Append(FormatLevel(logLevel)).Append(']')
            .Append(' ').Append(_categoryName)
            .Append(": ").Append(message);

        if (eventId.Id != 0)
        {
            builder.Append(" (событие ").Append(eventId.Id.ToString(CultureInfo.InvariantCulture)).Append(')');
        }

        if (exception is not null)
        {
            builder.AppendLine().Append(exception);
        }

        _sink.Enqueue(builder.ToString());
    }

    private static string FormatLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "FATAL",
        _ => "NONE ",
    };
}
