using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Auditing;
using Wolverine.Attributes;

namespace Wallow.Identity.Infrastructure.Handlers;

/// <summary>
/// The attribute is load-bearing. Wolverine's conventional discovery matches a type name ending in
/// "Handler" or "Consumer" and reads instance methods; a static class named "...Handlers" satisfies
/// neither, so without it every method here is silently unreachable and nothing is ever audited.
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
    /// The transition is spelled into the event type rather than kept in a column of its own, so a
    /// membership decision is queried the same way every other audited event is.
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
    /// A client has no user to be the subject, so the actor stands in both columns: the row is
    /// about what an admin did, and the client it was done to is named in its own column.
    /// </summary>
    public static Task Handle(ClientRegisteredEvent message, IAuthAuditService authAuditService) =>
        RecordClientEventAsync(
            "ClientRegistered", message.ClientId, message.OrganizationId, message.ActorId,
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
