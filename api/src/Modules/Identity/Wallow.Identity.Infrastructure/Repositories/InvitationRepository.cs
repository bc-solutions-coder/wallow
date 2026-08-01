using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Infrastructure.Repositories;

public sealed class InvitationRepository(IdentityDbContext context) : IInvitationRepository
{
    public Task<Invitation?> GetByIdAsync(InvitationId id, CancellationToken ct = default)
    {
        return context.Invitations
            .AsTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    /// <summary>
    /// Resolves an invitation by its token, bypassing tenant query filters (IgnoreQueryFilters).
    /// Neither caller can supply the tenant the invitation belongs to: verification is anonymous, so
    /// no tenant resolves at all, and acceptance runs as the invited person, whose ambient tenant is
    /// by definition an organization other than the one inviting them. The token is 32 bytes of
    /// cryptographic randomness and is itself the selector.
    /// </summary>
    public Task<Invitation?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return context.Invitations
            .AsTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Token == token, ct);
    }

    /// <summary>
    /// Lists one organization's invitations, scoped on the parameter rather than on the ambient
    /// query filter. A method named for a tenant it does not filter by is one "cleanup" away from
    /// returning every invited email address in the system.
    /// </summary>
    public Task<List<Invitation>> GetPagedByTenantAsync(Guid tenantId, int skip = 0, int take = 20, CancellationToken ct = default)
    {
        TenantId scope = TenantId.Create(tenantId);

        return context.Invitations
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == scope)
            .OrderByDescending(i => i.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public void Add(Invitation invitation)
    {
        context.Invitations.Add(invitation);
    }

    public void Delete(Invitation invitation)
    {
        context.Invitations.Remove(invitation);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return context.SaveChangesAsync(ct);
    }
}
