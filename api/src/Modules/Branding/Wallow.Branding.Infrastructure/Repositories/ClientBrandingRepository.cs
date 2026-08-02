using Microsoft.EntityFrameworkCore;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Branding.Infrastructure.Persistence;

namespace Wallow.Branding.Infrastructure.Repositories;

public sealed class ClientBrandingRepository(BrandingDbContext context) : IClientBrandingRepository
{
    /// <summary>
    /// Resolves branding by client ID, bypassing tenant query filters (IgnoreQueryFilters).
    /// client_id carries a repo-wide unique index, so no tenant partitions this table and the
    /// parameter is itself the selector. The public read is anonymous, so no tenant resolves at
    /// all; the writes are authorized on the OIDC application's creatorUserId — which OpenIddict
    /// stores globally — rather than on the ambient tenant, and a filtered miss would send
    /// UpsertBranding down its insert branch and into that unique index.
    /// </summary>
    public Task<ClientBranding?> GetByClientIdAsync(string clientId, CancellationToken ct = default)
    {
        return context.ClientBrandings
            .AsTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.ClientId == clientId, ct);
    }

    public void Add(ClientBranding branding)
    {
        context.ClientBrandings.Add(branding);
    }

    public void Remove(ClientBranding branding)
    {
        context.ClientBrandings.Remove(branding);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return context.SaveChangesAsync(ct);
    }
}
