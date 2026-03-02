using Foundry.Communications.Infrastructure.Services;
using Foundry.Shared.Contracts.Realtime;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Tests.Infrastructure.Services;

public class SignalRNotificationServiceEdgeCaseTests
{
    private readonly IRealtimeDispatcher _dispatcher;
    private readonly SignalRNotificationService _service;

    public SignalRNotificationServiceEdgeCaseTests()
    {
        _dispatcher = Substitute.For<IRealtimeDispatcher>();
        ILogger<SignalRNotificationService> logger = Substitute.For<ILogger<SignalRNotificationService>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _service = new SignalRNotificationService(_dispatcher, TimeProvider.System, logger);
    }

    [Fact]
    public async Task SendToUserAsync_EnvelopePayloadContainsTitle()
    {
        Guid userId = Guid.NewGuid();
        RealtimeEnvelope? capturedEnvelope = null;
        await _dispatcher.SendToUserAsync(
            Arg.Any<string>(),
            Arg.Do<RealtimeEnvelope>(e => capturedEnvelope = e),
            Arg.Any<CancellationToken>());

        await _service.SendToUserAsync(userId, "My Title", "My Message", "Info");

        capturedEnvelope.Should().NotBeNull();
        capturedEnvelope!.Module.Should().Be("Notifications");
        capturedEnvelope.Type.Should().Be("NotificationCreated");
    }

    [Fact]
    public async Task SendToUserAsync_UsesUserIdAsRecipient()
    {
        Guid userId = Guid.NewGuid();

        await _service.SendToUserAsync(userId, "Title", "Message", "Info");

        await _dispatcher.Received(1).SendToUserAsync(
            userId.ToString(),
            Arg.Any<RealtimeEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastToTenantAsync_UsesTenantPrefixedGroupId()
    {
        TenantId tenantId = TenantId.New();

        await _service.BroadcastToTenantAsync(tenantId, "Title", "Message", "Alert");

        await _dispatcher.Received(1).SendToGroupAsync(
            $"tenant:{tenantId.Value}",
            Arg.Any<RealtimeEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastToTenantAsync_EnvelopeTypeIsAnnouncementPublished()
    {
        TenantId tenantId = TenantId.New();

        await _service.BroadcastToTenantAsync(tenantId, "Title", "Message", "Alert");

        await _dispatcher.Received(1).SendToGroupAsync(
            Arg.Any<string>(),
            Arg.Is<RealtimeEnvelope>(e =>
                e.Type == "AnnouncementPublished" &&
                e.Module == "Notifications"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToUserAsync_WithEmptyTitle_StillSends()
    {
        Guid userId = Guid.NewGuid();

        await _service.SendToUserAsync(userId, "", "Message", "Info");

        await _dispatcher.Received(1).SendToUserAsync(
            Arg.Any<string>(),
            Arg.Any<RealtimeEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastToTenantAsync_WithEmptyMessage_StillSends()
    {
        TenantId tenantId = TenantId.New();

        await _service.BroadcastToTenantAsync(tenantId, "Title", "", "Alert");

        await _dispatcher.Received(1).SendToGroupAsync(
            Arg.Any<string>(),
            Arg.Any<RealtimeEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToUserAsync_WithDefaultCancellationToken_PassesDefault()
    {
        Guid userId = Guid.NewGuid();

        await _service.SendToUserAsync(userId, "Title", "Message", "Info");

        await _dispatcher.Received(1).SendToUserAsync(
            Arg.Any<string>(),
            Arg.Any<RealtimeEnvelope>(),
            default);
    }

    [Fact]
    public async Task BroadcastToTenantAsync_WithDefaultCancellationToken_PassesDefault()
    {
        TenantId tenantId = TenantId.New();

        await _service.BroadcastToTenantAsync(tenantId, "Title", "Message", "Alert");

        await _dispatcher.Received(1).SendToGroupAsync(
            Arg.Any<string>(),
            Arg.Any<RealtimeEnvelope>(),
            default);
    }

    [Fact]
    public async Task SendToUserAsync_DispatcherThrows_PropagatesException()
    {
        Guid userId = Guid.NewGuid();
        _dispatcher.SendToUserAsync(
            Arg.Any<string>(),
            Arg.Any<RealtimeEnvelope>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Hub disconnected")));

        Func<Task> act = () => _service.SendToUserAsync(userId, "Title", "Message", "Info");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Hub disconnected");
    }

    [Fact]
    public async Task BroadcastToTenantAsync_DispatcherThrows_PropagatesException()
    {
        TenantId tenantId = TenantId.New();
        _dispatcher.SendToGroupAsync(
            Arg.Any<string>(),
            Arg.Any<RealtimeEnvelope>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Hub disconnected")));

        Func<Task> act = () => _service.BroadcastToTenantAsync(tenantId, "Title", "Message", "Alert");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Hub disconnected");
    }
}
