using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Shared.Contracts.Realtime;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Infrastructure.Services;

public sealed partial class SignalRNotificationService(
    IRealtimeDispatcher dispatcher,
    TimeProvider timeProvider,
    ILogger<SignalRNotificationService> logger) : INotificationService
{

    public async Task SendToUserAsync(
        Guid userId,
        string title,
        string message,
        string type,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            Title = title,
            Message = message,
            Type = type,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Notifications", "NotificationCreated", payload);
        await dispatcher.SendToUserAsync(userId.ToString(), envelope, cancellationToken);

        LogSentToUser(logger, userId, title);
    }

    public async Task BroadcastToTenantAsync(
        TenantId tenantId,
        string title,
        string message,
        string type,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            Title = title,
            Message = message,
            Type = type,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Notifications", "AnnouncementPublished", payload);

        // Use tenant ID as the group ID - clients should join their tenant's group
        await dispatcher.SendToGroupAsync($"tenant:{tenantId.Value}", envelope, cancellationToken);

        LogBroadcastToTenant(logger, tenantId.Value, title);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent real-time notification to user {UserId}: {Title}")]
    private static partial void LogSentToUser(ILogger logger, Guid userId, string title);

    [LoggerMessage(Level = LogLevel.Information, Message = "Broadcast announcement notification to tenant {TenantId}: {Title}")]
    private static partial void LogBroadcastToTenant(ILogger logger, Guid tenantId, string title);
}
