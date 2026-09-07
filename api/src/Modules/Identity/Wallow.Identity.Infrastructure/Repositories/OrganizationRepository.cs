using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Repositories;

public sealed class OrganizationRepository(IdentityDbContext context) : IOrganizationRepository
{
    // Cross-organization reads bypass tenant filters; callers must authorize the addressed
    // organization, not merely a permission held in their ambient tenant.
    public Task<Organization?> GetByIdAsync(OrganizationId id, CancellationToken ct = default)
    {
        return context.Organizations
            .AsTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public Task<List<Organization>> GetAllAsync(string? search = null, int skip = 0, int take = 20, CancellationToken ct = default)
    {
        IQueryable<Organization> query = context.Organizations
            .IgnoreQueryFilters();

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

    // Membership status controls inclusion; this query does not filter organization state.
    public Task<List<Organization>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        IQueryable<OrganizationId> activeOrganizationIds = context.Memberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
            .Select(m => m.OrganizationId);

        return context.Organizations
            .IgnoreQueryFilters()
            .Where(o => activeOrganizationIds.Contains(o.Id))
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
