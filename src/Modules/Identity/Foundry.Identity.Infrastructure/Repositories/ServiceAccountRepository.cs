using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Identity;
using Foundry.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Identity.Infrastructure.Repositories;

public sealed class ServiceAccountRepository : IServiceAccountRepository
{
    private static readonly Func<IdentityDbContext, string, Task<ServiceAccountMetadata?>>
        _getByKeycloakClientIdQuery = EF.CompileAsyncQuery(
            (IdentityDbContext ctx, string keycloakClientId) =>
                ctx.ServiceAccountMetadata
                    .AsTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefault(x => x.KeycloakClientId == keycloakClientId));

    private readonly IdentityDbContext _context;

    public ServiceAccountRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public Task<ServiceAccountMetadata?> GetByIdAsync(ServiceAccountMetadataId id, CancellationToken ct = default)
    {
        return _context.ServiceAccountMetadata
            .AsTracking()
            .Where(x => x.Status != Domain.Enums.ServiceAccountStatus.Revoked)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<ServiceAccountMetadata?> GetByKeycloakClientIdAsync(string keycloakClientId, CancellationToken ct = default)
    {
        // Need to bypass tenant filter for middleware lookups
        return _getByKeycloakClientIdQuery(_context, keycloakClientId);
    }

    public async Task<IReadOnlyList<ServiceAccountMetadata>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.ServiceAccountMetadata
            .Where(x => x.Status != Domain.Enums.ServiceAccountStatus.Revoked)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public void Add(ServiceAccountMetadata entity)
    {
        _context.ServiceAccountMetadata.Add(entity);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
