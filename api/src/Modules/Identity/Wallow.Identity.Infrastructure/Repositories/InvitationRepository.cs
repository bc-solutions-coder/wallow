using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Repositories;

public sealed class InvitationRepository(IdentityDbContext context) : IInvitationRepository
{
    public Task<Invitation?> GetByIdAsync(InvitationId id, CancellationToken ct = default)
    {
        return context.Invitations
            .AsTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public Task<Invitation?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return context.Invitations
            .AsTracking()
            .FirstOrDefaultAsync(i => i.Token == token, ct);
    }

    public Task<List<Invitation>> GetPagedByTenantAsync(Guid tenantId, int skip = 0, int take = 20, CancellationToken ct = default)
    {
        return context.Invitations
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
