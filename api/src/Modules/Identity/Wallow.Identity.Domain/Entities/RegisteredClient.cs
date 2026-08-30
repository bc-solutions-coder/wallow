using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

/// <summary>
/// Wallow's own record of a client an organization registered: which organization owns it, what
/// kind it is, its lifecycle status and provenance. The OpenIddict application holds the OAuth
/// configuration (secret, redirect URIs, granted scopes); this row holds what OpenIddict has no
/// place for. Deliberately NOT tenant-scoped — an organization admin may address another
/// organization's clients through the access policy, which the tenant filter would defeat.
/// </summary>
public sealed class RegisteredClient : Entity<RegisteredClientId>
{
    public string ClientId { get; private set; } = string.Empty;
    public Guid OrganizationId { get; private set; }
    public RegisteredClientKind Kind { get; private set; }
    public RegisteredClientStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }
    public Guid? LastRotatedByUserId { get; private set; }
    public DateTimeOffset? LastRotatedAt { get; private set; }

    public DateTimeOffset? PlatformSuspendedAt { get; private set; }
    public Guid? PlatformSuspendedBy { get; private set; }
    public string? PlatformSuspensionReason { get; private set; }

    /// <summary>
    /// Whether the platform operator has taken this client out of service. A separate axis from
    /// <see cref="Status"/>: the owning organization's suspend and reinstate govern only its own
    /// status, and neither can lift what the platform placed.
    /// </summary>
    public bool IsPlatformSuspended => PlatformSuspendedAt is not null;

    // ReSharper disable once UnusedMember.Local
    private RegisteredClient() { } // EF Core

    public static RegisteredClient Create(
        string clientId,
        Guid organizationId,
        RegisteredClientKind kind,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new BusinessRuleException("Identity.ClientIdRequired", "Client id cannot be empty");
        }

        if (organizationId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Identity.ClientOrganizationRequired",
                "A registered client must belong to an organization");
        }

        return new RegisteredClient
        {
            Id = RegisteredClientId.New(),
            ClientId = clientId,
            OrganizationId = organizationId,
            Kind = kind,
            Status = RegisteredClientStatus.Active,
            CreatedByUserId = createdByUserId,
            CreatedAt = timeProvider.GetUtcNow(),
        };
    }

    /// <summary>
    /// Records who replaced the client secret and when. The secret itself lives on the OpenIddict
    /// application; this row only remembers the provenance a "who did this" question needs.
    /// </summary>
    public void RecordSecretRotation(Guid actorUserId, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        LastRotatedByUserId = actorUserId;
        LastRotatedAt = timeProvider.GetUtcNow();
    }

    /// <summary>
    /// Takes the client out of service without forgetting anything about it: configuration,
    /// branding and consents stay, so reinstating restores exactly what was there.
    /// </summary>
    public void Suspend()
    {
        if (Status == RegisteredClientStatus.Suspended)
        {
            throw new BusinessRuleException("Identity.ClientAlreadySuspended", "The client is already suspended");
        }

        Status = RegisteredClientStatus.Suspended;
    }

    public void Reinstate()
    {
        if (Status == RegisteredClientStatus.Active)
        {
            throw new BusinessRuleException("Identity.ClientNotSuspended", "The client is not suspended");
        }

        Status = RegisteredClientStatus.Active;
    }

    /// <summary>
    /// Places the platform operator's suspension, with the reason the owning organization's
    /// admins will read but cannot lift.
    /// </summary>
    public void SuspendByPlatform(string reason, Guid actorId, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException(
                "Identity.PlatformSuspensionReasonRequired",
                "A platform suspension requires a reason");
        }

        if (IsPlatformSuspended)
        {
            throw new BusinessRuleException(
                "Identity.ClientAlreadySuspendedByPlatform",
                "The client is already suspended by the platform");
        }

        PlatformSuspendedAt = timeProvider.GetUtcNow();
        PlatformSuspendedBy = actorId;
        PlatformSuspensionReason = reason;
    }

    public void ReinstateByPlatform()
    {
        if (!IsPlatformSuspended)
        {
            throw new BusinessRuleException(
                "Identity.ClientNotSuspendedByPlatform",
                "The client is not suspended by the platform");
        }

        PlatformSuspendedAt = null;
        PlatformSuspendedBy = null;
        PlatformSuspensionReason = null;
    }
}
