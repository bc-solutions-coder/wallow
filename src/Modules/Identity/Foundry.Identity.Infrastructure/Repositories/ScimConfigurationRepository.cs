using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Identity.Infrastructure.Repositories;

public sealed class ScimConfigurationRepository(IdentityDbContext context) : IScimConfigurationRepository
{
    private static readonly Func<IdentityDbContext, Task<ScimConfiguration?>>
        _getQuery = EF.CompileAsyncQuery(
            (IdentityDbContext ctx) =>
                ctx.ScimConfigurations
                    .AsTracking()
                    .FirstOrDefault());

    public Task<ScimConfiguration?> GetAsync(CancellationToken ct = default)
    {
        // Each tenant has at most one SCIM configuration
        return _getQuery(context);
    }

    public void Add(ScimConfiguration entity)
    {
        context.ScimConfigurations.Add(entity);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await context.SaveChangesAsync(ct);
    }
}
