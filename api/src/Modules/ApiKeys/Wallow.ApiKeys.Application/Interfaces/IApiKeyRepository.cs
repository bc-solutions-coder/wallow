using Wallow.ApiKeys.Domain.ApiKeys;
using Wallow.ApiKeys.Domain.Entities;

namespace Wallow.ApiKeys.Application.Interfaces;

public interface IApiKeyRepository
{
    Task AddAsync(ApiKey key, CancellationToken ct);
    Task<ApiKey?> GetByHashAsync(string hash, Guid tenantId, CancellationToken ct);
    Task<ApiKey?> GetByHashAsync(string hash, CancellationToken ct = default);

    /// <summary>
    /// Looks up a domain ID within the current tenant query filter.
    /// The caller must also verify ownership against the returned row.
    /// </summary>
    Task<ApiKey?> GetByIdAsync(ApiKeyId id, CancellationToken ct = default);
    Task<List<ApiKey>> ListByServiceAccountAsync(string serviceAccountId, Guid tenantId, CancellationToken ct);
    Task<List<ApiKey>> ListByTenantAsync(Guid tenantId, CancellationToken ct);
    Task RevokeAsync(ApiKeyId id, Guid tenantId, Guid revokedBy, CancellationToken ct);

    /// <summary>
    /// Sets the repository's tenant before querying another organization's keys.
    /// The publisher's ambient tenant may differ from the organization named by an event.
    /// </summary>
    void UseTenant(Guid tenantId);
}
