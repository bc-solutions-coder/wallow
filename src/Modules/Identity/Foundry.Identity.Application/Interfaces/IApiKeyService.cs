using JetBrains.Annotations;

namespace Foundry.Identity.Application.Interfaces;

/// <summary>
/// Service for managing API keys for service-to-service authentication.
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Creates a new API key for a user/tenant.
    /// </summary>
    /// <param name="name">Friendly name for the key</param>
    /// <param name="userId">The user ID this key belongs to</param>
    /// <param name="tenantId">The tenant ID this key is scoped to</param>
    /// <param name="scopes">Optional permission scopes (null = all permissions)</param>
    /// <param name="expiresAt">Optional expiration date</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created API key (only returned once, store securely!)</returns>
    Task<ApiKeyCreateResult> CreateApiKeyAsync(
        string name,
        Guid userId,
        Guid tenantId,
        IEnumerable<string>? scopes = null,
        DateTimeOffset? expiresAt = null,
        CancellationToken ct = default);

    /// <summary>
    /// Validates an API key and returns its metadata if valid.
    /// </summary>
    Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default);

    /// <summary>
    /// Lists all API keys for a user (returns metadata only, not the actual keys).
    /// </summary>
    Task<IReadOnlyList<ApiKeyMetadata>> ListApiKeysAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Gets the number of API keys a user currently has.
    /// </summary>
    Task<int> GetApiKeyCountAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Revokes an API key by its ID.
    /// </summary>
    Task<bool> RevokeApiKeyAsync(string keyId, Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Result of creating an API key.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ApiKeyCreateResult(
    bool Success,
    string? KeyId,
    string? ApiKey,  // The full key - only returned on creation
    string? Prefix,  // First 8 chars for identification
    string? Error);

/// <summary>
/// Result of validating an API key.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ApiKeyValidationResult(
    bool IsValid,
    string? KeyId,
    Guid? UserId,
    Guid? TenantId,
    IReadOnlyList<string>? Scopes,
    string? Error);

/// <summary>
/// API key metadata (does not include the actual key).
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ApiKeyMetadata(
    string KeyId,
    string Name,
    string Prefix,
    Guid UserId,
    Guid TenantId,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt);
