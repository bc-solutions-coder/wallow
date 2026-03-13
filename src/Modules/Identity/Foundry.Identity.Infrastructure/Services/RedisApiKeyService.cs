using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Foundry.Identity.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Foundry.Identity.Infrastructure.Services;

/// <summary>
/// Redis-backed API key service for service-to-service authentication.
/// </summary>
public sealed partial class RedisApiKeyService(IConnectionMultiplexer redis, ILogger<RedisApiKeyService> logger) : IApiKeyService
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

            ApiKeyData metadata = new()
            {
                KeyId = keyId,
                Name = name,
                Prefix = prefix,
                KeyHash = keyHash,
                UserId = userId,
                TenantId = tenantId,
                Scopes = scopes?.ToList() ?? [],
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

            RedisValue json = await db.StringGetAsync($"{KeyPrefix}{keyHash}");
            if (json.IsNullOrEmpty)
            {
                return new ApiKeyValidationResult(
                    IsValid: false,
                    KeyId: null,
                    UserId: null,
                    TenantId: null,
                    Scopes: null,
                    Error: "API key not found");
            }

            ApiKeyData? data = JsonSerializer.Deserialize<ApiKeyData>(json.ToString());
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

    public async Task<IReadOnlyList<ApiKeyMetadata>> ListApiKeysAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            IDatabase db = redis.GetDatabase();
            RedisValue[] keyIds = await db.SetMembersAsync($"{UserKeysPrefix}{userId}");

            List<ApiKeyMetadata> results = [];
            foreach (RedisValue keyId in keyIds)
            {
                RedisValue json = await db.StringGetAsync($"{KeyPrefix}id:{keyId}");
                if (json.IsNullOrEmpty)
                {
                    continue;
                }

                ApiKeyData? data = JsonSerializer.Deserialize<ApiKeyData>(json.ToString());
                if (data == null)
                {
                    continue;
                }

                results.Add(new ApiKeyMetadata(
                    KeyId: data.KeyId,
                    Name: data.Name,
                    Prefix: data.Prefix,
                    UserId: data.UserId,
                    TenantId: data.TenantId,
                    Scopes: data.Scopes,
                    CreatedAt: data.CreatedAt,
                    ExpiresAt: data.ExpiresAt,
                    LastUsedAt: data.LastUsedAt));
            }

            return results.OrderByDescending(k => k.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            LogListApiKeysFailed(ex, userId);
            return [];
        }
    }

    public async Task<bool> RevokeApiKeyAsync(string keyId, Guid userId, CancellationToken ct = default)
    {
        try
        {
            IDatabase db = redis.GetDatabase();

            // Get the key data first
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

            // Delete by hash (validation lookup)
            await db.KeyDeleteAsync($"{KeyPrefix}{data.KeyHash}");

            // Delete by ID (management lookup)
            await db.KeyDeleteAsync($"{KeyPrefix}id:{keyId}");

            // Remove from user's key list
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

            await db.StringSetAsync($"{KeyPrefix}{keyHash}", json, expiry, keepTtl: false, When.Always, CommandFlags.None);
            await db.StringSetAsync($"{KeyPrefix}id:{data.KeyId}", json, expiry, keepTtl: false, When.Always, CommandFlags.None);
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
