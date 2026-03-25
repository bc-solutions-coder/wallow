using Microsoft.EntityFrameworkCore;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Branding.Infrastructure.Persistence;

namespace Wallow.Branding.Infrastructure.Repositories;

public sealed class ClientBrandingRepository(BrandingDbContext context) : IClientBrandingRepository
{
    public Task<ClientBranding?> GetByClientIdAsync(string clientId, CancellationToken ct = default)
    {
        return context.ClientBrandings
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
