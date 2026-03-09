using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Identity.Infrastructure.Repositories;

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
