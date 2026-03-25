using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Notifications.Infrastructure.Services;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public class SignalRNotificationServiceTests
{
    private readonly IRealtimeDispatcher _dispatcher = Substitute.For<IRealtimeDispatcher>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly SignalRNotificationService _sut;

#pragma warning disable CA2000 // LoggerFactory disposal not needed in tests
    public SignalRNotificationServiceTests()
    {
        _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero));
        _sut = new SignalRNotificationService(
            _dispatcher,
            _timeProvider,
            LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Trace))
                .CreateLogger<SignalRNotificationService>());
    }
#pragma warning restore CA2000

    [Fact]
    public async Task SendToUserAsync_WithValidInput_DispatchesEnvelopeToUser()
    {
        Guid userId = Guid.NewGuid();

        await _sut.SendToUserAsync(userId, "Test Title", "Test Message", "info");

        await _dispatcher.Received(1).SendToUserAsync(
            userId.ToString(),
            Arg.Is<RealtimeEnvelope>(e =>
                e.Module == "Notifications" &&
                e.Type == "NotificationCreated"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToUserAsync_WhenDispatcherThrows_PropagatesException()
    {
        Guid userId = Guid.NewGuid();
        _dispatcher
            .SendToUserAsync(Arg.Any<string>(), Arg.Any<RealtimeEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection lost"));

        Func<Task> act = () => _sut.SendToUserAsync(userId, "Title", "Message", "error");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Connection lost");
    }

    [Fact]
    public async Task BroadcastToTenantAsync_WithValidInput_DispatchesEnvelopeToTenantGroup()
    {
        TenantId tenantId = TenantId.New();

        await _sut.BroadcastToTenantAsync(tenantId, "Announcement", "Hello tenants", "announcement");

        await _dispatcher.Received(1).SendToGroupAsync(
            $"tenant:{tenantId.Value}",
            Arg.Is<RealtimeEnvelope>(e =>
                e.Module == "Notifications" &&
                e.Type == "AnnouncementPublished"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastToTenantAsync_WhenDispatcherThrows_PropagatesException()
    {
        TenantId tenantId = TenantId.New();
        _dispatcher
            .SendToGroupAsync(Arg.Any<string>(), Arg.Any<RealtimeEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Broker unavailable"));

        Func<Task> act = () => _sut.BroadcastToTenantAsync(tenantId, "Title", "Message", "alert");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Broker unavailable");
    }

    [Fact]
    public async Task SendToUserAsync_PassesCancellationToken_ToDispatcher()
    {
        Guid userId = Guid.NewGuid();
        using CancellationTokenSource cts = new();
        CancellationToken token = cts.Token;

        await _sut.SendToUserAsync(userId, "Title", "Message", "info", null, token);

        await _dispatcher.Received(1).SendToUserAsync(
            Arg.Any<string>(),
            Arg.Any<RealtimeEnvelope>(),
            token);
    }

    [Fact]
    public async Task BroadcastToTenantAsync_PassesCancellationToken_ToDispatcher()
    {
        TenantId tenantId = TenantId.New();
        using CancellationTokenSource cts = new();
        CancellationToken token = cts.Token;

        await _sut.BroadcastToTenantAsync(tenantId, "Title", "Message", "info", token);

        await _dispatcher.Received(1).SendToGroupAsync(
            Arg.Any<string>(),
            Arg.Any<RealtimeEnvelope>(),
            token);
    }

    [Fact]
    public async Task SendToUserAsync_UsesUserIdAsStringTarget()
    {
        Guid userId = Guid.NewGuid();

        await _sut.SendToUserAsync(userId, "Title", "Message", "info");

        await _dispatcher.Received(1).SendToUserAsync(
            userId.ToString(),
            Arg.Any<RealtimeEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastToTenantAsync_UsesTenantPrefixedGroupId()
    {
        TenantId tenantId = TenantId.New();

        await _sut.BroadcastToTenantAsync(tenantId, "Title", "Message", "info");

        await _dispatcher.Received(1).SendToGroupAsync(
            $"tenant:{tenantId.Value}",
            Arg.Any<RealtimeEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToUserAsync_EnvelopeContainsNotificationCreatedType()
    {
        Guid userId = Guid.NewGuid();
        RealtimeEnvelope? capturedEnvelope = null;
        await _dispatcher.SendToUserAsync(
            Arg.Any<string>(),
            Arg.Do<RealtimeEnvelope>(e => capturedEnvelope = e),
            Arg.Any<CancellationToken>());

        await _sut.SendToUserAsync(userId, "My Title", "My Message", "warning");

        capturedEnvelope.Should().NotBeNull();
        capturedEnvelope!.Type.Should().Be("NotificationCreated");
        capturedEnvelope.Module.Should().Be("Notifications");
    }

    [Fact]
    public async Task BroadcastToTenantAsync_EnvelopeContainsAnnouncementPublishedType()
    {
        TenantId tenantId = TenantId.New();
        RealtimeEnvelope? capturedEnvelope = null;
        await _dispatcher.SendToGroupAsync(
            Arg.Any<string>(),
            Arg.Do<RealtimeEnvelope>(e => capturedEnvelope = e),
            Arg.Any<CancellationToken>());

        await _sut.BroadcastToTenantAsync(tenantId, "Announce", "Content", "announcement");

        capturedEnvelope.Should().NotBeNull();
        capturedEnvelope!.Type.Should().Be("AnnouncementPublished");
        capturedEnvelope.Module.Should().Be("Notifications");
    }

    [Fact]
    public async Task SendToUserAsync_UsesTimeProviderForTimestamp()
    {
        DateTimeOffset fixedTime = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        _timeProvider.GetUtcNow().Returns(fixedTime);
        Guid userId = Guid.NewGuid();

        await _sut.SendToUserAsync(userId, "Title", "Message", "info");

        _timeProvider.Received(1).GetUtcNow();
    }

    [Fact]
    public async Task BroadcastToTenantAsync_UsesTimeProviderForTimestamp()
    {
        DateTimeOffset fixedTime = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        _timeProvider.GetUtcNow().Returns(fixedTime);
        TenantId tenantId = TenantId.New();

        await _sut.BroadcastToTenantAsync(tenantId, "Title", "Message", "info");

        _timeProvider.Received(1).GetUtcNow();
    }

    [Fact]
    public async Task SendToUserAsync_WithEmptyStrings_StillDispatches()
    {
        Guid userId = Guid.NewGuid();

        await _sut.SendToUserAsync(userId, string.Empty, string.Empty, string.Empty);

        await _dispatcher.Received(1).SendToUserAsync(
            userId.ToString(),
            Arg.Any<RealtimeEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastToTenantAsync_WithEmptyStrings_StillDispatches()
    {
        TenantId tenantId = TenantId.New();

        await _sut.BroadcastToTenantAsync(tenantId, string.Empty, string.Empty, string.Empty);

        await _dispatcher.Received(1).SendToGroupAsync(
            $"tenant:{tenantId.Value}",
            Arg.Any<RealtimeEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToUserAsync_LogsInformation()
    {
        FakeLogger<SignalRNotificationService> fakeLogger = new();
        SignalRNotificationService sut = new(_dispatcher, _timeProvider, fakeLogger);
        Guid userId = Guid.NewGuid();

        await sut.SendToUserAsync(userId, "Log Title", "Log Message", "info");

        fakeLogger.LogEntries.Should().ContainSingle();
        FakeLogEntry entry = fakeLogger.LogEntries[0];
        entry.LogLevel.Should().Be(LogLevel.Information);
        entry.FormattedMessage.Should().Contain(userId.ToString());
        entry.FormattedMessage.Should().Contain("Log Title");
    }

    [Fact]
    public async Task BroadcastToTenantAsync_LogsInformation()
    {
        FakeLogger<SignalRNotificationService> fakeLogger = new();
        SignalRNotificationService sut = new(_dispatcher, _timeProvider, fakeLogger);
        TenantId tenantId = TenantId.New();

        await sut.BroadcastToTenantAsync(tenantId, "Broadcast Title", "Broadcast Msg", "announcement");

        fakeLogger.LogEntries.Should().ContainSingle();
        FakeLogEntry entry = fakeLogger.LogEntries[0];
        entry.LogLevel.Should().Be(LogLevel.Information);
        entry.FormattedMessage.Should().Contain(tenantId.Value.ToString());
        entry.FormattedMessage.Should().Contain("Broadcast Title");
    }

    [Fact]
    public void Service_ImplementsINotificationService()
    {
        _sut.Should().BeAssignableTo<INotificationService>();
    }

    [Fact]
    public async Task SendToUserAsync_WithDefaultCancellationToken_UsesDefault()
    {
        Guid userId = Guid.NewGuid();

        await _sut.SendToUserAsync(userId, "Title", "Message", "info");

        await _dispatcher.Received(1).SendToUserAsync(
            Arg.Any<string>(),
            Arg.Any<RealtimeEnvelope>(),
            default);
    }

    [Fact]
    public async Task BroadcastToTenantAsync_WithDefaultCancellationToken_UsesDefault()
    {
        TenantId tenantId = TenantId.New();

        await _sut.BroadcastToTenantAsync(tenantId, "Title", "Message", "info");

        await _dispatcher.Received(1).SendToGroupAsync(
            Arg.Any<string>(),
            Arg.Any<RealtimeEnvelope>(),
            default);
    }

    [Fact]
    public async Task SendToUserAsync_MultipleCalls_DispatchesEachIndependently()
    {
        Guid userId1 = Guid.NewGuid();
        Guid userId2 = Guid.NewGuid();

        await _sut.SendToUserAsync(userId1, "Title1", "Msg1", "info");
        await _sut.SendToUserAsync(userId2, "Title2", "Msg2", "warning");

        await _dispatcher.Received(1).SendToUserAsync(
            userId1.ToString(), Arg.Any<RealtimeEnvelope>(), Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).SendToUserAsync(
            userId2.ToString(), Arg.Any<RealtimeEnvelope>(), Arg.Any<CancellationToken>());
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
