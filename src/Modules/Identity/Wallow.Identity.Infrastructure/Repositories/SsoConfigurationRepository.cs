using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Repositories;

public sealed class SsoConfigurationRepository(IdentityDbContext context) : ISsoConfigurationRepository
{

    public Task<SsoConfiguration?> GetAsync(CancellationToken ct = default)
    {
        // Each tenant has at most one SSO configuration
        return context.SsoConfigurations.AsTracking().FirstOrDefaultAsync(ct);
    }

    public void Add(SsoConfiguration entity)
    {
        context.SsoConfigurations.Add(entity);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await context.SaveChangesAsync(ct);
    }
}
