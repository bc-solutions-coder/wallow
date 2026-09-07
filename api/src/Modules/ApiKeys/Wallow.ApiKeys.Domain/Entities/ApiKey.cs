using Wallow.ApiKeys.Domain.ApiKeys;
using Wallow.ApiKeys.Domain.Errors;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.ApiKeys.Domain.Entities;

/// <summary>
/// Stores an API key hash and its tenant-scoped owner; never the plaintext secret.
/// </summary>
public sealed class ApiKey : AuditableEntity<ApiKeyId>, ITenantScoped
{
    public TenantId TenantId { get; init; }

    /// <summary>
    /// Owner identifier; the API-key service stores the user ID as a string.
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
    /// Granted OAuth scopes. Creation endpoints validate them against caller permissions
    /// and, for service-account callers, the client's permitted scopes.
    /// </summary>
    public IReadOnlyList<string> Scopes => _scopes.AsReadOnly();

    /// <summary>
    /// Optional expiration date. Null means the key does not expire.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

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
            throw new BusinessRuleException(ApiKeysErrors.ServiceAccountIdRequired);
        }

        if (string.IsNullOrWhiteSpace(hashedKey))
        {
            throw new BusinessRuleException(ApiKeysErrors.HashedKeyRequired);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new BusinessRuleException(ApiKeysErrors.ApiKeyDisplayNameRequired);
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
            throw new BusinessRuleException(ApiKeysErrors.ApiKeyAlreadyRevoked);
        }

        IsRevoked = true;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }
}
