using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Repository for SCIM configuration entities.
/// </summary>
public interface IScimConfigurationRepository
{
    /// <summary>
    /// Gets the SCIM configuration for the current tenant.
    /// </summary>
    Task<ScimConfiguration?> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Adds a new SCIM configuration.
    /// </summary>
    void Add(ScimConfiguration entity);

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>
/// Repository for SCIM sync log entries.
/// </summary>
public interface IScimSyncLogRepository
{
    /// <summary>
    /// Gets recent sync logs for the current tenant.
    /// </summary>
    Task<IReadOnlyList<ScimSyncLog>> GetRecentAsync(int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Adds a new sync log entry.
    /// </summary>
    void Add(ScimSyncLog entity);

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
