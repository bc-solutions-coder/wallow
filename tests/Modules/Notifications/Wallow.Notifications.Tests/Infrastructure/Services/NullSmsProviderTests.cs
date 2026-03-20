using Wallow.Notifications.Application.Channels.Sms.Interfaces;
using Wallow.Notifications.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public class NullSmsProviderTests
{
#pragma warning disable CA2000 // LoggerFactory disposal not needed in tests
    private readonly NullSmsProvider _provider = new(
        LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Trace))
            .CreateLogger<NullSmsProvider>());
#pragma warning restore CA2000

    [Fact]
    public async Task SendAsync_AlwaysReturnsSuccess()
    {
        SmsDeliveryResult result = await _provider.SendAsync("+12025550100", "Test message");

        result.Success.Should().BeTrue();
        result.MessageSid.Should().Be("null-sid");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WithDifferentInput_ReturnsSuccess()
    {
        SmsDeliveryResult result = await _provider.SendAsync("+447911123456", "Another message");

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_WithCancellationToken_ReturnsSuccess()
    {
        using CancellationTokenSource cts = new();

        SmsDeliveryResult result = await _provider.SendAsync("+12025550100", "Test", cts.Token);

        result.Success.Should().BeTrue();
        result.MessageSid.Should().Be("null-sid");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WithEmptyBody_ReturnsSuccess()
    {
        SmsDeliveryResult result = await _provider.SendAsync("+12025550100", string.Empty);

        result.Success.Should().BeTrue();
        result.MessageSid.Should().Be("null-sid");
    }

    [Fact]
    public async Task SendAsync_WithLongMessage_ReturnsSuccess()
    {
        string longMessage = new string('X', 1600);

        SmsDeliveryResult result = await _provider.SendAsync("+12025550100", longMessage);

        result.Success.Should().BeTrue();
        result.MessageSid.Should().Be("null-sid");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_MultipleCalls_EachReturnsIndependentSuccess()
    {
        SmsDeliveryResult result1 = await _provider.SendAsync("+11111111111", "First");
        SmsDeliveryResult result2 = await _provider.SendAsync("+22222222222", "Second");

        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result1.MessageSid.Should().Be("null-sid");
        result2.MessageSid.Should().Be("null-sid");
    }

    [Fact]
    public async Task SendAsync_LogsInformationWithRecipientAndBody()
    {
        FakeLogger<NullSmsProvider> fakeLogger = new();
        NullSmsProvider provider = new(fakeLogger);

        await provider.SendAsync("+15551234567", "Hello there");

        fakeLogger.LogEntries.Should().ContainSingle();
        FakeLogEntry entry = fakeLogger.LogEntries[0];
        entry.LogLevel.Should().Be(LogLevel.Information);
        entry.FormattedMessage.Should().Contain("+15551234567");
        entry.FormattedMessage.Should().Contain("Hello there");
    }

    [Fact]
    public async Task SendAsync_LogsOncePerCall()
    {
        FakeLogger<NullSmsProvider> fakeLogger = new();
        NullSmsProvider provider = new(fakeLogger);

        await provider.SendAsync("+11111111111", "First");
        await provider.SendAsync("+22222222222", "Second");

        fakeLogger.LogEntries.Should().HaveCount(2);
    }

    [Fact]
    public async Task SendAsync_WithEmptyPhoneNumber_StillReturnsSuccess()
    {
        SmsDeliveryResult result = await _provider.SendAsync(string.Empty, "Body text");

        result.Success.Should().BeTrue();
        result.MessageSid.Should().Be("null-sid");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_ConcurrentCalls_AllReturnSuccess()
    {
        Task<SmsDeliveryResult>[] tasks = Enumerable.Range(0, 10)
            .Select(i => _provider.SendAsync($"+1555000{i:D4}", $"Message {i}"))
            .ToArray();

        SmsDeliveryResult[] results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.MessageSid.Should().Be("null-sid");
            r.ErrorMessage.Should().BeNull();
        });
    }

    [Fact]
    public async Task SendAsync_ReturnsCompletedTask_Synchronously()
    {
        Task<SmsDeliveryResult> task = _provider.SendAsync("+12025550100", "Test");

        task.IsCompletedSuccessfully.Should().BeTrue();
        SmsDeliveryResult result = await task;
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Provider_ImplementsISmsProvider()
    {
        _provider.Should().BeAssignableTo<ISmsProvider>();
    }

#pragma warning disable CA2000 // LoggerFactory disposal not needed in tests
    [Fact]
    public async Task SendAsync_WithLoggingDisabled_StillReturnsSuccess()
    {
        NullSmsProvider provider = new(
            LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.None))
                .CreateLogger<NullSmsProvider>());

        SmsDeliveryResult result = await provider.SendAsync("+12025550100", "Test message");

        result.Success.Should().BeTrue();
        result.MessageSid.Should().Be("null-sid");
        result.ErrorMessage.Should().BeNull();
    }
#pragma warning restore CA2000

    [Fact]
    public async Task SendAsync_LogMessageContainsSuppressedIndicator()
    {
        FakeLogger<NullSmsProvider> fakeLogger = new();
        NullSmsProvider provider = new(fakeLogger);

        await provider.SendAsync("+10000000000", "Test body");

        fakeLogger.LogEntries[0].FormattedMessage.Should().Contain("suppressed");
    }

    private sealed class FakeLogEntry
    {
        public LogLevel LogLevel { get; init; }
        public string FormattedMessage { get; init; } = string.Empty;
    }

    private sealed class FakeLogger<T> : ILogger<T>
    {
        public List<FakeLogEntry> LogEntries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogEntries.Add(new FakeLogEntry
            {
                LogLevel = logLevel,
                FormattedMessage = formatter(state, exception)
            });
        }
    }
}
