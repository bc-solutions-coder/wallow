using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Repositories;

public sealed class OrganizationRepository(IdentityDbContext context) : IOrganizationRepository
{
    // Organization IS the tenant (org.Id == TenantId by construction), so the ambient
    // tenant query filter would hide every org whose id does not equal the caller's tenant.
    // Addressing an org by id is instead authorized at the controller via [HasPermission],
    // so these reads bypass the tenant filter with IgnoreQueryFilters.
    public Task<Organization?> GetByIdAsync(OrganizationId id, CancellationToken ct = default)
    {
        return context.Organizations
            .AsTracking()
            .IgnoreQueryFilters()
            .Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public Task<List<Organization>> GetAllAsync(string? search = null, int skip = 0, int take = 20, CancellationToken ct = default)
    {
        IQueryable<Organization> query = context.Organizations
            .IgnoreQueryFilters()
            .Include(o => o.Members);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(o => o.Name.Contains(search));
        }

        return query
            .OrderBy(o => o.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<List<Organization>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return context.Organizations
            .IgnoreQueryFilters()
            .Include(o => o.Members)
            .Where(o => o.Members.Any(m => m.UserId == userId))
            .OrderBy(o => o.Name)
            .ToListAsync(ct);
    }

    public void Add(Organization organization)
    {
        context.Organizations.Add(organization);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return context.SaveChangesAsync(ct);
    }
}
