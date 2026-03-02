using Foundry.Communications.Infrastructure.Services;
using Foundry.Shared.Contracts.Realtime;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Tests.Infrastructure.Services;

public class SignalRNotificationServiceTests
{
    private readonly IRealtimeDispatcher _dispatcher;
    private readonly SignalRNotificationService _service;

    public SignalRNotificationServiceTests()
    {
        _dispatcher = Substitute.For<IRealtimeDispatcher>();
        ILogger<SignalRNotificationService> logger = Substitute.For<ILogger<SignalRNotificationService>>();
        _service = new SignalRNotificationService(_dispatcher, TimeProvider.System, logger);
    }

    [Fact]
    public async Task SendToUserAsync_DispatchesEnvelopeToUser()
    {
        Guid userId = Guid.NewGuid();

        await _service.SendToUserAsync(userId, "Test Title", "Test Message", "SystemAlert");

        await _dispatcher.Received(1).SendToUserAsync(
            userId.ToString(),
            Arg.Is<RealtimeEnvelope>(e =>
                e.Module == "Notifications" &&
                e.Type == "NotificationCreated"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToUserAsync_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        Guid userId = Guid.NewGuid();

        await _service.SendToUserAsync(userId, "Title", "Message", "Alert", cts.Token);

        await _dispatcher.Received(1).SendToUserAsync(
            Arg.Any<string>(),
            Arg.Any<RealtimeEnvelope>(),
            cts.Token);
    }

    [Fact]
    public async Task BroadcastToTenantAsync_DispatchesEnvelopeToTenantGroup()
    {
        TenantId tenantId = TenantId.New();

        await _service.BroadcastToTenantAsync(tenantId, "Announcement Title", "Announcement Message", "Announcement");

        await _dispatcher.Received(1).SendToGroupAsync(
            $"tenant:{tenantId.Value}",
            Arg.Is<RealtimeEnvelope>(e =>
                e.Module == "Notifications" &&
                e.Type == "AnnouncementPublished"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastToTenantAsync_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        TenantId tenantId = TenantId.New();

        await _service.BroadcastToTenantAsync(tenantId, "Title", "Message", "Alert", cts.Token);

        await _dispatcher.Received(1).SendToGroupAsync(
            Arg.Any<string>(),
            Arg.Any<RealtimeEnvelope>(),
            cts.Token);
    }

    [Fact]
    public async Task SendToUserAsync_EnvelopeHasTimestamp()
    {
        Guid userId = Guid.NewGuid();
        DateTime before = DateTime.UtcNow;

        await _service.SendToUserAsync(userId, "Title", "Message", "Alert");

        await _dispatcher.Received(1).SendToUserAsync(
            Arg.Any<string>(),
            Arg.Is<RealtimeEnvelope>(e => e.Timestamp >= before),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastToTenantAsync_EnvelopeHasTimestamp()
    {
        TenantId tenantId = TenantId.New();
        DateTime before = DateTime.UtcNow;

        await _service.BroadcastToTenantAsync(tenantId, "Title", "Message", "Alert");

        await _dispatcher.Received(1).SendToGroupAsync(
            Arg.Any<string>(),
            Arg.Is<RealtimeEnvelope>(e => e.Timestamp >= before),
            Arg.Any<CancellationToken>());
    }
}
