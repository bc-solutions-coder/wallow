using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Repositories;

public sealed class MembershipRepository(IdentityDbContext context) : IMembershipRepository
{
    // GetAsync and GetForUserAsync run at authorize time, before a tenant is resolved, so they
    // bypass the ambient filter the way OrganizationRepository.GetByUserIdAsync does. Both still
    // constrain on their own parameters — never on the ambient tenant.
    public Task<Membership?> GetAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        OrganizationId typedOrganizationId = OrganizationId.Create(organizationId);

        return context.Memberships
            .AsTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == typedOrganizationId, ct);
    }

    public async Task<IReadOnlyList<Membership>> GetForUserAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await context.Memberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == userId)
            .ToListAsync(ct);
    }

    // Runs inside authenticated, tenant-resolved handling, so the ambient filter stays on as a
    // backstop under the roster endpoints.
    public async Task<IReadOnlyList<Membership>> GetForOrganizationAsync(
        Guid organizationId,
        MembershipStatus? status = null,
        CancellationToken ct = default)
    {
        OrganizationId typedOrganizationId = OrganizationId.Create(organizationId);

        IQueryable<Membership> query = context.Memberships
            .Where(m => m.OrganizationId == typedOrganizationId);

        if (status is not null)
        {
            query = query.Where(m => m.Status == status.Value);
        }

        return await query.ToListAsync(ct);
    }

    public void Add(Membership membership)
    {
        context.Memberships.Add(membership);
    }

    public void Remove(Membership membership)
    {
        context.Memberships.Remove(membership);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return context.SaveChangesAsync(ct);
    }
}
