using Microsoft.Extensions.Logging;

namespace Wallow.Tests.Common;

/// <summary>
/// Formats and captures each log entry during the Log call, before a provider can reuse its state.
/// </summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyList<LogEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
    }
}

public sealed record LogEntry(LogLevel Level, EventId EventId, string Message);
