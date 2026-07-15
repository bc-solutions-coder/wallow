using Microsoft.Extensions.Logging;
using Wallow.Shared.Contracts.Identity.Events;

namespace Wallow.Identity.Infrastructure.Handlers;

public static partial class EmailChangeHandlers
{
    public static void Handle(UserEmailChangeRequestedEvent message, ILogger logger)
    {
        LogEmailChangeRequested(logger, message.UserId, message.TenantId, message.NewEmail);
    }

    public static void Handle(UserEmailChangedEvent message, ILogger logger)
    {
        LogEmailChanged(logger, message.UserId, message.TenantId, message.OldEmail, message.NewEmail);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email change requested for user {UserId} in tenant {TenantId} to {NewEmail}")]
    private static partial void LogEmailChangeRequested(ILogger logger, Guid userId, Guid tenantId, string newEmail);

    [LoggerMessage(Level = LogLevel.Information, Message = "Email changed for user {UserId} in tenant {TenantId} from {OldEmail} to {NewEmail}")]
    private static partial void LogEmailChanged(ILogger logger, Guid userId, Guid tenantId, string oldEmail, string newEmail);
}
