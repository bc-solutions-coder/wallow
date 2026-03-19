using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Identity;

namespace Foundry.Identity.Application.Interfaces;

public interface IApiKeyRepository
{
    Task AddAsync(ApiKey key, CancellationToken ct);
    Task<ApiKey?> GetByHashAsync(string hash, Guid tenantId, CancellationToken ct);
    Task<List<ApiKey>> ListByServiceAccountAsync(string serviceAccountId, Guid tenantId, CancellationToken ct);
    Task RevokeAsync(ApiKeyId id, Guid tenantId, CancellationToken ct);
    Task<ApiKey?> GetByIdAsync(ApiKeyId id, Guid tenantId, CancellationToken ct);
}
