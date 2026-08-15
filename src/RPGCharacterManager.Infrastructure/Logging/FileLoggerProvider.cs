using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Infrastructure.Logging;

/// <summary>
/// Поставщик журналировщиков, записывающих сообщения в файл.
/// </summary>
[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.Ordinal);
    private readonly FileLogSink _sink;
    private readonly IOptionsMonitor<FileLoggerOptions> _options;

    /// <summary>
    /// Создаёт поставщика журналировщиков.
    /// </summary>
    /// <param name="sink">Приёмник записей журнала.</param>
    /// <param name="options">Отслеживаемые параметры журналирования.</param>
    public FileLoggerProvider(FileLogSink sink, IOptionsMonitor<FileLoggerOptions> options)
    {
        _sink = Guard.NotNull(sink);
        _options = Guard.NotNull(options);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _sink, () => _options.CurrentValue));

    /// <inheritdoc />
    public void Dispose() => _loggers.Clear();
}
