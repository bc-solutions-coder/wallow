using Wallow.ApiKeys.Domain.ApiKeys;
using Wallow.ApiKeys.Domain.Entities;

namespace Wallow.ApiKeys.Application.Interfaces;

public interface IApiKeyRepository
{
    Task AddAsync(ApiKey key, CancellationToken ct);
    Task<ApiKey?> GetByHashAsync(string hash, Guid tenantId, CancellationToken ct);
    Task<List<ApiKey>> ListByServiceAccountAsync(string serviceAccountId, Guid tenantId, CancellationToken ct);
    Task RevokeAsync(ApiKeyId id, Guid tenantId, CancellationToken ct);
    Task<ApiKey?> GetByIdAsync(ApiKeyId id, Guid tenantId, CancellationToken ct);
}
