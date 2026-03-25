using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Identity.Infrastructure.Repositories;

public sealed class MembershipRequestRepository(IdentityDbContext context) : IMembershipRequestRepository
{
    public Task<MembershipRequest?> GetByIdAsync(MembershipRequestId id, CancellationToken ct = default)
    {
        return context.MembershipRequests
            .AsTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public Task<List<MembershipRequest>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return context.MembershipRequests
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<List<MembershipRequest>> GetByOrganizationIdAsync(OrganizationId organizationId, CancellationToken ct = default)
    {
        return context.MembershipRequests
            .Where(r => r.ResolvedOrganizationId == organizationId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<List<MembershipRequest>> GetPendingAsync(int skip = 0, int take = 20, CancellationToken ct = default)
    {
        return context.MembershipRequests
            .Where(r => r.Status == MembershipRequestStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public void Add(MembershipRequest entity)
    {
        context.MembershipRequests.Add(entity);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return context.SaveChangesAsync(ct);
    }
}
