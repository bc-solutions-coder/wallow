using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Identity;
using Foundry.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Identity.Infrastructure.Repositories;

public sealed class OrganizationRepository(IdentityDbContext context) : IOrganizationRepository
{
    public Task<Organization?> GetByIdAsync(OrganizationId id, CancellationToken ct = default)
    {
        return context.Organizations
            .AsTracking()
            .Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<List<Organization>> GetAllAsync(string? search = null, int skip = 0, int take = 20, CancellationToken ct = default)
    {
        IQueryable<Organization> query = context.Organizations
            .Include(o => o.Members);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(o => o.Name.Contains(search));
        }

        return await query
            .OrderBy(o => o.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<List<Organization>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.Organizations
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
