using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Membership review and departure operations. Suspend and leave revoke access after
/// saving the membership change; leaving acts on the caller's own membership.
/// </summary>
public interface IMembershipReviewService
{
    /// <summary>
    /// Outstanding requests, oldest first; missing request timestamps sort last.
    /// </summary>
    Task<IReadOnlyList<PendingMembershipDto>> GetPendingAsync(
        Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Suspended memberships, most recently changed first.
    /// </summary>
    Task<IReadOnlyList<ReviewedMembershipDto>> GetSuspendedAsync(
        Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Denied memberships, most recently reviewed first.
    /// </summary>
    Task<IReadOnlyList<ReviewedMembershipDto>> GetDeniedAsync(
        Guid organizationId, CancellationToken ct = default);

    Task ApproveAsync(Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default);

    Task DenyAsync(Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a denied membership so the person can request access again immediately.
    /// </summary>
    Task ClearDenialAsync(
        Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Keeps the membership for reinstatement, then revokes organization-scoped tokens
    /// and requests disconnection of its realtime streams.
    /// </summary>
    Task SuspendAsync(Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default);

    Task ReinstateAsync(Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Deletes the caller's membership and revokes access to this organization.
    /// The last-owner guard can refuse the departure.
    /// </summary>
    Task LeaveAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
}
