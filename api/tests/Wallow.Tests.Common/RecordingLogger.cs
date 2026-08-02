using Microsoft.Extensions.Logging;

namespace Wallow.Tests.Common;

/// <summary>
/// Captures log entries as they are written. The [LoggerMessage] source generator used here
/// (Microsoft.Gen.Logging) writes into a thread-local state that it clears the moment
/// <see cref="ILogger.Log{TState}"/> returns, so a recorded-call inspection (e.g. NSubstitute)
/// sees emptied tags. Formatting must happen inside the call, which is what this logger does.
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
