using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Repositories;

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
