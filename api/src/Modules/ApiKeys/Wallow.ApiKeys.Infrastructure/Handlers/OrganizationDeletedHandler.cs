using Microsoft.Extensions.Logging;
using Wallow.ApiKeys.Application.Interfaces;
using Wallow.ApiKeys.Domain.Entities;
using Wallow.ApiKeys.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;

namespace Wallow.ApiKeys.Infrastructure.Handlers;

/// <summary>
/// Revokes the deleted organization's keys and removes their Valkey cache entries.
/// Cache names come from database rows, so cleanup works after cache expiry.
/// Redelivery skips already-revoked rows and repeats cache deletion safely.
/// </summary>
public sealed partial class OrganizationDeletedHandler(
    IApiKeyRepository apiKeys,
    IRedisDatabase redis,
    ILogger<OrganizationDeletedHandler> logger)
{
    public async Task HandleAsync(OrganizationDeletedEvent message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The event's organization can differ from the publisher's ambient tenant.
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
