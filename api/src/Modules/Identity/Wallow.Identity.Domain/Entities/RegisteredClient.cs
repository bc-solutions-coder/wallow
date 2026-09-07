using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

/// <summary>
/// Organization-owned client identity, lifecycle, and provenance. OAuth configuration
/// lives on the OpenIddict application. No tenant filter is applied here; callers
/// must enforce organization ownership and access policy.
/// </summary>
public sealed class RegisteredClient : Entity<RegisteredClientId>
{
    public string ClientId { get; private set; } = string.Empty;
    public Guid OrganizationId { get; private set; }

    /// <summary>
    /// Immutable registration name, separate from the mutable OpenIddict display name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;
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
    /// Platform suspension is independent of the owning organization's <see cref="Status"/>.
    /// Organization-level reinstatement does not clear it.
    /// </summary>
    public bool IsPlatformSuspended => PlatformSuspendedAt is not null;

    // ReSharper disable once UnusedMember.Local
    private RegisteredClient() { } // EF Core

    public static RegisteredClient Create(
        string clientId,
        Guid organizationId,
        string name,
        RegisteredClientKind kind,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new BusinessRuleException(IdentityErrors.ClientIdRequired);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException(IdentityErrors.ClientNameRequired);
        }

        if (organizationId == Guid.Empty)
        {
            throw new BusinessRuleException(IdentityErrors.ClientOrganizationRequired);
        }

        return new RegisteredClient
        {
            Id = RegisteredClientId.New(),
            ClientId = clientId,
            OrganizationId = organizationId,
            Name = name.Trim(),
            Kind = kind,
            Status = RegisteredClientStatus.Active,
            CreatedByUserId = createdByUserId,
            CreatedAt = timeProvider.GetUtcNow(),
        };
    }

    /// <summary>
    /// Records secret-rotation provenance; the secret remains on the OpenIddict application.
    /// </summary>
    public void RecordSecretRotation(Guid actorUserId, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        LastRotatedByUserId = actorUserId;
        LastRotatedAt = timeProvider.GetUtcNow();
    }

    /// <summary>
    /// Marks the client suspended without deleting its registration.
    /// The service revokes tokens separately.
    /// </summary>
    public void Suspend()
    {
        if (Status == RegisteredClientStatus.Suspended)
        {
            throw new BusinessRuleException(IdentityErrors.ClientAlreadySuspended);
        }

        Status = RegisteredClientStatus.Suspended;
    }

    public void Reinstate()
    {
        if (Status == RegisteredClientStatus.Active)
        {
            throw new BusinessRuleException(IdentityErrors.ClientNotSuspended);
        }

        Status = RegisteredClientStatus.Active;
    }

    /// <summary>
    /// Records the platform suspension reason, actor, and time.
    /// </summary>
    public void SuspendByPlatform(string reason, Guid actorId, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException(IdentityErrors.PlatformSuspensionReasonRequired);
        }

        if (IsPlatformSuspended)
        {
            throw new BusinessRuleException(IdentityErrors.ClientAlreadySuspendedByPlatform);
        }

        PlatformSuspendedAt = timeProvider.GetUtcNow();
        PlatformSuspendedBy = actorId;
        PlatformSuspensionReason = reason;
    }

    public void ReinstateByPlatform()
    {
        if (!IsPlatformSuspended)
        {
            throw new BusinessRuleException(IdentityErrors.ClientNotSuspendedByPlatform);
        }

        PlatformSuspendedAt = null;
        PlatformSuspendedBy = null;
        PlatformSuspensionReason = null;
    }
}
