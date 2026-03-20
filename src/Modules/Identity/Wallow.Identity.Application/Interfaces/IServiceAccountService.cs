using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Service for managing OAuth2 service accounts via Keycloak.
/// Handles creation of Keycloak clients and local metadata tracking.
/// </summary>
public interface IServiceAccountService
{
    /// <summary>
    /// Creates a new service account in Keycloak and stores local metadata.
    /// </summary>
    Task<ServiceAccountCreatedResult> CreateAsync(CreateServiceAccountRequest request, CancellationToken ct = default);

    /// <summary>
    /// Lists all service accounts for the current tenant.
    /// </summary>
    Task<IReadOnlyList<ServiceAccountDto>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a single service account by ID.
    /// </summary>
    Task<ServiceAccountDto?> GetAsync(ServiceAccountMetadataId id, CancellationToken ct = default);

    /// <summary>
    /// Rotates the client secret in Keycloak. Returns the new secret (shown once).
    /// </summary>
    Task<SecretRotatedResult> RotateSecretAsync(ServiceAccountMetadataId id, CancellationToken ct = default);

    /// <summary>
    /// Updates the scopes assigned to a service account.
    /// </summary>
    Task UpdateScopesAsync(ServiceAccountMetadataId id, IEnumerable<string> scopes, CancellationToken ct = default);

    /// <summary>
    /// Revokes the service account, deleting it from Keycloak.
    /// </summary>
    Task RevokeAsync(ServiceAccountMetadataId id, CancellationToken ct = default);
}
