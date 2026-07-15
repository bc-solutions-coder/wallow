using Wallow.ApiKeys.Domain.ApiKeys;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.ApiKeys.Domain.Entities;

/// <summary>
/// Represents a hashed API key bound to a service account within a tenant.
/// The plaintext key is never stored — only a hash for verification.
/// </summary>
public sealed class ApiKey : AuditableEntity<ApiKeyId>, ITenantScoped
{
    public TenantId TenantId { get; init; }

    /// <summary>
    /// The service account client ID this key authenticates as.
    /// </summary>
    public string ServiceAccountId { get; private set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the plaintext API key.
    /// </summary>
    public string HashedKey { get; private set; } = string.Empty;

    /// <summary>
    /// Human-readable label for identifying this key (e.g., "Production Key").
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    private readonly List<string> _scopes = [];

    /// <summary>
    /// OAuth2 scopes granted to this API key, must be a subset of the service account's scopes.
    /// </summary>
    public IReadOnlyList<string> Scopes => _scopes.AsReadOnly();

    /// <summary>
    /// Optional expiration date. Null means the key does not expire.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// Whether this key has been revoked.
    /// </summary>
    public bool IsRevoked { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private ApiKey() { } // EF Core

    private ApiKey(
        TenantId tenantId,
        string serviceAccountId,
        string hashedKey,
        string displayName,
        IEnumerable<string> scopes,
        DateTimeOffset? expiresAt,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        Id = ApiKeyId.New();
        TenantId = tenantId;
        ServiceAccountId = serviceAccountId;
        HashedKey = hashedKey;
        DisplayName = displayName;
        _scopes.AddRange(scopes);
        ExpiresAt = expiresAt;
        IsRevoked = false;
        SetCreated(timeProvider.GetUtcNow(), createdByUserId);
    }

    public static ApiKey Create(
        TenantId tenantId,
        string serviceAccountId,
        string hashedKey,
        string displayName,
        IEnumerable<string> scopes,
        DateTimeOffset? expiresAt,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(serviceAccountId))
        {
            throw new BusinessRuleException(
                "ApiKeys.ServiceAccountIdRequired",
                "Service account ID cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(hashedKey))
        {
            throw new BusinessRuleException(
                "ApiKeys.HashedKeyRequired",
                "Hashed key cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new BusinessRuleException(
                "ApiKeys.ApiKeyDisplayNameRequired",
                "API key display name cannot be empty");
        }

        return new ApiKey(
            tenantId,
            serviceAccountId,
            hashedKey,
            displayName,
            scopes,
            expiresAt,
            createdByUserId,
            timeProvider);
    }

    public void Revoke(Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (IsRevoked)
        {
            throw new BusinessRuleException(
                "ApiKeys.ApiKeyAlreadyRevoked",
                "API key is already revoked");
        }

        IsRevoked = true;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }
}
