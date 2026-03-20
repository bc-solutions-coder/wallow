using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Repository for SSO configuration entities.
/// </summary>
public interface ISsoConfigurationRepository
{
    /// <summary>
    /// Gets the SSO configuration for the current tenant.
    /// </summary>
    Task<SsoConfiguration?> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Adds a new SSO configuration.
    /// </summary>
    void Add(SsoConfiguration entity);

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
