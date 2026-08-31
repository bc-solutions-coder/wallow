using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Wallow.ApiKeys.Application.Interfaces;
using Wallow.ApiKeys.Domain.ApiKeys;
using Wallow.ApiKeys.Domain.Entities;
using Wallow.Shared.Contracts.ApiKeys;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.ApiKeys.Infrastructure.Services;

/// <summary>
/// Redis-backed API key service for service-to-service authentication.
/// Dual-writes to PostgreSQL (via IApiKeyRepository) and Valkey (Redis) for cache.
/// </summary>
public sealed partial class RedisApiKeyService(
    IRedisDatabase db,
    IApiKeyRepository apiKeyRepository,
    TimeProvider timeProvider,
    ILogger<RedisApiKeyService> logger) : IApiKeyService
{
    public async Task<ApiKeyCreateResult> CreateApiKeyAsync(
        string name,
        Guid userId,
        Guid tenantId,
        IEnumerable<string>? scopes = null,
        DateTimeOffset? expiresAt = null,
        CancellationToken ct = default)
    {
        try
        {
            // Generate a secure random key: sk_live_<32 random bytes as base64url>
            byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
            string secretPart = Convert.ToBase64String(randomBytes)
                .Replace("+", "-", StringComparison.Ordinal).Replace("/", "_", StringComparison.Ordinal).Replace("=", "", StringComparison.Ordinal);
            string apiKey = $"sk_live_{secretPart}";
            string prefix = apiKey[..16]; // "sk_live_" + first 8 of secret

            // Hash the key for storage (we never store the raw key)
            string keyHash = HashApiKey(apiKey);

            List<string> scopeList = scopes?.ToList() ?? [];

            // Persist to PostgreSQL first
            ApiKey domainKey = ApiKey.Create(
                new TenantId(tenantId),
                userId.ToString(),
                keyHash,
                name,
                scopeList,
                expiresAt,
                userId,
                timeProvider);

            await apiKeyRepository.AddAsync(domainKey, ct);

            // The cache and every caller address the key by its domain id — the one identifier
            // that survives a cache flush, so a listed key can always be revoked.
            string keyId = domainKey.Id.Value.ToString();

            // Then write to Valkey cache
            ApiKeyData metadata = new()
            {
                KeyId = keyId,
                Name = name,
                Prefix = prefix,
                KeyHash = keyHash,
                UserId = userId,
                TenantId = tenantId,
                Scopes = scopeList,
                CreatedAt = timeProvider.GetUtcNow(),
                ExpiresAt = expiresAt,
                LastUsedAt = null
            };

            string json = JsonSerializer.Serialize(metadata);

            TimeSpan? ttl = expiresAt.HasValue ? expiresAt.Value - timeProvider.GetUtcNow() : null;

            // Store by hash for validation lookups
            await db.StringSetAsync(
                ApiKeyCacheKeys.ByHash(keyHash),
                json,
                ttl,
                keepTtl: false,
                When.Always,
                CommandFlags.None);

            // Add to user's key list for management
            await db.SetAddAsync(ApiKeyCacheKeys.UserSet(userId.ToString()), keyId);

            // Store metadata by keyId (for listing/revocation)
            await db.StringSetAsync(
                ApiKeyCacheKeys.ById(keyId),
                json,
                ttl,
                keepTtl: false,
                When.Always,
                CommandFlags.None);

            LogApiKeyCreated(keyId, userId, tenantId);

            return new ApiKeyCreateResult(
                Success: true,
                KeyId: keyId,
                ApiKey: apiKey,
                Prefix: prefix,
                Error: null);
        }
        catch (Exception ex)
        {
            LogCreateApiKeyFailed(ex, userId);
            return new ApiKeyCreateResult(
                Success: false,
                KeyId: null,
                ApiKey: null,
                Prefix: null,
                Error: "Failed to create API key");
        }
    }

    public async Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || !apiKey.StartsWith("sk_live_", StringComparison.Ordinal))
        {
            return new ApiKeyValidationResult(
                IsValid: false,
                KeyId: null,
                UserId: null,
                TenantId: null,
                Scopes: null,
                Error: "Invalid API key format");
        }

        try
        {
            string keyHash = HashApiKey(apiKey);
            // Check Valkey first
            RedisValue json = await db.StringGetAsync(ApiKeyCacheKeys.ByHash(keyHash));
            if (!json.IsNullOrEmpty)
            {
                return ValidateFromCachedData(json.ToString(), keyHash);
            }

            // Cache miss -- fall back to PostgreSQL
            // We don't have tenantId in this path, so search across all tenants by hash
            ApiKey? domainKey = await apiKeyRepository.GetByHashAsync(keyHash, ct);
            if (domainKey is null || domainKey.IsRevoked)
            {
                return new ApiKeyValidationResult(
                    IsValid: false,
                    KeyId: null,
                    UserId: null,
                    TenantId: null,
                    Scopes: null,
                    Error: "API key not found");
            }

            // Check expiration
            if (domainKey.ExpiresAt < timeProvider.GetUtcNow())
            {
                return new ApiKeyValidationResult(
                    IsValid: false,
                    KeyId: domainKey.Id.Value.ToString(),
                    UserId: null,
                    TenantId: null,
                    Scopes: null,
                    Error: "API key expired");
            }

            // Repopulate Valkey cache
            ApiKeyData cacheData = new()
            {
                KeyId = domainKey.Id.Value.ToString(),
                Name = domainKey.DisplayName,
                Prefix = "",
                KeyHash = keyHash,
                UserId = Guid.TryParse(domainKey.ServiceAccountId, out Guid parsedUserId) ? parsedUserId : Guid.Empty,
                TenantId = domainKey.TenantId.Value,
                Scopes = domainKey.Scopes.ToList(),
                CreatedAt = domainKey.CreatedAt,
                ExpiresAt = domainKey.ExpiresAt,
                LastUsedAt = null
            };

            string cacheJson = JsonSerializer.Serialize(cacheData);
            TimeSpan? ttl = domainKey.ExpiresAt.HasValue ? domainKey.ExpiresAt.Value - timeProvider.GetUtcNow() : null;

            await db.StringSetAsync(ApiKeyCacheKeys.ByHash(keyHash), cacheJson, ttl, keepTtl: false, When.Always, CommandFlags.None);

            return new ApiKeyValidationResult(
                IsValid: true,
                KeyId: cacheData.KeyId,
                UserId: cacheData.UserId,
                TenantId: cacheData.TenantId,
                Scopes: cacheData.Scopes,
                Error: null);
        }
        catch (Exception ex)
        {
            LogValidateApiKeyFailed(ex);
            return new ApiKeyValidationResult(
                IsValid: false,
                KeyId: null,
                UserId: null,
                TenantId: null,
                Scopes: null,
                Error: "Validation error");
        }
    }

    public async Task<IReadOnlyList<ApiKeyMetadata>> ListApiKeysAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            // Read from PostgreSQL only
            List<ApiKey> keys = await apiKeyRepository.ListByServiceAccountAsync(userId.ToString(), tenantId, ct);

            return keys
                .Where(k => !k.IsRevoked)
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new ApiKeyMetadata(
                    KeyId: k.Id.Value.ToString(),
                    Name: k.DisplayName,
                    Prefix: "",
                    UserId: Guid.TryParse(k.ServiceAccountId, out Guid parsedUserId) ? parsedUserId : Guid.Empty,
                    TenantId: k.TenantId.Value,
                    Scopes: k.Scopes,
                    CreatedAt: k.CreatedAt,
                    ExpiresAt: k.ExpiresAt,
                    LastUsedAt: k.UpdatedAt))
                .ToList();
        }
        catch (Exception ex)
        {
            LogListApiKeysFailed(ex, userId);
            return [];
        }
    }

    public async Task<int> GetApiKeyCountAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            long count = await db.SetLengthAsync(ApiKeyCacheKeys.UserSet(userId.ToString()));
            return (int)count;
        }
        catch (Exception ex)
        {
            LogListApiKeysFailed(ex, userId);
            return 0;
        }
    }

    public async Task<bool> RevokeApiKeyAsync(string keyId, Guid userId, CancellationToken ct = default)
    {
        try
        {
            if (!Guid.TryParse(keyId, out Guid parsedKeyId))
            {
                return false;
            }

            // PostgreSQL is the source of truth: the row must die even when the cache entries
            // have already expired, so the lookup goes to the repository and ownership is
            // proved against the row, not against cached JSON.
            ApiKey? domainKey = await apiKeyRepository.GetByIdAsync(new ApiKeyId(parsedKeyId), ct);
            if (domainKey is null || domainKey.ServiceAccountId != userId.ToString())
            {
                return false;
            }

            if (!domainKey.IsRevoked)
            {
                await apiKeyRepository.RevokeAsync(domainKey.Id, domainKey.TenantId.Value, userId, ct);
            }

            // Then drop the cache entries, every name derived from the row.
            string normalizedKeyId = parsedKeyId.ToString();
            await db.KeyDeleteAsync(ApiKeyCacheKeys.ByHash(domainKey.HashedKey));
            await db.KeyDeleteAsync(ApiKeyCacheKeys.ById(normalizedKeyId));
            await db.SetRemoveAsync(ApiKeyCacheKeys.UserSet(userId.ToString()), normalizedKeyId);

            LogApiKeyRevoked(keyId, userId);
            return true;
        }
        catch (Exception ex)
        {
            LogRevokeApiKeyFailed(ex, keyId);
            return false;
        }
    }

    private ApiKeyValidationResult ValidateFromCachedData(string jsonString, string keyHash)
    {
        ApiKeyData? data = JsonSerializer.Deserialize<ApiKeyData>(jsonString);
        if (data == null)
        {
            return new ApiKeyValidationResult(
                IsValid: false,
                KeyId: null,
                UserId: null,
                TenantId: null,
                Scopes: null,
                Error: "Invalid API key data");
        }

        // Check expiration
        if (data.ExpiresAt < timeProvider.GetUtcNow())
        {
            return new ApiKeyValidationResult(
                IsValid: false,
                KeyId: data.KeyId,
                UserId: null,
                TenantId: null,
                Scopes: null,
                Error: "API key expired");
        }

        // Update last used timestamp (fire and forget)
        _ = UpdateLastUsedAsync(keyHash, data);

        return new ApiKeyValidationResult(
            IsValid: true,
            KeyId: data.KeyId,
            UserId: data.UserId,
            TenantId: data.TenantId,
            Scopes: data.Scopes,
            Error: null);
    }

    private async Task UpdateLastUsedAsync(string keyHash, ApiKeyData data)
    {
        try
        {
            data.LastUsedAt = timeProvider.GetUtcNow();
            string json = JsonSerializer.Serialize(data);

            TimeSpan? expiry = data.ExpiresAt.HasValue
                ? data.ExpiresAt.Value - timeProvider.GetUtcNow()
                : null;

            await db.StringSetAsync(ApiKeyCacheKeys.ByHash(keyHash), json, expiry, keepTtl: false, When.Exists, CommandFlags.None);
            await db.StringSetAsync(ApiKeyCacheKeys.ById(data.KeyId), json, expiry, keepTtl: false, When.Exists, CommandFlags.None);
        }
        catch (Exception ex)
        {
            LogUpdateLastUsedFailed(ex, data.KeyId);
        }
    }

    private static string HashApiKey(string apiKey)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexStringLower(bytes);
    }

    private sealed class ApiKeyData
    {
        public string KeyId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Prefix { get; set; } = "";
        public string KeyHash { get; set; } = "";
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public List<string> Scopes { get; set; } = [];
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public DateTimeOffset? LastUsedAt { get; set; }
    }
}

public sealed partial class RedisApiKeyService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Created API key {KeyId} for user {UserId} in tenant {TenantId}")]
    private partial void LogApiKeyCreated(string keyId, Guid userId, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create API key for user {UserId}")]
    private partial void LogCreateApiKeyFailed(Exception ex, Guid userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to validate API key")]
    private partial void LogValidateApiKeyFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to list API keys for user {UserId}")]
    private partial void LogListApiKeysFailed(Exception ex, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Revoked API key {KeyId} for user {UserId}")]
    private partial void LogApiKeyRevoked(string keyId, Guid userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to revoke API key {KeyId}")]
    private partial void LogRevokeApiKeyFailed(Exception ex, string keyId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to update last used timestamp for key {KeyId}")]
    private partial void LogUpdateLastUsedFailed(Exception ex, string keyId);
}
