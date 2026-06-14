using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Data;
using System.Threading.Channels;

namespace RadioE45.Services.Logging;

public sealed class DatabaseLoggerProvider : ILoggerProvider
{
    private readonly Channel<Log> _channel = Channel.CreateUnbounded<Log>(
        new UnboundedChannelOptions { SingleReader = true });
    private volatile bool _enabled;
    private ILogRepository? _repository;

    public void Enable(ILogRepository repository)
    {
        _repository = repository;
        _enabled = true;
        _ = Task.Run(ConsumeAsync);
    }

    public ILogger CreateLogger(string categoryName) =>
        new DatabaseLogger(categoryName, this);

    internal void TryEnqueue(Log log)
    {
        if (_enabled)
            _channel.Writer.TryWrite(log);
    }

    private async Task ConsumeAsync()
    {
        await foreach (Log log in _channel.Reader.ReadAllAsync())
        {
            try { await _repository!.InsertAsync(log); }
            catch { }
        }
    }

    public void Dispose() => _channel.Writer.TryComplete();
}

internal sealed class DatabaseLogger(string categoryName, DatabaseLoggerProvider provider) : ILogger
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
