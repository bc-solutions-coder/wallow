using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Infrastructure.Services;
using Wallow.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public class LogPushProviderTests
{
#pragma warning disable CA2000 // LoggerFactory disposal not needed in tests
    private readonly LogPushProvider _provider = new(
        LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Trace))
            .CreateLogger<LogPushProvider>());
#pragma warning restore CA2000

    [Fact]
    public async Task SendAsync_AlwaysReturnsSuccess()
    {
        PushMessage message = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "Test Title", "Test Body", TimeProvider.System);

        PushDeliveryResult result = await _provider.SendAsync(message, "device-token-abc");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WithDifferentDeviceToken_ReturnsSuccess()
    {
        PushMessage message = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "Alert", "Something happened", TimeProvider.System);

        PushDeliveryResult result = await _provider.SendAsync(message, "another-device-token-xyz");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WithMinimalBody_ReturnsSuccess()
    {
        PushMessage message = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "Title Only", ".", TimeProvider.System);

        PushDeliveryResult result = await _provider.SendAsync(message, "device-token");

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_WithCancellationToken_ReturnsSuccess()
    {
        PushMessage message = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "Test", "Body", TimeProvider.System);
        using CancellationTokenSource cts = new();

        PushDeliveryResult result = await _provider.SendAsync(message, "device-token", cts.Token);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WithLongTitle_ReturnsSuccess()
    {
        string longTitle = new string('A', 500);
        PushMessage message = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), longTitle, "Body", TimeProvider.System);

        PushDeliveryResult result = await _provider.SendAsync(message, "device-token");

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_LogsInformationWithDeviceTokenAndMessageDetails()
    {
        FakeLogger<LogPushProvider> fakeLogger = new();
        LogPushProvider provider = new(fakeLogger);
        PushMessage message = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "Alert Title", "Alert Body", TimeProvider.System);

        await provider.SendAsync(message, "device-abc-123");

        fakeLogger.LogEntries.Should().ContainSingle();
        FakeLogEntry entry = fakeLogger.LogEntries[0];
        entry.LogLevel.Should().Be(LogLevel.Information);
        entry.FormattedMessage.Should().Contain("device-abc-123");
        entry.FormattedMessage.Should().Contain("Alert Title");
        entry.FormattedMessage.Should().Contain("Alert Body");
    }

    [Fact]
    public async Task SendAsync_WithEmptyDeviceToken_StillReturnsSuccess()
    {
        PushMessage message = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "Title", "Body", TimeProvider.System);

        PushDeliveryResult result = await _provider.SendAsync(message, string.Empty);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WithLongBody_ReturnsSuccess()
    {
        string longBody = new string('B', 2000);
        PushMessage message = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "Title", longBody, TimeProvider.System);

        PushDeliveryResult result = await _provider.SendAsync(message, "device-token");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_MultipleCalls_EachReturnsIndependentSuccess()
    {
        PushMessage message1 = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "First", "Body1", TimeProvider.System);
        PushMessage message2 = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "Second", "Body2", TimeProvider.System);

        PushDeliveryResult result1 = await _provider.SendAsync(message1, "token-1");
        PushDeliveryResult result2 = await _provider.SendAsync(message2, "token-2");

        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result1.ErrorMessage.Should().BeNull();
        result2.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_ConcurrentCalls_AllReturnSuccess()
    {
        Task<PushDeliveryResult>[] tasks = Enumerable.Range(0, 10)
            .Select(i =>
            {
                PushMessage msg = PushMessage.Create(
                    TenantId.New(), new UserId(Guid.NewGuid()), $"Title-{i}", $"Body-{i}", TimeProvider.System);
                return _provider.SendAsync(msg, $"token-{i}");
            })
            .ToArray();

        PushDeliveryResult[] results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.ErrorMessage.Should().BeNull();
        });
    }

    [Fact]
    public async Task SendAsync_LogsOncePerCall()
    {
        FakeLogger<LogPushProvider> fakeLogger = new();
        LogPushProvider provider = new(fakeLogger);
        PushMessage message1 = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "T1", "B1", TimeProvider.System);
        PushMessage message2 = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "T2", "B2", TimeProvider.System);

        await provider.SendAsync(message1, "tok-1");
        await provider.SendAsync(message2, "tok-2");

        fakeLogger.LogEntries.Should().HaveCount(2);
    }

    [Fact]
    public async Task SendAsync_ReturnsCompletedTask_Synchronously()
    {
        PushMessage message = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "Title", "Body", TimeProvider.System);

        Task<PushDeliveryResult> task = _provider.SendAsync(message, "device-token");

        task.IsCompletedSuccessfully.Should().BeTrue();
        PushDeliveryResult result = await task;
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Provider_ImplementsIPushProvider()
    {
        _provider.Should().BeAssignableTo<IPushProvider>();
    }

#pragma warning disable CA2000 // LoggerFactory disposal not needed in tests
    [Fact]
    public async Task SendAsync_WithLoggingDisabled_StillReturnsSuccess()
    {
        LogPushProvider provider = new(
            LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.None))
                .CreateLogger<LogPushProvider>());
        PushMessage message = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "Title", "Body", TimeProvider.System);

        PushDeliveryResult result = await provider.SendAsync(message, "device-token");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }
#pragma warning restore CA2000

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
