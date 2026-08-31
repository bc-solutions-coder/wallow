using Microsoft.Extensions.Logging;
using Wallow.ApiKeys.Application.Interfaces;
using Wallow.ApiKeys.Domain.Entities;
using Wallow.ApiKeys.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;

namespace Wallow.ApiKeys.Infrastructure.Handlers;

/// <summary>
/// When an organization is deleted, every API key in its tenant dies with it: the rows are
/// marked revoked in PostgreSQL and the validation cache entries are dropped from Valkey, so a
/// key in flight stops validating the moment the entry goes rather than when its TTL runs out.
/// Every cache name is derived from the PostgreSQL row — the key hash, the domain id, the
/// owning service account — so nothing depends on what the cache happens to still hold.
/// Idempotent — a redelivered event finds the keys revoked and re-deletes cache entries that
/// are already gone.
/// </summary>
public sealed partial class OrganizationDeletedHandler(
    IApiKeyRepository apiKeys,
    IRedisDatabase redis,
    ILogger<OrganizationDeletedHandler> logger)
{
    public async Task HandleAsync(OrganizationDeletedEvent message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The envelope restores the PUBLISHER'S tenant — a global admin deleting across
        // organizations publishes under their own — so the tenant whose keys die is stated
        // explicitly.
        apiKeys.UseTenant(message.OrganizationId);
        List<ApiKey> keys = await apiKeys.ListByTenantAsync(message.OrganizationId, ct);

        int revoked = 0;
        foreach (ApiKey key in keys)
        {
            if (!key.IsRevoked)
            {
                await apiKeys.RevokeAsync(key.Id, message.OrganizationId, message.ActorId, ct);
                revoked++;
            }

            string keyId = key.Id.Value.ToString();
            await redis.KeyDeleteAsync(ApiKeyCacheKeys.ByHash(key.HashedKey));
            await redis.KeyDeleteAsync(ApiKeyCacheKeys.ById(keyId));
            await redis.SetRemoveAsync(ApiKeyCacheKeys.UserSet(key.ServiceAccountId), keyId);
        }

        LogTenantKeysRevoked(revoked, keys.Count, message.OrganizationId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Revoked {RevokedCount} of {TotalCount} API keys for deleted organization {OrganizationId}")]
    private partial void LogTenantKeysRevoked(int revokedCount, int totalCount, Guid organizationId);
}
