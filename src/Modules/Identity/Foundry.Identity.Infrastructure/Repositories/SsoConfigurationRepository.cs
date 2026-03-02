using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Identity.Infrastructure.Repositories;

public sealed class SsoConfigurationRepository : ISsoConfigurationRepository
{
    private readonly IdentityDbContext _context;

    public SsoConfigurationRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public Task<SsoConfiguration?> GetAsync(CancellationToken ct = default)
    {
        // Each tenant has at most one SSO configuration
        return _context.SsoConfigurations.AsTracking().FirstOrDefaultAsync(ct);
    }

    public void Add(SsoConfiguration entity)
    {
        _context.SsoConfigurations.Add(entity);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
