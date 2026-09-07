using Microsoft.Extensions.Logging;
using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Shared.Contracts.Realtime;

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent SSE notification to user {UserId}: {Title}")]
    private static partial void LogSentToUser(ILogger logger, Guid userId, string title);
}
