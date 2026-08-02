using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Every decision that changes where somebody stands in an organization: a reviewer's five — let
/// them in, turn them away, let them ask again, take their access away, give it back — and the
/// member's own one, leaving.
///
/// One seam rather than five scattered methods, because each transition that ends access has to
/// revoke what the member already holds, and that is the step that gets forgotten when the
/// transitions live apart. Leaving belongs here for exactly that reason, even though it is the
/// only one the caller makes about themselves and so asks for no permission.
/// </summary>
public interface IMembershipReviewService
{
    /// <summary>
    /// The organization's outstanding access requests, oldest first: the queue a reviewer works
    /// through.
    /// </summary>
    Task<IReadOnlyList<PendingMembershipDto>> GetPendingAsync(
        Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// The memberships whose access is currently taken away, most recently suspended first. The
    /// list reinstatement is driven from: a suspended member appears on no other roster, so without
    /// it the decision can only be undone by somebody who already knows it was made.
    /// </summary>
    Task<IReadOnlyList<ReviewedMembershipDto>> GetSuspendedAsync(
        Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// The requests this organization turned away and has not taken back, most recently refused
    /// first. The list clearing a denial is driven from.
    /// </summary>
    Task<IReadOnlyList<ReviewedMembershipDto>> GetDeniedAsync(
        Guid organizationId, CancellationToken ct = default);

    Task ApproveAsync(Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default);

    Task DenyAsync(Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Lift a standing denial before it expires of its own accord, so the person may ask again now.
    /// The row is deleted rather than reversed: a denial nobody stands behind is not a decision
    /// worth keeping, and a leftover row would answer the next request on its own.
    /// </summary>
    Task ClearDenialAsync(
        Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Keeps the membership row so it can be reinstated, and takes away everything the member
    /// already holds here: their tokens for this organization and their open realtime connections
    /// to it.
    /// </summary>
    Task SuspendAsync(Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default);

    Task ReinstateAsync(Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// The caller gives up their own membership. The row is deleted rather than marked, so nothing
    /// stands in the way of them asking to join again — and their access to this organization ends
    /// with it.
    /// </summary>
    Task LeaveAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
}
