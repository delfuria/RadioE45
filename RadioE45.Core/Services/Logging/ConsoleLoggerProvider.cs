using Microsoft.Extensions.Logging;

namespace RadioE45.Services.Logging;

internal sealed class ConsoleLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new ConsoleLogger(categoryName);
    public void Dispose() { }
}

internal sealed class ConsoleLogger(string categoryName) : ILogger
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
        Console.WriteLine($"{prefix}: {categoryName}[{eventId.Name}]");
        Console.WriteLine($"      {message}");

        if (exception is not null)
            Console.WriteLine(exception);
    }
}
