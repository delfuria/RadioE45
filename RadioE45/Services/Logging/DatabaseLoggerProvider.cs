using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Data;
using System.Threading.Channels;

namespace RadioE45.Services.Logging;

public sealed class DatabaseLoggerProvider : ILoggerProvider
{
    // Rows kept in the Logs table, and how often the consumer prunes down to that. Trimming used to
    // happen only at startup, so a long-running session (radio in a car easily runs for hours) grew
    // the table without bound. The channel is bounded for the same reason: if the writer ever
    // outruns SQLite, drop the oldest lines rather than the process' memory.
    private const int MaxRows = 1000;
    private const int TrimEveryInserts = 250;
    private const int QueueCapacity = 5000;

    private readonly Channel<Log> _channel = Channel.CreateBounded<Log>(
        new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

    private volatile bool _enabled;
    private ILogRepository? _repository;
    private int _insertsSinceTrim;

    public void Enable(ILogRepository repository)
    {
        _repository = repository;
        _enabled = true;
        _ = Task.Run(ConsumeAsync);
    }

    public ILogger CreateLogger(string categoryName) =>
        new DatabaseLogger(this);

    internal void TryEnqueue(Log log)
    {
        if (_enabled)
            _channel.Writer.TryWrite(log);
    }

    private async Task ConsumeAsync()
    {
        await foreach (Log log in _channel.Reader.ReadAllAsync())
        {
            try
            {
                await _repository!.InsertAsync(log);

                if (++_insertsSinceTrim >= TrimEveryInserts)
                {
                    _insertsSinceTrim = 0;
                    await _repository.TrimToLastAsync(MaxRows);
                }
            }
            catch { }
        }
    }

    public void Dispose() => _channel.Writer.TryComplete();
}

internal sealed class DatabaseLogger(DatabaseLoggerProvider provider) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        string prefix = logLevel switch
        {
            LogLevel.Trace       => "trce",
            LogLevel.Debug       => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning     => "warn",
            LogLevel.Error       => "fail",
            LogLevel.Critical    => "crit",
            _                    => "none"
        };

        string message = formatter(state, exception);
        if (exception is not null)
            message += Environment.NewLine + exception;

        provider.TryEnqueue(new Log
        {
            TimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Level = prefix,
            Message = message
        });
    }
}
