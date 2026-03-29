using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Repositories;

public sealed class OrganizationDomainRepository(IdentityDbContext context) : IOrganizationDomainRepository
{
    public Task<OrganizationDomain?> GetByIdAsync(OrganizationDomainId id, CancellationToken ct = default)
    {
        return context.OrganizationDomains
            .AsTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public Task<OrganizationDomain?> GetByDomainAsync(string domain, CancellationToken ct = default)
    {
        string normalizedDomain = domain.ToLowerInvariant();
        return context.OrganizationDomains
            .FirstOrDefaultAsync(d => d.Domain == normalizedDomain, ct);
    }

    public Task<List<OrganizationDomain>> GetByOrganizationIdAsync(OrganizationId organizationId, CancellationToken ct = default)
    {
        return context.OrganizationDomains
            .Where(d => d.OrganizationId == organizationId)
            .OrderBy(d => d.Domain)
            .ToListAsync(ct);
    }

    public void Add(OrganizationDomain entity)
    {
        context.OrganizationDomains.Add(entity);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return context.SaveChangesAsync(ct);
    }
}
