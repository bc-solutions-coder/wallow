using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Infrastructure.Repositories;

/// <summary>
/// Internal repository interface for service account lookups that bypass tenant query filters.
/// Used exclusively by infrastructure middleware for cross-tenant service account resolution.
/// </summary>
public interface IServiceAccountUnfilteredRepository
{
    /// <summary>
    /// Gets a service account by its client ID, bypassing tenant query filters (IgnoreQueryFilters).
    /// This is intended for internal cross-layer use only, such as middleware that must resolve
    /// service accounts before tenant context is established.
    /// </summary>
    Task<ServiceAccountMetadata?> GetByClientIdAsync(string clientId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
