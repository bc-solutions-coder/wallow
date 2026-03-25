using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Application.Interfaces;

public interface IOrganizationDomainRepository
{
    Task<OrganizationDomain?> GetByIdAsync(OrganizationDomainId id, CancellationToken ct = default);
    Task<OrganizationDomain?> GetByDomainAsync(string domain, CancellationToken ct = default);
    Task<List<OrganizationDomain>> GetByOrganizationIdAsync(OrganizationId organizationId, CancellationToken ct = default);
    void Add(OrganizationDomain entity);
    Task SaveChangesAsync(CancellationToken ct = default);
}
