using Microsoft.Extensions.Logging;
using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed partial class SseNotificationService(
    ISseDispatcher dispatcher,
    TimeProvider timeProvider,
    ILogger<SseNotificationService> logger) : INotificationService
{
    public async Task SendToUserAsync(
        Guid userId,
        string title,
        string message,
        string type,
        string? actionUrl = null,
        CancellationToken cancellationToken = default)
    {
        object payload = new
        {
            Title = title,
            Message = message,
            Type = type,
            ActionUrl = actionUrl,
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
        object payload = new
        {
            Title = title,
            Message = message,
            Type = type,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Notifications", "AnnouncementPublished", payload);
        await dispatcher.SendToTenantAsync(tenantId.Value, envelope, cancellationToken);

        LogBroadcastToTenant(logger, tenantId.Value, title);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent SSE notification to user {UserId}: {Title}")]
    private static partial void LogSentToUser(ILogger logger, Guid userId, string title);

    [LoggerMessage(Level = LogLevel.Information, Message = "Broadcast SSE announcement to tenant {TenantId}: {Title}")]
    private static partial void LogBroadcastToTenant(ILogger logger, Guid tenantId, string title);
}
