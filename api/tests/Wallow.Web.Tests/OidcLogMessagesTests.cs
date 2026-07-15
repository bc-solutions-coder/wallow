using Microsoft.Extensions.Logging;

namespace Wallow.Web.Tests;

public sealed class OidcLogMessagesTests
{
    [Fact]
    public void OnAuthorizationCodeReceived_LogsAtDebugLevel_WithScheme()
    {
        // Arrange
        TestLogCollector collector = new();
        ILogger logger = new CollectingLogger(collector);

        // Act
        OidcLogMessages.OnAuthorizationCodeReceived(logger, "OpenIdConnect");

        // Assert
        collector.Entries.Should().ContainSingle();
        LogEntry entry = collector.Entries[0];
        entry.LogLevel.Should().Be(LogLevel.Debug);
        entry.Message.Should().Contain("authorization code received");
        entry.Message.Should().Contain("OpenIdConnect");
    }

    [Fact]
    public void OnTokenValidated_LogsAtDebugLevel_WithSubjectAndIssuer()
    {
        // Arrange
        TestLogCollector collector = new();
        ILogger logger = new CollectingLogger(collector);

        // Act
        OidcLogMessages.OnTokenValidated(logger, "user-123", "https://issuer.example.com");

        // Assert
        collector.Entries.Should().ContainSingle();
        LogEntry entry = collector.Entries[0];
        entry.LogLevel.Should().Be(LogLevel.Debug);
        entry.Message.Should().Contain("token validated");
        entry.Message.Should().Contain("user-123");
        entry.Message.Should().Contain("https://issuer.example.com");
    }

    [Fact]
    public void OnTokenResponseReceived_LogsAtDebugLevel_WithScheme()
    {
        // Arrange
        TestLogCollector collector = new();
        ILogger logger = new CollectingLogger(collector);

        // Act
        OidcLogMessages.OnTokenResponseReceived(logger, "OpenIdConnect");

        // Assert
        collector.Entries.Should().ContainSingle();
        LogEntry entry = collector.Entries[0];
        entry.LogLevel.Should().Be(LogLevel.Debug);
        entry.Message.Should().Contain("token response received");
        entry.Message.Should().Contain("OpenIdConnect");
    }

    [Fact]
    public void OnRedirectToIdentityProvider_LogsAtDebugLevel_WithRedirectUri()
    {
        // Arrange
        TestLogCollector collector = new();
        ILogger logger = new CollectingLogger(collector);

        // Act
        OidcLogMessages.OnRedirectToIdentityProvider(logger, "https://auth.example.com/authorize");

        // Assert
        collector.Entries.Should().ContainSingle();
        LogEntry entry = collector.Entries[0];
        entry.LogLevel.Should().Be(LogLevel.Debug);
        entry.Message.Should().Contain("redirecting to identity provider");
        entry.Message.Should().Contain("https://auth.example.com/authorize");
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message);

    private sealed class TestLogCollector
    {
        public List<LogEntry> Entries { get; } = [];
    }

    private sealed class CollectingLogger(TestLogCollector collector) : ILogger
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
