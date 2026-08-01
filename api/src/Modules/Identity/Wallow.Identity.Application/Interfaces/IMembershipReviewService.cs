using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// The four decisions somebody makes about another person's membership: let them in, turn them
/// away, take their access away, give it back.
///
/// One seam rather than four scattered methods, because they share a precondition nothing else
/// does — the actor must be allowed to manage this organization's members — and because each
/// transition that ends access has to revoke what the member already holds, which is the step
/// that gets forgotten when the transitions live apart.
/// </summary>
public interface IMembershipReviewService
{
    /// <summary>
    /// The organization's outstanding access requests, oldest first: the queue a reviewer works
    /// through.
    /// </summary>
    Task<IReadOnlyList<PendingMembershipDto>> GetPendingAsync(
        Guid organizationId, CancellationToken ct = default);

    Task ApproveAsync(Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default);

    Task DenyAsync(Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Keeps the membership row so it can be reinstated, and takes away everything the member
    /// already holds here: their tokens for this organization and their open realtime connections
    /// to it.
    /// </summary>
    Task SuspendAsync(Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default);

    Task ReinstateAsync(Guid organizationId, Guid userId, Guid actorId, CancellationToken ct = default);
}
