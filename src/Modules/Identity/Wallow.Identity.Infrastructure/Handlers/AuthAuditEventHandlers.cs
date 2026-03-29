using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Auditing;

namespace Wallow.Identity.Infrastructure.Handlers;

public static class AuthAuditEventHandlers
{
    public static Task Handle(UserLoginSucceededEvent message, IAuthAuditService authAuditService)
    {
        return authAuditService.RecordAsync(new AuthAuditRecord
        {
            EventType = "LoginSucceeded",
            UserId = message.UserId,
            TenantId = message.TenantId,
            IpAddress = message.IpAddress,
            OccurredAt = message.OccurredAt
        }, CancellationToken.None);
    }

    public static Task Handle(UserLoginFailedEvent message, IAuthAuditService authAuditService)
    {
        return authAuditService.RecordAsync(new AuthAuditRecord
        {
            EventType = "LoginFailed",
            UserId = message.UserId,
            TenantId = message.TenantId,
            IpAddress = message.IpAddress,
            OccurredAt = message.OccurredAt
        }, CancellationToken.None);
    }

    public static Task Handle(UserAccountLockedOutEvent message, IAuthAuditService authAuditService)
    {
        return authAuditService.RecordAsync(new AuthAuditRecord
        {
            EventType = "AccountLockedOut",
            UserId = message.UserId,
            TenantId = message.TenantId,
            IpAddress = message.IpAddress,
            OccurredAt = message.OccurredAt
        }, CancellationToken.None);
    }

    public static Task Handle(UserMfaLockedOutEvent message, IAuthAuditService authAuditService)
    {
        return authAuditService.RecordAsync(new AuthAuditRecord
        {
            EventType = "MfaLockedOut",
            UserId = message.UserId,
            TenantId = message.TenantId,
            OccurredAt = message.OccurredAt
        }, CancellationToken.None);
    }
}
