using Foundry.Identity.Application.DTOs;

namespace Foundry.Identity.Application.Interfaces;

/// <summary>
/// Service for managing SCIM (System for Cross-domain Identity Management) operations.
/// Implements SCIM 2.0 protocol for user provisioning and synchronization.
/// </summary>
public interface IScimService
{
    // Configuration
    /// <summary>
    /// Gets the SCIM configuration for the current tenant.
    /// </summary>
    Task<ScimConfigurationDto?> GetConfigurationAsync(CancellationToken ct = default);

    /// <summary>
    /// Enables SCIM provisioning for the current tenant.
    /// </summary>
    Task<EnableScimResponse> EnableScimAsync(EnableScimRequest request, CancellationToken ct = default);

    /// <summary>
    /// Disables SCIM provisioning for the current tenant.
    /// </summary>
    Task DisableScimAsync(CancellationToken ct = default);

    /// <summary>
    /// Regenerates the SCIM bearer token. Returns the new plain-text token (only shown once).
    /// </summary>
    Task<string> RegenerateTokenAsync(CancellationToken ct = default);

    // SCIM User Operations (called by SCIM API endpoints)
    /// <summary>
    /// Creates a new user via SCIM provisioning.
    /// </summary>
    Task<ScimUser> CreateUserAsync(ScimUserRequest request, CancellationToken ct = default);

    /// <summary>
    /// Fully replaces a user's attributes (PUT operation).
    /// </summary>
    Task<ScimUser> UpdateUserAsync(string id, ScimUserRequest request, CancellationToken ct = default);

    /// <summary>
    /// Partially updates a user's attributes (PATCH operation).
    /// </summary>
    Task<ScimUser> PatchUserAsync(string id, ScimPatchRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes or deactivates a user based on configuration.
    /// </summary>
    Task DeleteUserAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Gets a user by their Keycloak ID.
    /// </summary>
    Task<ScimUser?> GetUserAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Lists users with optional filtering and pagination.
    /// </summary>
    Task<ScimListResponse<ScimUser>> ListUsersAsync(ScimListRequest request, CancellationToken ct = default);

    // SCIM Group Operations (optional)
    /// <summary>
    /// Gets a group by ID.
    /// </summary>
    Task<ScimGroup?> GetGroupAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new group via SCIM provisioning.
    /// </summary>
    Task<ScimGroup> CreateGroupAsync(ScimGroupRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates a group's attributes.
    /// </summary>
    Task<ScimGroup> UpdateGroupAsync(string id, ScimGroupRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a group.
    /// </summary>
    Task DeleteGroupAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Lists groups with optional filtering and pagination.
    /// </summary>
    Task<ScimListResponse<ScimGroup>> ListGroupsAsync(ScimListRequest request, CancellationToken ct = default);

    // Sync log
    /// <summary>
    /// Gets recent SCIM sync logs for auditing.
    /// </summary>
    Task<IReadOnlyList<ScimSyncLogDto>> GetSyncLogsAsync(int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Validates a SCIM bearer token for authentication.
    /// </summary>
    Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default);
}
