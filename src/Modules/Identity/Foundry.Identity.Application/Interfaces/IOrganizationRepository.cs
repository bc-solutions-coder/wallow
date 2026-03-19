using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Identity;

namespace Foundry.Identity.Application.Interfaces;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(OrganizationId id, CancellationToken ct = default);
    Task<List<Organization>> GetAllAsync(string? search = null, int skip = 0, int take = 20, CancellationToken ct = default);
    Task<List<Organization>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    void Add(Organization organization);
    Task SaveChangesAsync(CancellationToken ct = default);
}
