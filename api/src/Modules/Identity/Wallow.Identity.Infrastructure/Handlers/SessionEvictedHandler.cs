using Microsoft.Extensions.Logging;
using Wallow.Shared.Contracts.Identity.Events;

namespace Wallow.Identity.Infrastructure.Handlers;

public static partial class SessionEvictedHandler
{
    public static void Handle(UserSessionEvictedEvent message, ILogger logger)
    {
        logger.LogSessionEvicted(message.SessionId, message.UserId, message.TenantId, message.Reason);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Session {SessionId} evicted for user {UserId} in tenant {TenantId}. Reason: {Reason}")]
    private static partial void LogSessionEvicted(this ILogger logger, Guid sessionId, Guid userId, Guid tenantId, string reason);
}
