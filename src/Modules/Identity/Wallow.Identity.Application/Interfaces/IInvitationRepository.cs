using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Application.Interfaces;

public interface IInvitationRepository
{
    Task<Invitation?> GetByIdAsync(InvitationId id, CancellationToken ct = default);
    Task<Invitation?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<List<Invitation>> GetPagedByTenantAsync(Guid tenantId, int skip = 0, int take = 20, CancellationToken ct = default);
    void Add(Invitation invitation);
    void Delete(Invitation invitation);
    Task SaveChangesAsync(CancellationToken ct = default);
}
