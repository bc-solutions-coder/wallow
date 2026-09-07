using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
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
    /// Looks up a token across tenants for anonymous verification and acceptance.
    /// The accepting caller may not have the inviting organization as its ambient tenant.
    /// </summary>
    public Task<Invitation?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return context.Invitations
            .AsTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Token == token, ct);
    }

    /// <summary>
    /// Lists invitations for the supplied tenant, independently of the ambient tenant filter.
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

    public Task<Invitation?> GetPendingByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
    {
        TenantId scope = TenantId.Create(tenantId);
        string normalized = email.ToUpperInvariant();

        // Compare uppercase values without ILike: an underscore in an email is literal,
        // not a pattern wildcard. The relational provider translates ToUpper to SQL.
#pragma warning disable CA1304, CA1311, CA1862
        return context.Invitations
            .AsTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                i => i.TenantId == scope
                    && i.Status == InvitationStatus.Pending
                    && i.Email.ToUpper() == normalized,
                ct);
#pragma warning restore CA1304, CA1311, CA1862
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
