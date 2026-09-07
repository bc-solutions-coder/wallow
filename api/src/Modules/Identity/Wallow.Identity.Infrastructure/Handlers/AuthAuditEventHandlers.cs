using Wallow.Shared.Contracts.Branding.Events;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Auditing;
using Wolverine.Attributes;

namespace Wallow.Identity.Infrastructure.Handlers;

/// <summary>
/// Records authentication and governance events. WolverineHandler explicitly includes this static handler class.
/// </summary>
[WolverineHandler]
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

    /// <summary>
    /// Encodes the membership transition in EventType for audit filtering.
    /// </summary>
    public static Task Handle(MembershipTransitionedEvent message, IAuthAuditService authAuditService)
    {
        return authAuditService.RecordAsync(new AuthAuditRecord
        {
            EventType = $"Membership{message.Transition}",
            UserId = message.UserId,
            ActorId = message.ActorId,
            TenantId = message.TenantId,
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

    /// <summary>
    /// Uses the actor as the audit subject and records the affected client separately.
    /// </summary>
    public static Task Handle(ClientRegisteredEvent message, IAuthAuditService authAuditService) =>
        RecordClientEventAsync(
            "ClientRegistered", message.ClientId, message.OrganizationId, message.ActorId,
            message.IpAddress, message.OccurredAt, authAuditService);

    public static Task Handle(ClientBrandingUpdatedEvent message, IAuthAuditService authAuditService) =>
        RecordClientEventAsync(
            "ClientBrandingUpdated", message.ClientId, message.OrganizationId, message.ActorId,
            message.IpAddress, message.OccurredAt, authAuditService);

    public static Task Handle(ClientSecretRotatedEvent message, IAuthAuditService authAuditService) =>
        RecordClientEventAsync(
            "ClientSecretRotated", message.ClientId, message.OrganizationId, message.ActorId,
            message.IpAddress, message.OccurredAt, authAuditService);

    public static Task Handle(ClientSuspendedEvent message, IAuthAuditService authAuditService) =>
        RecordClientEventAsync(
            "ClientSuspended", message.ClientId, message.OrganizationId, message.ActorId,
            message.IpAddress, message.OccurredAt, authAuditService);

    public static Task Handle(ClientReinstatedEvent message, IAuthAuditService authAuditService) =>
        RecordClientEventAsync(
            "ClientReinstated", message.ClientId, message.OrganizationId, message.ActorId,
            message.IpAddress, message.OccurredAt, authAuditService);

    public static Task Handle(ClientSuspendedByPlatformEvent message, IAuthAuditService authAuditService) =>
        RecordClientEventAsync(
            "ClientSuspendedByPlatform", message.ClientId, message.OrganizationId, message.ActorId,
            message.IpAddress, message.OccurredAt, authAuditService, message.Reason);

    public static Task Handle(ClientReinstatedByPlatformEvent message, IAuthAuditService authAuditService) =>
        RecordClientEventAsync(
            "ClientReinstatedByPlatform", message.ClientId, message.OrganizationId, message.ActorId,
            message.IpAddress, message.OccurredAt, authAuditService);

    public static Task Handle(OrganizationSuspendedByPlatformEvent message, IAuthAuditService authAuditService) =>
        RecordOrganizationEventAsync(
            "OrganizationSuspendedByPlatform", message.OrganizationId, message.ActorId,
            message.OccurredAt, authAuditService, message.Reason);

    public static Task Handle(OrganizationReinstatedByPlatformEvent message, IAuthAuditService authAuditService) =>
        RecordOrganizationEventAsync(
            "OrganizationReinstatedByPlatform", message.OrganizationId, message.ActorId,
            message.OccurredAt, authAuditService);

    public static Task Handle(ClientDeletedEvent message, IAuthAuditService authAuditService) =>
        RecordClientEventAsync(
            "ClientDeleted", message.ClientId, message.OrganizationId, message.ActorId,
            message.IpAddress, message.OccurredAt, authAuditService);

    public static Task Handle(OrganizationDeletedEvent message, IAuthAuditService authAuditService) =>
        RecordOrganizationEventAsync(
            "OrganizationDeleted", message.OrganizationId, message.ActorId,
            message.OccurredAt, authAuditService);

    private static Task RecordClientEventAsync(
        string eventType,
        string clientId,
        Guid organizationId,
        Guid actorId,
        string? ipAddress,
        DateTimeOffset occurredAt,
        IAuthAuditService authAuditService,
        string? reason = null)
    {
        return authAuditService.RecordAsync(new AuthAuditRecord
        {
            EventType = eventType,
            UserId = actorId,
            ActorId = actorId,
            TenantId = organizationId,
            ClientId = clientId,
            IpAddress = ipAddress,
            Reason = reason,
            OccurredAt = occurredAt
        }, CancellationToken.None);
    }

    private static Task RecordOrganizationEventAsync(
        string eventType,
        Guid organizationId,
        Guid actorId,
        DateTimeOffset occurredAt,
        IAuthAuditService authAuditService,
        string? reason = null)
    {
        return authAuditService.RecordAsync(new AuthAuditRecord
        {
            EventType = eventType,
            UserId = actorId,
            ActorId = actorId,
            TenantId = organizationId,
            Reason = reason,
            OccurredAt = occurredAt
        }, CancellationToken.None);
    }
}
