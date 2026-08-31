using Wallow.ApiKeys.Domain.ApiKeys;
using Wallow.ApiKeys.Domain.Entities;

namespace Wallow.ApiKeys.Application.Interfaces;

public interface IApiKeyRepository
{
    Task AddAsync(ApiKey key, CancellationToken ct);
    Task<ApiKey?> GetByHashAsync(string hash, Guid tenantId, CancellationToken ct);
    Task<ApiKey?> GetByHashAsync(string hash, CancellationToken ct = default);

    /// <summary>
    /// Looks a key up by its domain id across all tenants — the revocation path's analogue of
    /// the hash overload above: the caller proves ownership against the returned row, not by
    /// naming the tenant.
    /// </summary>
    Task<ApiKey?> GetByIdAsync(ApiKeyId id, CancellationToken ct = default);
    Task<List<ApiKey>> ListByServiceAccountAsync(string serviceAccountId, Guid tenantId, CancellationToken ct);
    Task<List<ApiKey>> ListByTenantAsync(Guid tenantId, CancellationToken ct);
    Task RevokeAsync(ApiKeyId id, Guid tenantId, Guid revokedBy, CancellationToken ct);

    /// <summary>
    /// The tenant this repository's queries address. An event handler runs under the
    /// PUBLISHER'S ambient tenant — a global admin acting across organizations publishes under
    /// their own — so a handler working another tenant's keys must state that tenant here
    /// before querying, or the tenant query filter silently returns nothing.
    /// </summary>
    void UseTenant(Guid tenantId);
}
