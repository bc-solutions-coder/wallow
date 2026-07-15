using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Application.Interfaces;

public interface IMembershipRequestRepository
{
    Task<MembershipRequest?> GetByIdAsync(MembershipRequestId id, CancellationToken ct = default);
    Task<List<MembershipRequest>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<List<MembershipRequest>> GetByOrganizationIdAsync(OrganizationId organizationId, CancellationToken ct = default);
    Task<List<MembershipRequest>> GetPendingAsync(int skip = 0, int take = 20, CancellationToken ct = default);
    void Add(MembershipRequest entity);
    Task SaveChangesAsync(CancellationToken ct = default);
}
