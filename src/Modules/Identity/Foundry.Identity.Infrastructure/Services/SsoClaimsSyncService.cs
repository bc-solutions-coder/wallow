using System.Net.Http.Json;
using Foundry.Identity.Infrastructure.Extensions;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Foundry.Identity.Infrastructure.Services;

public sealed partial class SsoClaimsSyncService
{
    private readonly HttpClient _httpClient;
    private readonly ISsoConfigurationRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SsoClaimsSyncService> _logger;
    private readonly string _realm;

    public SsoClaimsSyncService(
        IHttpClientFactory httpClientFactory,
        ISsoConfigurationRepository repository,
        ITenantContext tenantContext,
        IOptions<KeycloakOptions> keycloakOptions,
        ILogger<SsoClaimsSyncService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("KeycloakAdminClient");
        _repository = repository;
        _tenantContext = tenantContext;
        _realm = keycloakOptions.Value.Realm;
        _logger = logger;
    }

    public async Task SyncUserClaimsAsync(Guid userId, CancellationToken ct = default)
    {
        Domain.Entities.SsoConfiguration? config = await _repository.GetAsync(ct);
        if (config == null)
        {
            LogSkippingClaimsSyncNoConfig(_tenantContext.TenantId.Value);
            return;
        }

        if (!config.SyncGroupsAsRoles)
        {
            LogSkippingClaimsSyncDisabled(_tenantContext.TenantId.Value);
            return;
        }

        if (string.IsNullOrWhiteSpace(config.GroupsAttribute))
        {
            LogSkippingClaimsSyncNoAttribute(_tenantContext.TenantId.Value);
            return;
        }

        LogSyncingUserClaims(userId, _tenantContext.TenantId.Value);

        try
        {
            Dictionary<string, IEnumerable<string>>? userAttributes = await GetUserAttributesAsync(userId, ct);
            if (userAttributes == null)
            {
                LogUserNotFoundForClaimsSync(userId);
                return;
            }

            List<string> groups = ExtractGroupsFromAttributes(userAttributes, config.GroupsAttribute);
            if (groups.Count == 0)
            {
                LogNoGroupsFoundForSync(config.GroupsAttribute, userId);
                return;
            }

            string groupList = string.Join(", ", groups);
            LogFoundGroupsForSync(groups.Count, userId, groupList);

            IReadOnlyList<string> currentRoles = await GetUserRolesFromKeycloakAsync(userId, ct);

            await SyncGroupsToRolesAsync(userId, groups, currentRoles, config.DefaultRole, ct);

            LogUserClaimsSynced(userId, _tenantContext.TenantId.Value);
        }
        catch (Exception ex)
        {
            LogSyncUserClaimsFailed(ex, userId);
            throw;
        }
    }

    private async Task<Dictionary<string, IEnumerable<string>>?> GetUserAttributesAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/admin/realms/{_realm}/users/{userId}",
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            KeycloakUserRepresentation? user = await response.Content.ReadFromJsonAsync<KeycloakUserRepresentation>(ct);
            return user?.Attributes;
        }
        catch (Exception ex)
        {
            LogGetUserAttributesFailed(ex, userId);
            return null;
        }
    }

    private static List<string> ExtractGroupsFromAttributes(
        Dictionary<string, IEnumerable<string>> attributes,
        string groupsAttribute)
    {
        if (!attributes.TryGetValue(groupsAttribute, out IEnumerable<string>? groupValues))
        {
            return [];
        }

        List<string> groups = [];
        foreach (string value in groupValues)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (value.StartsWith('['))
            {
                try
                {
                    List<string>? parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(value);
                    if (parsed != null)
                    {
                        groups.AddRange(parsed.Where(g => !string.IsNullOrWhiteSpace(g)));
                        continue;
                    }
                }
                catch
                {
                    // Not valid JSON, treat as literal value
                }
            }

            groups.Add(value);
        }

        return groups;
    }

    private async Task<IReadOnlyList<string>> GetUserRolesFromKeycloakAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/admin/realms/{_realm}/users/{userId}/role-mappings/realm",
                ct);
            await response.EnsureSuccessOrThrowAsync();

            List<KeycloakRoleRepresentation>? roles = await response.Content.ReadFromJsonAsync<List<KeycloakRoleRepresentation>>(ct);
            if (roles == null)
            {
                return [];
            }

            return roles
                .Select(r => r.Name ?? string.Empty)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();
        }
        catch (Exception ex)
        {
            LogGetUserRolesFailed(ex, userId);
            return [];
        }
    }

    private async Task SyncGroupsToRolesAsync(
        Guid userId,
        List<string> groups,
        IReadOnlyList<string> currentRoles,
        string? defaultRole,
        CancellationToken ct)
    {
        HashSet<string> targetRoles = groups
            .Select(g => SanitizeGroupNameForRole(g))
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(defaultRole))
        {
            targetRoles.Add(defaultRole);
        }

        HashSet<string> preservedRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            "default-roles-foundry",
            "offline_access",
            "uma_authorization"
        };

        List<string> rolesToAdd = targetRoles
            .Where(r => !currentRoles.Contains(r, StringComparer.OrdinalIgnoreCase))
            .ToList();

        List<string> rolesToRemove = currentRoles
            .Where(r => !targetRoles.Contains(r) && !preservedRoles.Contains(r))
            .ToList();

        foreach (string roleName in rolesToAdd)
        {
            await TryAssignRoleAsync(userId, roleName, ct);
        }

        foreach (string roleName in rolesToRemove)
        {
            await TryRemoveRoleAsync(userId, roleName, ct);
        }

        LogRoleSyncCompleted(userId, rolesToAdd.Count, rolesToRemove.Count);
    }

    private static string SanitizeGroupNameForRole(string groupName)
    {
        string name = groupName;

        if (name.Contains(',', StringComparison.Ordinal) && name.Contains('=', StringComparison.Ordinal))
        {
            string[] parts = name.Split(',');
            string? cnPart = parts.FirstOrDefault(p => p.Trim().StartsWith("CN=", StringComparison.OrdinalIgnoreCase));
            if (cnPart != null)
            {
                name = cnPart.Trim()[3..];
            }
        }

        if (name.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
        {
            name = name[3..];
        }

        return name.Trim().ToLowerInvariant();
    }

    private async Task TryAssignRoleAsync(Guid userId, string roleName, CancellationToken ct)
    {
        try
        {
            KeycloakRoleRepresentation? role = await GetRealmRoleAsync(roleName, ct);
            if (role == null)
            {
                LogRoleDoesNotExist(roleName);
                return;
            }

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/admin/realms/{_realm}/users/{userId}/role-mappings/realm",
                new[] { role },
                ct);

            if (response.IsSuccessStatusCode)
            {
                LogRoleAssigned(roleName, userId);
            }
            else
            {
                LogAssignRoleFailed(roleName, userId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            LogTryAssignRoleFailed(ex, roleName, userId);
        }
    }

    private async Task TryRemoveRoleAsync(Guid userId, string roleName, CancellationToken ct)
    {
        try
        {
            KeycloakRoleRepresentation? role = await GetRealmRoleAsync(roleName, ct);
            if (role == null)
            {
                return;
            }

            using HttpRequestMessage request = new(HttpMethod.Delete, $"/admin/realms/{_realm}/users/{userId}/role-mappings/realm")
            {
                Content = JsonContent.Create(new[] { role })
            };

            HttpResponseMessage response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                LogRoleRemoved(roleName, userId);
            }
            else
            {
                LogRemoveRoleFailed(roleName, userId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            LogTryRemoveRoleFailed(ex, roleName, userId);
        }
    }

    private async Task<KeycloakRoleRepresentation?> GetRealmRoleAsync(string roleName, CancellationToken ct)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/roles/{roleName}", ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            return await response.Content.ReadFromJsonAsync<KeycloakRoleRepresentation>(ct);
        }
        catch (Exception ex)
        {
            LogGetRealmRoleFailed(ex, roleName);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "No SSO configuration found for tenant {TenantId}, skipping claims sync")]
    private partial void LogSkippingClaimsSyncNoConfig(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SyncGroupsAsRoles is disabled for tenant {TenantId}, skipping claims sync")]
    private partial void LogSkippingClaimsSyncDisabled(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "GroupsAttribute not configured for tenant {TenantId}, skipping claims sync")]
    private partial void LogSkippingClaimsSyncNoAttribute(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Syncing user claims for user {UserId} in tenant {TenantId}")]
    private partial void LogSyncingUserClaims(Guid userId, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "User {UserId} not found in Keycloak, skipping claims sync")]
    private partial void LogUserNotFoundForClaimsSync(Guid userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No groups found in attribute {GroupsAttribute} for user {UserId}")]
    private partial void LogNoGroupsFoundForSync(string groupsAttribute, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {GroupCount} groups for user {UserId}: {Groups}")]
    private partial void LogFoundGroupsForSync(int groupCount, Guid userId, string groups);

    [LoggerMessage(Level = LogLevel.Information, Message = "User claims synced for user {UserId} in tenant {TenantId}")]
    private partial void LogUserClaimsSynced(Guid userId, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to sync user claims for user {UserId}")]
    private partial void LogSyncUserClaimsFailed(Exception ex, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get user attributes for {UserId}")]
    private partial void LogGetUserAttributesFailed(Exception ex, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get roles for user {UserId}")]
    private partial void LogGetUserRolesFailed(Exception ex, Guid userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Role sync for user {UserId}: added {AddedCount}, removed {RemovedCount}")]
    private partial void LogRoleSyncCompleted(Guid userId, int addedCount, int removedCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Role {RoleName} does not exist in realm, skipping assignment")]
    private partial void LogRoleDoesNotExist(string roleName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Assigned role {RoleName} to user {UserId}")]
    private partial void LogRoleAssigned(string roleName, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to assign role {RoleName} to user {UserId}: {StatusCode}")]
    private partial void LogAssignRoleFailed(string roleName, Guid userId, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to assign role {RoleName} to user {UserId}")]
    private partial void LogTryAssignRoleFailed(Exception ex, string roleName, Guid userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed role {RoleName} from user {UserId}")]
    private partial void LogRoleRemoved(string roleName, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to remove role {RoleName} from user {UserId}: {StatusCode}")]
    private partial void LogRemoveRoleFailed(string roleName, Guid userId, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to remove role {RoleName} from user {UserId}")]
    private partial void LogTryRemoveRoleFailed(Exception ex, string roleName, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get realm role {RoleName}")]
    private partial void LogGetRealmRoleFailed(Exception ex, string roleName);
}
