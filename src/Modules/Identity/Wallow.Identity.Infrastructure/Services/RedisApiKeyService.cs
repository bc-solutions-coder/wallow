using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Redis-backed API key service for service-to-service authentication.
/// Dual-writes to PostgreSQL (via IApiKeyRepository) and Valkey (Redis) for cache.
/// </summary>
public sealed partial class RedisApiKeyService(
    IConnectionMultiplexer redis,
    IApiKeyRepository apiKeyRepository,
    TimeProvider timeProvider,
    ILogger<RedisApiKeyService> logger) : IApiKeyService
{

    private const string KeyPrefix = "apikey:";
    private const string UserKeysPrefix = "apikeys:user:";

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
            string keyId = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12))
                .Replace("+", "", StringComparison.Ordinal).Replace("/", "", StringComparison.Ordinal).Replace("=", "", StringComparison.Ordinal)[..16];
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
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = expiresAt,
                LastUsedAt = null
            };

            IDatabase db = redis.GetDatabase();
            string json = JsonSerializer.Serialize(metadata);

            TimeSpan? ttl = expiresAt.HasValue ? expiresAt.Value - DateTimeOffset.UtcNow : null;

            // Store by hash for validation lookups
            await db.StringSetAsync(
                $"{KeyPrefix}{keyHash}",
                json,
                ttl,
                keepTtl: false,
                When.Always,
                CommandFlags.None);

            // Add to user's key list for management
            await db.SetAddAsync($"{UserKeysPrefix}{userId}", keyId);

            // Store metadata by keyId (for listing/revocation)
            await db.StringSetAsync(
                $"{KeyPrefix}id:{keyId}",
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
            IDatabase db = redis.GetDatabase();

            // Check Valkey first
            RedisValue json = await db.StringGetAsync($"{KeyPrefix}{keyHash}");
            if (!json.IsNullOrEmpty)
            {
                return ValidateFromCachedData(json.ToString(), keyHash);
            }

            // Cache miss — fall back to PostgreSQL
            // We don't have tenantId in this path, so search across all tenants by hash
            ApiKey? domainKey = await apiKeyRepository.GetByHashAsync(keyHash, Guid.Empty, ct);
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
            if (domainKey.ExpiresAt < DateTimeOffset.UtcNow)
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
            TimeSpan? ttl = domainKey.ExpiresAt.HasValue ? domainKey.ExpiresAt.Value - DateTimeOffset.UtcNow : null;

            await db.StringSetAsync($"{KeyPrefix}{keyHash}", cacheJson, ttl, keepTtl: false, When.Always, CommandFlags.None);

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
            IDatabase db = redis.GetDatabase();
            long count = await db.SetLengthAsync($"{UserKeysPrefix}{userId}");
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
            IDatabase db = redis.GetDatabase();

            // Get the key data from Valkey first
            RedisValue json = await db.StringGetAsync($"{KeyPrefix}id:{keyId}");
            if (json.IsNullOrEmpty)
            {
                return false;
            }

            ApiKeyData? data = JsonSerializer.Deserialize<ApiKeyData>(json.ToString());
            if (data == null || data.UserId != userId)
            {
                return false; // Key doesn't exist or doesn't belong to user
            }

            // Mark revoked in PostgreSQL first (look up by hash since Redis keyId != domain ApiKeyId)
            ApiKey? domainKey = await apiKeyRepository.GetByHashAsync(data.KeyHash, data.TenantId, ct);
            if (domainKey is not null)
            {
                await apiKeyRepository.RevokeAsync(domainKey.Id, data.TenantId, ct);
            }

            // Then delete from Valkey
            await db.KeyDeleteAsync($"{KeyPrefix}{data.KeyHash}");
            await db.KeyDeleteAsync($"{KeyPrefix}id:{keyId}");
            await db.SetRemoveAsync($"{UserKeysPrefix}{userId}", keyId);

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
        if (data.ExpiresAt < DateTimeOffset.UtcNow)
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
            data.LastUsedAt = DateTimeOffset.UtcNow;
            IDatabase db = redis.GetDatabase();
            string json = JsonSerializer.Serialize(data);

            TimeSpan? expiry = data.ExpiresAt.HasValue
                ? data.ExpiresAt.Value - DateTimeOffset.UtcNow
                : null;

            await db.StringSetAsync($"{KeyPrefix}{keyHash}", json, expiry, keepTtl: false, When.Exists, CommandFlags.None);
            await db.StringSetAsync($"{KeyPrefix}id:{data.KeyId}", json, expiry, keepTtl: false, When.Exists, CommandFlags.None);
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
