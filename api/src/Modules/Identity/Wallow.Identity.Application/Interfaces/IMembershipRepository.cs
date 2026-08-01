using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Application.Interfaces;

public interface IMembershipRepository
{
    /// <summary>
    /// The membership joining one person to one organization, resolvable regardless of the
    /// ambient tenant — this runs at authorize time, before any tenant is resolved.
    /// </summary>
    Task<Membership?> GetAsync(Guid userId, Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Every membership a person holds, across organizations. Also an authorize-time read.
    /// </summary>
    Task<IReadOnlyList<Membership>> GetForUserAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<Membership>> GetForOrganizationAsync(
        Guid organizationId,
        MembershipStatus? status = null,
        CancellationToken ct = default);

    /// <summary>
    /// Active-membership counts keyed by organization id, for the roster count a listing shows.
    /// Organizations with no active member are absent from the result rather than zero.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountActiveByOrganizationAsync(
        IReadOnlyCollection<Guid> organizationIds,
        CancellationToken ct = default);

    void Add(Membership membership);

    void Remove(Membership membership);

    Task SaveChangesAsync(CancellationToken ct = default);
}
