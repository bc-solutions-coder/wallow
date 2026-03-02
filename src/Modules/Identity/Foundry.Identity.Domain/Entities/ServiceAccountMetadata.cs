using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Domain.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Identity.Domain.Entities;

/// <summary>
/// Local reference to a Keycloak service account client.
/// Stores metadata for tenant-specific queries, metering attribution, and last-used tracking.
/// The actual authentication is handled by Keycloak.
/// </summary>
public sealed class ServiceAccountMetadata : AuditableEntity<ServiceAccountMetadataId>, ITenantScoped
{
    public TenantId TenantId { get; init; }

    /// <summary>
    /// Keycloak client ID (e.g., "sa-tenant123-production").
    /// </summary>
    public string KeycloakClientId { get; private set; } = string.Empty;

    /// <summary>
    /// Human-readable name for the service account.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Optional description of what this service account is used for.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Current status (Active or Revoked).
    /// </summary>
    public ServiceAccountStatus Status { get; private set; }

    /// <summary>
    /// Timestamp of the last API call made using this service account.
    /// </summary>
    public DateTime? LastUsedAt { get; private set; }

    private readonly List<string> _scopes = [];

    /// <summary>
    /// OAuth2 scopes granted to this service account.
    /// </summary>
    public IReadOnlyList<string> Scopes => _scopes.AsReadOnly();

    private ServiceAccountMetadata() { } // EF Core

    private ServiceAccountMetadata(
        TenantId tenantId,
        string keycloakClientId,
        string name,
        string? description,
        IEnumerable<string> scopes,
        Guid createdByUserId)
    {
        Id = ServiceAccountMetadataId.New();
        TenantId = tenantId;
        KeycloakClientId = keycloakClientId;
        Name = name;
        Description = description;
        Status = ServiceAccountStatus.Active;
        _scopes.AddRange(scopes);
        SetCreated(DateTimeOffset.UtcNow, createdByUserId);
    }

    public static ServiceAccountMetadata Create(
        TenantId tenantId,
        string keycloakClientId,
        string name,
        string? description,
        IEnumerable<string> scopes,
        Guid createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(keycloakClientId))
        {
            throw new BusinessRuleException(
                "Identity.KeycloakClientIdRequired",
                "Keycloak client ID cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException(
                "Identity.ServiceAccountNameRequired",
                "Service account name cannot be empty");
        }

        return new ServiceAccountMetadata(
            tenantId,
            keycloakClientId,
            name,
            description,
            scopes,
            createdByUserId);
    }

    /// <summary>
    /// Updates the last used timestamp. Called by middleware when API is accessed.
    /// </summary>
    public void MarkUsed()
    {
        LastUsedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Revokes the service account. The Keycloak client should be deleted separately.
    /// </summary>
    public void Revoke(Guid updatedByUserId)
    {
        if (Status == ServiceAccountStatus.Revoked)
        {
            throw new BusinessRuleException(
                "Identity.ServiceAccountAlreadyRevoked",
                "Service account is already revoked");
        }

        Status = ServiceAccountStatus.Revoked;
        SetUpdated(DateTimeOffset.UtcNow, updatedByUserId);
    }

    /// <summary>
    /// Updates the scopes assigned to this service account.
    /// </summary>
    public void UpdateScopes(IEnumerable<string> scopes, Guid updatedByUserId)
    {
        if (Status == ServiceAccountStatus.Revoked)
        {
            throw new BusinessRuleException(
                "Identity.CannotUpdateRevokedServiceAccount",
                "Cannot update a revoked service account");
        }

        _scopes.Clear();
        _scopes.AddRange(scopes);
        SetUpdated(DateTimeOffset.UtcNow, updatedByUserId);
    }

    /// <summary>
    /// Updates the service account name and description.
    /// </summary>
    public void UpdateDetails(string name, string? description, Guid updatedByUserId)
    {
        if (Status == ServiceAccountStatus.Revoked)
        {
            throw new BusinessRuleException(
                "Identity.CannotUpdateRevokedServiceAccount",
                "Cannot update a revoked service account");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException(
                "Identity.ServiceAccountNameRequired",
                "Service account name cannot be empty");
        }

        Name = name;
        Description = description;
        SetUpdated(DateTimeOffset.UtcNow, updatedByUserId);
    }
}
