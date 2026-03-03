using System.Globalization;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Infrastructure.Scim;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Keycloak.AuthServices.Sdk.Admin.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Foundry.Identity.Infrastructure.Services;

public sealed partial class ScimUserService
{
    private readonly HttpClient _httpClient;
    private readonly IScimConfigurationRepository _scimRepository;
    private readonly IScimSyncLogRepository _syncLogRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ScimUserService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly string _realm;

    public ScimUserService(
        IHttpClientFactory httpClientFactory,
        IScimConfigurationRepository scimRepository,
        IScimSyncLogRepository syncLogRepository,
        ITenantContext tenantContext,
        IOptions<KeycloakOptions> keycloakOptions,
        ILogger<ScimUserService> logger,
        TimeProvider timeProvider)
    {
        _httpClient = httpClientFactory.CreateClient("KeycloakAdminClient");
        _scimRepository = scimRepository;
        _syncLogRepository = syncLogRepository;
        _tenantContext = tenantContext;
        _realm = keycloakOptions.Value.Realm;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<ScimUser> CreateUserAsync(ScimUserRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = _tenantContext.TenantId;
        string externalId = request.ExternalId ?? Guid.NewGuid().ToString();

        LogCreatingScimUser(request.UserName, tenantId.Value);

        try
        {
            string email = request.Emails?.FirstOrDefault(e => e.Primary)?.Value ?? request.UserName;

            UserRepresentation userRepresentation = new()
            {
                Username = request.UserName,
                Email = email,
                FirstName = request.Name?.GivenName,
                LastName = request.Name?.FamilyName,
                Enabled = request.Active,
                Attributes = new Dictionary<string, ICollection<string>>
                {
                    ["scim_external_id"] = new[] { externalId }
                }
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/admin/realms/{_realm}/users",
                userRepresentation,
                ct);
            response.EnsureSuccessStatusCode();

            string? locationHeader = response.Headers.Location?.ToString();
            string userId = locationHeader?.Split('/').Last() ?? throw new InvalidOperationException("User created but Location header is missing");

            await AddUserToOrganizationAsync(userId, ct);
            await AssignDefaultRoleAsync(userId, ct);
            await LogSyncAsync(ScimOperation.Create, ScimResourceType.User, externalId, userId, true, ct: ct);

            LogScimUserCreated(request.UserName, userId);

            return await GetUserAsync(userId, ct) ?? throw new InvalidOperationException("Failed to retrieve created user");
        }
        catch (Exception ex)
        {
            LogCreateScimUserFailed(ex, request.UserName);
            await LogSyncAsync(ScimOperation.Create, ScimResourceType.User, externalId, null, false, ex.Message, ct: ct);
            throw;
        }
    }

    public async Task<ScimUser> UpdateUserAsync(string id, ScimUserRequest request, CancellationToken ct = default)
    {
        string externalId = request.ExternalId ?? id;

        LogUpdatingScimUser(id);

        try
        {
            string email = request.Emails?.FirstOrDefault(e => e.Primary)?.Value ?? request.UserName;

            UserRepresentation userRepresentation = new()
            {
                Username = request.UserName,
                Email = email,
                FirstName = request.Name?.GivenName,
                LastName = request.Name?.FamilyName,
                Enabled = request.Active,
                Attributes = new Dictionary<string, ICollection<string>>
                {
                    ["scim_external_id"] = new[] { externalId }
                }
            };

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/admin/realms/{_realm}/users/{id}",
                userRepresentation,
                ct);
            response.EnsureSuccessStatusCode();

            await LogSyncAsync(ScimOperation.Update, ScimResourceType.User, externalId, id, true, ct: ct);

            LogScimUserUpdated(id);

            return await GetUserAsync(id, ct) ?? throw new InvalidOperationException("Failed to retrieve updated user");
        }
        catch (Exception ex)
        {
            LogUpdateScimUserFailed(ex, id);
            await LogSyncAsync(ScimOperation.Update, ScimResourceType.User, externalId, id, false, ex.Message, ct: ct);
            throw;
        }
    }

    public async Task<ScimUser> PatchUserAsync(string id, ScimPatchRequest request, CancellationToken ct = default)
    {
        LogPatchingScimUser(id, request.Operations.Count);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/users/{id}", ct);
            response.EnsureSuccessStatusCode();
            ScimKeycloakUserRepresentation? currentUser = await response.Content.ReadFromJsonAsync<ScimKeycloakUserRepresentation>(ct);

            if (currentUser == null)
            {
                throw new InvalidOperationException($"User {id} not found");
            }

            foreach (ScimPatchOperation op in request.Operations)
            {
                ApplyPatchOperation(currentUser, op);
            }

            HttpResponseMessage updateResponse = await _httpClient.PutAsJsonAsync(
                $"/admin/realms/{_realm}/users/{id}",
                currentUser,
                ct);
            updateResponse.EnsureSuccessStatusCode();

            string externalId = currentUser.Attributes?.GetValueOrDefault("scim_external_id")?.FirstOrDefault() ?? id;
            await LogSyncAsync(ScimOperation.Patch, ScimResourceType.User, externalId, id, true, ct: ct);

            LogScimUserPatched(id);

            return await GetUserAsync(id, ct) ?? throw new InvalidOperationException("Failed to retrieve patched user");
        }
        catch (Exception ex)
        {
            LogPatchScimUserFailed(ex, id);
            await LogSyncAsync(ScimOperation.Patch, ScimResourceType.User, id, id, false, ex.Message, ct: ct);
            throw;
        }
    }

    public async Task DeleteUserAsync(string id, CancellationToken ct = default)
    {
        LogDeletingScimUser(id);

        try
        {
            ScimConfiguration? config = await _scimRepository.GetAsync(ct);

            if (config?.DeprovisionOnDelete == true)
            {
                HttpResponseMessage response = await _httpClient.DeleteAsync($"/admin/realms/{_realm}/users/{id}", ct);
                response.EnsureSuccessStatusCode();
                LogScimUserDeleted(id);
            }
            else
            {
                UserRepresentation disableRequest = new() { Enabled = false };
                HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                    $"/admin/realms/{_realm}/users/{id}",
                    disableRequest,
                    ct);
                response.EnsureSuccessStatusCode();
                LogScimUserDisabled(id);
            }

            await LogSyncAsync(ScimOperation.Delete, ScimResourceType.User, id, id, true, ct: ct);
        }
        catch (Exception ex)
        {
            LogDeleteScimUserFailed(ex, id);
            await LogSyncAsync(ScimOperation.Delete, ScimResourceType.User, id, id, false, ex.Message, ct: ct);
            throw;
        }
    }

    public async Task<ScimUser?> GetUserAsync(string id, CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/users/{id}", ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            ScimKeycloakUserRepresentation? user = await response.Content.ReadFromJsonAsync<ScimKeycloakUserRepresentation>(ct);
            if (user == null)
            {
                return null;
            }

            return MapToScimUser(user, id);
        }
        catch (Exception ex)
        {
            LogGetScimUserFailed(ex, id);
            return null;
        }
    }

    public async Task<ScimListResponse<ScimUser>> ListUsersAsync(ScimListRequest request, CancellationToken ct = default)
    {
        int first = Math.Max(0, request.StartIndex - 1);
        int max = Math.Min(request.Count, ScimConstants.MaxPageSize);

        ScimToKeycloakTranslator translator = new();
        KeycloakQueryParams keycloakParams = translator.Translate(request.Filter);

        List<string> queryParams =
        [
            $"first={first}",
            $"max={max}"
        ];

        if (keycloakParams.Username != null)
        {
            queryParams.Add($"username={Uri.EscapeDataString(keycloakParams.Username)}");
        }

        if (keycloakParams.Email != null)
        {
            queryParams.Add($"email={Uri.EscapeDataString(keycloakParams.Email)}");
        }

        if (keycloakParams.FirstName != null)
        {
            queryParams.Add($"firstName={Uri.EscapeDataString(keycloakParams.FirstName)}");
        }

        if (keycloakParams.LastName != null)
        {
            queryParams.Add($"lastName={Uri.EscapeDataString(keycloakParams.LastName)}");
        }

        if (keycloakParams.Search != null)
        {
            queryParams.Add($"search={Uri.EscapeDataString(keycloakParams.Search)}");
        }

        string queryString = string.Join("&", queryParams);
        HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/users?{queryString}", ct);
        response.EnsureSuccessStatusCode();

        List<ScimKeycloakUserRepresentation>? users = await response.Content.ReadFromJsonAsync<List<ScimKeycloakUserRepresentation>>(ct);

        List<ScimUser> scimUsers = users?.Select(u => MapToScimUser(u, u.Id ?? "")).ToList() ?? [];

        if (keycloakParams.InMemoryFilter != null)
        {
            scimUsers = scimUsers.Where(keycloakParams.InMemoryFilter).ToList();
        }

        HttpResponseMessage countResponse = await _httpClient.GetAsync($"/admin/realms/{_realm}/users/count", ct);
        int totalCount = await countResponse.Content.ReadFromJsonAsync<int>(ct);

        return new ScimListResponse<ScimUser>
        {
            TotalResults = keycloakParams.InMemoryFilter != null ? scimUsers.Count : totalCount,
            StartIndex = request.StartIndex,
            ItemsPerPage = scimUsers.Count,
            Resources = scimUsers
        };
    }

    private async Task AddUserToOrganizationAsync(string userId, CancellationToken ct)
    {
        TenantId tenantId = _tenantContext.TenantId;

        try
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/admin/realms/{_realm}/organizations/{tenantId.Value}/members",
                new { id = userId },
                ct);
            if (!response.IsSuccessStatusCode)
            {
                LogAddUserToOrgFailed(userId, tenantId.Value);
            }
        }
        catch (Exception ex)
        {
            LogAddUserToOrgException(ex, userId);
        }
    }

    private async Task AssignDefaultRoleAsync(string userId, CancellationToken ct)
    {
        ScimConfiguration? config = await _scimRepository.GetAsync(ct);
        if (string.IsNullOrWhiteSpace(config?.DefaultRole))
        {
            return;
        }

        try
        {
            HttpResponseMessage roleResponse = await _httpClient.GetAsync($"/admin/realms/{_realm}/roles/{config.DefaultRole}", ct);
            if (!roleResponse.IsSuccessStatusCode)
            {
                LogDefaultRoleNotFound(config.DefaultRole);
                return;
            }

            ScimKeycloakRoleRepresentation? role = await roleResponse.Content.ReadFromJsonAsync<ScimKeycloakRoleRepresentation>(ct);

            HttpResponseMessage assignResponse = await _httpClient.PostAsJsonAsync(
                $"/admin/realms/{_realm}/users/{userId}/role-mappings/realm",
                new[] { role },
                ct);
            assignResponse.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            LogAssignDefaultRoleFailed(ex, config.DefaultRole, userId);
        }
    }

    private async Task LogSyncAsync(
        ScimOperation operation,
        ScimResourceType resourceType,
        string externalId,
        string? internalId,
        bool success,
        string? errorMessage = null,
        string? requestBody = null,
        CancellationToken ct = default)
    {
        try
        {
            ScimSyncLog log = ScimSyncLog.Create(
                _tenantContext.TenantId,
                operation,
                resourceType,
                externalId,
                internalId,
                success,
                _timeProvider,
                errorMessage,
                requestBody);

            _syncLogRepository.Add(log);
            await _syncLogRepository.SaveChangesAsync(ct);

            ScimConfiguration? config = await _scimRepository.GetAsync(ct);
            if (config != null)
            {
                config.RecordSync(Guid.Empty, _timeProvider);
                await _scimRepository.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            LogSyncLogFailed(ex);
        }
    }

    private static void ApplyPatchOperation(ScimKeycloakUserRepresentation user, ScimPatchOperation op)
    {
        string? path = op.Path?.ToLowerInvariant();

        switch (op.Op.ToLowerInvariant())
        {
            case "replace":
            case "add":
                switch (path)
                {
                    case "active":
                        user.Enabled = op.Value is bool b ? b : bool.Parse(Convert.ToString(op.Value, CultureInfo.InvariantCulture) ?? "true");
                        break;
                    case "username":
                    case "userName":
                        user.Username = Convert.ToString(op.Value, CultureInfo.InvariantCulture);
                        break;
                    case "name.givenname":
                        user.FirstName = Convert.ToString(op.Value, CultureInfo.InvariantCulture);
                        break;
                    case "name.familyname":
                        user.LastName = Convert.ToString(op.Value, CultureInfo.InvariantCulture);
                        break;
                    case "emails":
                    case "emails[type eq \"work\"].value":
                    case "emails[primary eq true].value":
                        user.Email = Convert.ToString(op.Value, CultureInfo.InvariantCulture);
                        break;
                }
                break;
            case "remove":
                break;
        }
    }

    private static ScimUser MapToScimUser(ScimKeycloakUserRepresentation user, string id)
    {
        string? externalId = user.Attributes?.GetValueOrDefault("scim_external_id")?.FirstOrDefault();

        return new ScimUser
        {
            Id = id,
            ExternalId = externalId,
            UserName = user.Username ?? user.Email ?? string.Empty,
            Name = new ScimName
            {
                GivenName = user.FirstName,
                FamilyName = user.LastName,
                Formatted = $"{user.FirstName} {user.LastName}".Trim()
            },
            DisplayName = $"{user.FirstName} {user.LastName}".Trim(),
            Emails = string.IsNullOrWhiteSpace(user.Email) ? null : new[]
            {
                new ScimEmail
                {
                    Value = user.Email,
                    Type = "work",
                    Primary = true
                }
            },
            Active = user.Enabled ?? true,
            Meta = new ScimMeta
            {
                ResourceType = "User",
                Location = $"/scim/v2/Users/{id}"
            }
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Creating SCIM user {UserName} for tenant {TenantId}")]
    private partial void LogCreatingScimUser(string userName, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCIM user {UserName} created with ID {UserId}")]
    private partial void LogScimUserCreated(string userName, string userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create SCIM user {UserName}")]
    private partial void LogCreateScimUserFailed(Exception ex, string userName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updating SCIM user {UserId}")]
    private partial void LogUpdatingScimUser(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCIM user {UserId} updated")]
    private partial void LogScimUserUpdated(string userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update SCIM user {UserId}")]
    private partial void LogUpdateScimUserFailed(Exception ex, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Patching SCIM user {UserId} with {OpCount} operations")]
    private partial void LogPatchingScimUser(string userId, int opCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCIM user {UserId} patched")]
    private partial void LogScimUserPatched(string userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to patch SCIM user {UserId}")]
    private partial void LogPatchScimUserFailed(Exception ex, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleting SCIM user {UserId}")]
    private partial void LogDeletingScimUser(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCIM user {UserId} deleted")]
    private partial void LogScimUserDeleted(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCIM user {UserId} disabled")]
    private partial void LogScimUserDisabled(string userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete SCIM user {UserId}")]
    private partial void LogDeleteScimUserFailed(Exception ex, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get SCIM user {UserId}")]
    private partial void LogGetScimUserFailed(Exception ex, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not add user {UserId} to organization {OrgId}")]
    private partial void LogAddUserToOrgFailed(string userId, Guid orgId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to add user {UserId} to organization")]
    private partial void LogAddUserToOrgException(Exception ex, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Role {RoleName} not found")]
    private partial void LogDefaultRoleNotFound(string roleName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to assign default role {RoleName} to user {UserId}")]
    private partial void LogAssignDefaultRoleFailed(Exception ex, string roleName, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to log SCIM sync operation")]
    private partial void LogSyncLogFailed(Exception ex);
}
