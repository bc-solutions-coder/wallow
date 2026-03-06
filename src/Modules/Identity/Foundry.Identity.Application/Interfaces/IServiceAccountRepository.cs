using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Identity;

namespace Foundry.Identity.Application.Interfaces;

/// <summary>
/// Repository for managing service account metadata.
/// </summary>
public interface IServiceAccountRepository
{
    /// <summary>
    /// Gets a service account by ID for the current tenant.
    /// </summary>
    Task<ServiceAccountMetadata?> GetByIdAsync(ServiceAccountMetadataId id, CancellationToken ct = default);

    /// <summary>
    /// Gets all service accounts for the current tenant.
    /// </summary>
    Task<IReadOnlyList<ServiceAccountMetadata>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Adds a new service account.
    /// </summary>
    void Add(ServiceAccountMetadata entity);

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
