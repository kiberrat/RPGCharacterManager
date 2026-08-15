using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Infrastructure.Logging;

/// <summary>
/// Приёмник записей журнала, выполняющий запись в файл в отдельном потоке.
///
/// Запись выполняется асинхронно через очередь, поэтому журналирование никогда
/// не блокирует поток пользовательского интерфейса — требование STYLE_GUIDE
/// о недопустимости зависаний интерфейса.
/// </summary>
public sealed class FileLogSink : IAsyncDisposable
{
    private const int QueueCapacity = 8192;
    private const string FileNameDateFormat = "yyyyMMdd";
    private const string FileExtension = ".log";

    /// <summary>Метка порядка байтов UTF-8, записываемая в начало нового файла журнала.</summary>
    private static readonly byte[] Utf8Preamble = [0xEF, 0xBB, 0xBF];

    private readonly FileLoggerOptions _options;
    private readonly Channel<string> _queue;
    private readonly Task _writerTask;
    private readonly CancellationTokenSource _shutdown = new();

    private string? _currentFilePath;
    private DateOnly _currentFileDate;
    private long _currentFileSize;

    /// <summary>
    /// Создаёт приёмник записей журнала и запускает фоновую запись.
    /// </summary>
    /// <param name="options">Параметры журналирования.</param>
    public FileLogSink(IOptions<FileLoggerOptions> options)
    {
        _options = Guard.NotNull(options).Value;

        // DropOldest: при переполнении очереди теряются самые старые записи,
        // но приложение продолжает работу и не блокируется на журналировании.
        _queue = Channel.CreateBounded<string>(new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        _writerTask = Task.Run(WriteLoopAsync);
    }

    /// <summary>
    /// Ставит запись в очередь на сохранение в файл.
    /// </summary>
    /// <param name="line">Готовая строка журнала.</param>
    public void Enqueue(string line) => _queue.Writer.TryWrite(line);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();

        try
        {
            await _writerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Штатное завершение фоновой записи при остановке приложения.
        }

        _shutdown.Dispose();
    }

    private async Task WriteLoopAsync()
    {
        var buffer = new StringBuilder();

        await foreach (var line in _queue.Reader.ReadAllAsync(_shutdown.Token).ConfigureAwait(false))
        {
            buffer.Clear();
            buffer.AppendLine(line);

            // Забираем всё, что уже накопилось, чтобы выполнить одну операцию записи.
            while (_queue.Reader.TryRead(out var pending))
            {
                buffer.AppendLine(pending);
            }

            await WriteBufferAsync(buffer.ToString()).ConfigureAwait(false);
        }
    }

    private async Task WriteBufferAsync(string text)
    {
        try
        {
            var path = ResolveFilePath();
            var payload = Encoding.UTF8.GetBytes(text);

            // Метка порядка байтов записывается в начало нового файла, чтобы русский
            // текст журнала корректно отображался в любом текстовом редакторе.
            byte[] bytes = File.Exists(path) ? payload : [.. Utf8Preamble, .. payload];

            await using var stream = new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite,
                bufferSize: 4096,
                useAsync: true);

            await stream.WriteAsync(bytes).ConfigureAwait(false);
            _currentFileSize += bytes.Length;
        }
        catch (IOException)
        {
            // Сбой записи журнала не должен приводить к остановке приложения.
            // Следующая запись будет выполнена в новый файл.
            _currentFilePath = null;
        }
        catch (UnauthorizedAccessException)
        {
            _currentFilePath = null;
        }
    }

    private string ResolveFilePath()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var needsNewFile = _currentFilePath is null
            || _currentFileDate != today
            || _currentFileSize >= _options.MaxFileSizeBytes;

        if (!needsNewFile)
        {
            return _currentFilePath!;
        }

        System.IO.Directory.CreateDirectory(_options.Directory);

        _currentFileDate = today;
        _currentFilePath = BuildFilePath(today);
        _currentFileSize = File.Exists(_currentFilePath) ? new FileInfo(_currentFilePath).Length : 0;

        if (_currentFileSize >= _options.MaxFileSizeBytes)
        {
            _currentFilePath = BuildFilePath(today, FindNextSequenceNumber(today));
            _currentFileSize = 0;
        }

        RemoveObsoleteFiles();
        return _currentFilePath;
    }

    private string BuildFilePath(DateOnly date, int? sequence = null)
    {
        var datePart = date.ToString(FileNameDateFormat, CultureInfo.InvariantCulture);
        var sequencePart = sequence is null
            ? string.Empty
            : "_" + sequence.Value.ToString(CultureInfo.InvariantCulture);

        return Path.Combine(
            _options.Directory,
            $"{_options.FileNamePrefix}_{datePart}{sequencePart}{FileExtension}");
    }

    private int FindNextSequenceNumber(DateOnly date)
    {
        var sequence = 1;
        while (File.Exists(BuildFilePath(date, sequence)) &&
               new FileInfo(BuildFilePath(date, sequence)).Length >= _options.MaxFileSizeBytes)
        {
            sequence++;
        }

        return sequence;
    }

    private void RemoveObsoleteFiles()
    {
        if (_options.RetainedFileCount <= 0)
        {
            return;
        }

        try
        {
            var obsolete = new DirectoryInfo(_options.Directory)
                .GetFiles($"{_options.FileNamePrefix}_*{FileExtension}")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Skip(_options.RetainedFileCount);

            foreach (var file in obsolete)
            {
                file.Delete();
            }
        }
        catch (IOException)
        {
            // Очистка журналов не является критичной операцией.
        }
        catch (UnauthorizedAccessException)
        {
            // Очистка журналов не является критичной операцией.
        }
    }
}
