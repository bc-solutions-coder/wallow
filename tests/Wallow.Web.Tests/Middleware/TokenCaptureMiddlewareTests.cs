using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Wallow.Web.Middleware;

namespace Wallow.Web.Tests.Middleware;

public sealed class TokenCaptureMiddlewareTests
{
    [Fact]
    public void LogTokenCaptureBegin_EmitsDebugLevel_WithPath()
    {
        // Arrange
        TestLogCollector collector = new();
        CollectingLogger<TokenCaptureMiddleware> logger = new(collector);
        RequestDelegate noopNext = _ => Task.CompletedTask;
        TokenCaptureMiddleware middleware = new(noopNext, logger);

        // Act
        middleware.LogTokenCaptureBegin("/test-path");

        // Assert
        collector.Entries.Should().ContainSingle();
        LogEntry entry = collector.Entries[0];
        entry.LogLevel.Should().Be(LogLevel.Debug);
        entry.Message.Should().Contain("token capture");
        entry.Message.Should().Contain("/test-path");
    }

    [Fact]
    public void LogTokenCaptureSuccess_EmitsDebugLevel_WithPath()
    {
        // Arrange
        TestLogCollector collector = new();
        CollectingLogger<TokenCaptureMiddleware> logger = new(collector);
        RequestDelegate noopNext = _ => Task.CompletedTask;
        TokenCaptureMiddleware middleware = new(noopNext, logger);

        // Act
        middleware.LogTokenCaptureSuccess("/dashboard");

        // Assert
        collector.Entries.Should().ContainSingle();
        LogEntry entry = collector.Entries[0];
        entry.LogLevel.Should().Be(LogLevel.Debug);
        entry.Message.Should().Contain("token capture");
        entry.Message.Should().Contain("/dashboard");
    }

    [Fact]
    public void LogNoAccessTokenAvailable_EmitsWarningLevel_WithPath()
    {
        // Arrange
        TestLogCollector collector = new();
        CollectingLogger<TokenCaptureMiddleware> logger = new(collector);
        RequestDelegate noopNext = _ => Task.CompletedTask;
        TokenCaptureMiddleware middleware = new(noopNext, logger);

        // Act
        middleware.LogNoAccessTokenAvailable("/secure-page");

        // Assert
        collector.Entries.Should().ContainSingle();
        LogEntry entry = collector.Entries[0];
        entry.LogLevel.Should().Be(LogLevel.Warning);
        entry.Message.Should().Contain("no access token");
        entry.Message.Should().Contain("/secure-page");
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message);

    private sealed class TestLogCollector
    {
        public List<LogEntry> Entries { get; } = [];
    }

    private sealed class CollectingLogger<T>(TestLogCollector collector) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            collector.Entries.Add(new LogEntry(logLevel, message));
        }
    }
}
