using Foundry.Notifications.Infrastructure.Services;
using Foundry.Shared.Contracts.Realtime;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;

namespace Foundry.Notifications.Tests.Infrastructure.Services;

public class SignalRNotificationServiceTests
{
    private readonly IRealtimeDispatcher _dispatcher = Substitute.For<IRealtimeDispatcher>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly SignalRNotificationService _sut;

    public SignalRNotificationServiceTests()
    {
        _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero));
        _sut = new SignalRNotificationService(
            _dispatcher,
            _timeProvider,
            NullLogger<SignalRNotificationService>.Instance);
    }

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
}
