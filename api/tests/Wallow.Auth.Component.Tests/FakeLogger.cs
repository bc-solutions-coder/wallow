using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace Wallow.Auth.Component.Tests;

public sealed class FakeLogEntry
{
    public LogLevel LogLevel { get; init; }
    public string FormattedMessage { get; init; } = string.Empty;
}

public sealed class FakeLogger<T> : ILogger<T>
{
    private readonly List<FakeLogEntry> _entries = [];
    public ReadOnlyCollection<FakeLogEntry> LogEntries => _entries.AsReadOnly();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Add(new FakeLogEntry
        {
            LogLevel = logLevel,
            FormattedMessage = formatter(state, exception)
        });
    }
}
