using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Infrastructure.Extensions;
using Foundry.Shared.Contracts.Identity.Events;
using Foundry.Shared.Kernel.MultiTenancy;
using Keycloak.AuthServices.Sdk.Admin;
using Keycloak.AuthServices.Sdk.Admin.Models;
using Keycloak.AuthServices.Sdk.Admin.Requests.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Foundry.Identity.Infrastructure.Services;

public sealed partial class KeycloakAdminService : IKeycloakAdminService
{
    private readonly IKeycloakUserClient _userClient;
    private readonly HttpClient _httpClient;
    private readonly IMessageBus _messageBus;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<KeycloakAdminService> _logger;
    private readonly string _realm;

    public KeycloakAdminService(
        IKeycloakUserClient userClient,
        IHttpClientFactory httpClientFactory,
        IMessageBus messageBus,
        ITenantContext tenantContext,
        IOptions<KeycloakOptions> keycloakOptions,
        ILogger<KeycloakAdminService> logger)
    {
        _userClient = userClient;
        _httpClient = httpClientFactory.CreateClient("KeycloakAdminClient");
        _messageBus = messageBus;
        _tenantContext = tenantContext;
        _realm = keycloakOptions.Value.Realm;
        _logger = logger;
    }

    public async Task<Guid> CreateUserAsync(
        string email,
        string firstName,
        string lastName,
        string? password = null,
        CancellationToken ct = default)
    {
        LogCreatingUser(email);

        UserRepresentation userRepresentation = new()
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Username = email,
            Enabled = true,
            EmailVerified = false
        };

        if (!string.IsNullOrWhiteSpace(password))
        {
            userRepresentation.Credentials =
            [
                new CredentialRepresentation
                {
                    Type = "password",
                    Value = password,
                    Temporary = false
                }
            ];
        }

        HttpResponseMessage response = await _userClient.CreateUserWithResponseAsync(_realm, userRepresentation, ct);
        await response.EnsureSuccessOrThrowAsync(ct);

        string? locationHeader = response.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(locationHeader))
        {
            throw new InvalidOperationException("User created but Location header is missing");
        }

        Guid userId = Guid.Parse(locationHeader.Split('/').Last());

        await AssignRoleAsync(userId, "user", ct);

        await _messageBus.PublishAsync(new UserRegisteredEvent
        {
            UserId = userId,
            TenantId = _tenantContext.TenantId.Value,
            Email = email,
            FirstName = firstName,
            LastName = lastName
        });

        LogUserCreated(email, userId);

        return userId;
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            UserRepresentation user = await _userClient.GetUserAsync(_realm, userId.ToString(), false, ct);

            IReadOnlyList<string> roles = await GetUserRolesAsync(userId, ct);

            return new UserDto(
                userId,
                user.Email ?? string.Empty,
                user.FirstName ?? string.Empty,
                user.LastName ?? string.Empty,
                user.Enabled ?? false,
                roles);
        }
        catch (Exception ex)
        {
            LogGetUserFailed(ex, userId);
            return null;
        }
    }

    public async Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        try
        {
            List<UserRepresentation> users = (await _userClient.GetUsersAsync(_realm, new GetUsersRequestParameters
            {
                Email = email,
                Exact = true
            }, ct)).ToList();

            UserRepresentation? user = users.FirstOrDefault();
            if (user == null || string.IsNullOrWhiteSpace(user.Id))
            {
                return null;
            }

            Guid userId = Guid.Parse(user.Id);
            IReadOnlyList<string> roles = await GetUserRolesAsync(userId, ct);

            return new UserDto(
                userId,
                user.Email ?? string.Empty,
                user.FirstName ?? string.Empty,
                user.LastName ?? string.Empty,
                user.Enabled ?? false,
                roles);
        }
        catch (Exception ex)
        {
            LogGetUserByEmailFailed(ex, email);
            return null;
        }
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(
        string? search = null,
        int first = 0,
        int max = 20,
        CancellationToken ct = default)
    {
        try
        {
            List<UserRepresentation> users = (await _userClient.GetUsersAsync(_realm, new GetUsersRequestParameters
            {
                Search = search,
                First = first,
                Max = max
            }, ct)).ToList();

            if (users.Count == 0)
            {
                return Array.Empty<UserDto>();
            }

            List<UserRepresentation> validUsers = users
                .Where(u => !string.IsNullOrWhiteSpace(u.Id))
                .ToList();

            Task<IReadOnlyList<string>>[] roleTasks = validUsers
                .Select(u => GetUserRolesAsync(Guid.Parse(u.Id!), ct))
                .ToArray();

            IReadOnlyList<string>[] allRoles = await Task.WhenAll(roleTasks);

            List<UserDto> userDtos = new(validUsers.Count);
            for (int i = 0; i < validUsers.Count; i++)
            {
                UserRepresentation user = validUsers[i];
                Guid userId = Guid.Parse(user.Id!);
                userDtos.Add(new UserDto(
                    userId,
                    user.Email ?? string.Empty,
                    user.FirstName ?? string.Empty,
                    user.LastName ?? string.Empty,
                    user.Enabled ?? false,
                    allRoles[i]));
            }

            return userDtos;
        }
        catch (Exception ex)
        {
            LogGetUsersFailed(ex);
            return Array.Empty<UserDto>();
        }
    }

    public async Task DeactivateUserAsync(Guid userId, CancellationToken ct = default)
    {
        LogDeactivatingUser(userId);

        await _userClient.UpdateUserAsync(_realm, userId.ToString(), new UserRepresentation
        {
            Enabled = false
        }, ct);

        LogUserDeactivated(userId);
    }

    public async Task ActivateUserAsync(Guid userId, CancellationToken ct = default)
    {
        LogActivatingUser(userId);

        await _userClient.UpdateUserAsync(_realm, userId.ToString(), new UserRepresentation
        {
            Enabled = true
        }, ct);

        LogUserActivated(userId);
    }

    public async Task AssignRoleAsync(Guid userId, string roleName, CancellationToken ct = default)
    {
        LogAssigningRole(roleName, userId);

        RoleRepresentation? role = await GetRealmRoleAsync(roleName, ct);
        if (role == null)
        {
            LogRoleNotFound(roleName, _realm);
            throw new InvalidOperationException($"Role '{roleName}' not found");
        }

        UserRepresentation user = await _userClient.GetUserAsync(_realm, userId.ToString(), false, ct);
        IReadOnlyList<string> currentRoles = await GetUserRolesAsync(userId, ct);
        string oldRole = currentRoles.FirstOrDefault(r => r != roleName) ?? "none";

        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            $"/admin/realms/{_realm}/users/{userId}/role-mappings/realm",
            new[] { role },
            ct);
        await response.EnsureSuccessOrThrowAsync(ct);

        await _messageBus.PublishAsync(new UserRoleChangedEvent
        {
            UserId = userId,
            TenantId = _tenantContext.TenantId.Value,
            Email = user.Email ?? string.Empty,
            OldRole = oldRole,
            NewRole = roleName
        });

        LogRoleAssigned(roleName, userId);
    }

    public async Task RemoveRoleAsync(Guid userId, string roleName, CancellationToken ct = default)
    {
        LogRemovingRole(roleName, userId);

        RoleRepresentation? role = await GetRealmRoleAsync(roleName, ct);
        if (role == null)
        {
            LogRoleNotFound(roleName, _realm);
            return;
        }

        using HttpRequestMessage request = new(HttpMethod.Delete, $"/admin/realms/{_realm}/users/{userId}/role-mappings/realm")
        {
            Content = JsonContent.Create(new[] { role })
        };

        HttpResponseMessage response = await _httpClient.SendAsync(request, ct);
        await response.EnsureSuccessOrThrowAsync(ct);

        UserRepresentation user = await _userClient.GetUserAsync(_realm, userId.ToString(), false, ct);
        IReadOnlyList<string> currentRoles = await GetUserRolesAsync(userId, ct);
        string newRole = currentRoles.Count > 0 ? currentRoles[0] : "none";

        await _messageBus.PublishAsync(new UserRoleChangedEvent
        {
            UserId = userId,
            TenantId = _tenantContext.TenantId.Value,
            Email = user.Email ?? string.Empty,
            OldRole = roleName,
            NewRole = newRole
        });

        LogRoleRemoved(roleName, userId);
    }

    public async Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/users/{userId}/role-mappings/realm", ct);
            await response.EnsureSuccessOrThrowAsync(ct);

            List<RoleRepresentation>? roles = await response.Content.ReadFromJsonAsync<List<RoleRepresentation>>(ct);
            if (roles == null)
            {
                return Array.Empty<string>();
            }

            return roles.Select(r => r.Name ?? string.Empty).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        }
        catch (Exception ex)
        {
            LogGetUserRolesFailed(ex, userId);
            return Array.Empty<string>();
        }
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        LogDeletingUser(userId);

        await _userClient.DeleteUserAsync(_realm, userId.ToString(), ct);

        LogUserDeleted(userId);
    }

    private async Task<RoleRepresentation?> GetRealmRoleAsync(string roleName, CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/roles/{roleName}", ct);
            await response.EnsureSuccessOrThrowAsync(ct);
            return await response.Content.ReadFromJsonAsync<RoleRepresentation>(ct);
        }
        catch (Exception ex)
        {
            LogGetRealmRoleFailed(ex, roleName);
            return null;
        }
    }

}

internal record RoleRepresentation(string? Id, string? Name, string? Description, bool? ClientRole, string? ContainerId);

public sealed partial class KeycloakAdminService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Creating user {Email} in Keycloak")]
    private partial void LogCreatingUser(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {Email} created with ID {UserId}")]
    private partial void LogUserCreated(string email, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get user {UserId} from Keycloak")]
    private partial void LogGetUserFailed(Exception ex, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get user by email {Email} from Keycloak")]
    private partial void LogGetUserByEmailFailed(Exception ex, string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get users from Keycloak")]
    private partial void LogGetUsersFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deactivating user {UserId}")]
    private partial void LogDeactivatingUser(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} deactivated")]
    private partial void LogUserDeactivated(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Activating user {UserId}")]
    private partial void LogActivatingUser(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} activated")]
    private partial void LogUserActivated(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Assigning role {RoleName} to user {UserId}")]
    private partial void LogAssigningRole(string roleName, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Role {RoleName} not found in realm {Realm}")]
    private partial void LogRoleNotFound(string roleName, string realm);

    [LoggerMessage(Level = LogLevel.Information, Message = "Role {RoleName} assigned to user {UserId}")]
    private partial void LogRoleAssigned(string roleName, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removing role {RoleName} from user {UserId}")]
    private partial void LogRemovingRole(string roleName, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Role {RoleName} removed from user {UserId}")]
    private partial void LogRoleRemoved(string roleName, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get roles for user {UserId}")]
    private partial void LogGetUserRolesFailed(Exception ex, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleting user {UserId} from Keycloak")]
    private partial void LogDeletingUser(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} deleted from Keycloak")]
    private partial void LogUserDeleted(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get realm role {RoleName}")]
    private partial void LogGetRealmRoleFailed(Exception ex, string roleName);
}
