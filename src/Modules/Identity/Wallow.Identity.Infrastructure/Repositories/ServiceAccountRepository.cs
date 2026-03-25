using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Identity.Infrastructure.Repositories;

public sealed class ServiceAccountRepository(IdentityDbContext context) : IServiceAccountRepository, IServiceAccountUnfilteredRepository
{
    private static readonly Func<IdentityDbContext, string, Task<ServiceAccountMetadata?>>
        _getByClientIdQuery = EF.CompileAsyncQuery(
            (IdentityDbContext ctx, string clientId) =>
                ctx.ServiceAccountMetadata
                    .AsTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefault(x => x.ClientId == clientId));

    public Task<ServiceAccountMetadata?> GetByIdAsync(ServiceAccountMetadataId id, CancellationToken ct = default)
    {
        return context.ServiceAccountMetadata
            .AsTracking()
            .Where(x => x.Status != Domain.Enums.ServiceAccountStatus.Revoked)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    /// <summary>
    /// Resolves a service account by its client ID, bypassing tenant query filters (IgnoreQueryFilters).
    /// For internal cross-layer use only (e.g., middleware that must identify service accounts before tenant context is established).
    /// </summary>
    Task<ServiceAccountMetadata?> IServiceAccountUnfilteredRepository.GetByClientIdAsync(string clientId, CancellationToken ct)
    {
        return _getByClientIdQuery(context, clientId);
    }

    public async Task<IReadOnlyList<ServiceAccountMetadata>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.ServiceAccountMetadata
            .Where(x => x.Status != Domain.Enums.ServiceAccountStatus.Revoked)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public void Add(ServiceAccountMetadata entity)
    {
        context.ServiceAccountMetadata.Add(entity);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await context.SaveChangesAsync(ct);
    }
}
