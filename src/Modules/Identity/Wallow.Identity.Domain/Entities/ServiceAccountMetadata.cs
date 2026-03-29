using JetBrains.Annotations;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Domain.Entities;

/// <summary>
/// Metadata for an OAuth2 service account client.
/// Stores metadata for tenant-specific queries, metering attribution, and last-used tracking.
/// </summary>
public sealed class ServiceAccountMetadata : AuditableEntity<ServiceAccountMetadataId>, ITenantScoped
{
    public TenantId TenantId { get; init; }

    /// <summary>
    /// OAuth2 client ID (e.g., "sa-tenant123-production").
    /// </summary>
    public string ClientId { get; private set; } = string.Empty;

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

    // ReSharper disable once UnusedMember.Local
    private ServiceAccountMetadata() { } // EF Core

    private ServiceAccountMetadata(
        TenantId tenantId,
        string clientId,
        string name,
        string? description,
        IEnumerable<string> scopes,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        Id = ServiceAccountMetadataId.New();
        TenantId = tenantId;
        ClientId = clientId;
        Name = name;
        Description = description;
        Status = ServiceAccountStatus.Active;
        _scopes.AddRange(scopes);
        SetCreated(timeProvider.GetUtcNow(), createdByUserId);
    }

    public static ServiceAccountMetadata Create(
        TenantId tenantId,
        string clientId,
        string name,
        string? description,
        IEnumerable<string> scopes,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new BusinessRuleException(
                "Identity.ClientIdRequired",
                "Client ID cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException(
                "Identity.ServiceAccountNameRequired",
                "Service account name cannot be empty");
        }

        return new ServiceAccountMetadata(
            tenantId,
            clientId,
            name,
            description,
            scopes,
            createdByUserId,
            timeProvider);
    }

    /// <summary>
    /// Updates the last used timestamp. Called by middleware when API is accessed.
    /// </summary>
    public void MarkUsed(TimeProvider timeProvider)
    {
        LastUsedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Revokes the service account.
    /// </summary>
    public void Revoke(Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status == ServiceAccountStatus.Revoked)
        {
            throw new BusinessRuleException(
                "Identity.ServiceAccountAlreadyRevoked",
                "Service account is already revoked");
        }

        Status = ServiceAccountStatus.Revoked;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    /// <summary>
    /// Updates the name and description of this service account.
    /// </summary>
    [UsedImplicitly]
    public void UpdateDetails(string name, string? description, Guid updatedByUserId, TimeProvider timeProvider)
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
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    /// <summary>
    /// Updates the scopes assigned to this service account.
    /// </summary>
    public void UpdateScopes(IEnumerable<string> scopes, Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status == ServiceAccountStatus.Revoked)
        {
            throw new BusinessRuleException(
                "Identity.CannotUpdateRevokedServiceAccount",
                "Cannot update a revoked service account");
        }

        _scopes.Clear();
        _scopes.AddRange(scopes);
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

}
