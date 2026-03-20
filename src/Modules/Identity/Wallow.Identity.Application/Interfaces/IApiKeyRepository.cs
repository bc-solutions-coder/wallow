using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Application.Interfaces;

public interface IApiKeyRepository
{
    Task AddAsync(ApiKey key, CancellationToken ct);
    Task<ApiKey?> GetByHashAsync(string hash, Guid tenantId, CancellationToken ct);
    Task<List<ApiKey>> ListByServiceAccountAsync(string serviceAccountId, Guid tenantId, CancellationToken ct);
    Task RevokeAsync(ApiKeyId id, Guid tenantId, CancellationToken ct);
    Task<ApiKey?> GetByIdAsync(ApiKeyId id, Guid tenantId, CancellationToken ct);
}
