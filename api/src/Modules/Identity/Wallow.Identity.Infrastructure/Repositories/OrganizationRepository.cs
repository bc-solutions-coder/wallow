using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Repositories;

public sealed class OrganizationRepository(IdentityDbContext context) : IOrganizationRepository
{
    public Task<Organization?> GetByIdAsync(OrganizationId id, CancellationToken ct = default)
    {
        return context.Organizations
            .AsTracking()
            .Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public Task<List<Organization>> GetAllAsync(string? search = null, int skip = 0, int take = 20, CancellationToken ct = default)
    {
        IQueryable<Organization> query = context.Organizations
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
