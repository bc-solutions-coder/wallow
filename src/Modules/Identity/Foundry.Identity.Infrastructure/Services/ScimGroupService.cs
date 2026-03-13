using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Infrastructure.Extensions;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Foundry.Identity.Infrastructure.Services;

public sealed partial class ScimGroupService(
    IHttpClientFactory httpClientFactory,
    IScimConfigurationRepository scimRepository,
    IScimSyncLogRepository syncLogRepository,
    ITenantContext tenantContext,
    IOptions<KeycloakOptions> keycloakOptions,
    ILogger<ScimGroupService> logger,
    TimeProvider timeProvider)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("KeycloakAdminClient");
    private readonly string _realm = keycloakOptions.Value.Realm;

    public async Task<ScimGroup> CreateGroupAsync(ScimGroupRequest request, CancellationToken ct = default)
    {
        string externalId = request.ExternalId ?? Guid.NewGuid().ToString();

        LogCreatingScimGroup(request.DisplayName);

        try
        {
            var groupRepresentation = new
            {
                name = request.DisplayName,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { externalId }
                }
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/admin/realms/{_realm}/groups",
                groupRepresentation,
                ct);
            await response.EnsureSuccessOrThrowAsync();

            string? locationHeader = response.Headers.Location?.ToString();
            string groupId = locationHeader?.Split('/').Last() ?? throw new InvalidOperationException("Group created but Location header is missing");

            if (request.Members != null)
            {
                foreach (ScimMember member in request.Members)
                {
                    await AddUserToGroupAsync(member.Value, groupId, ct);
                }
            }

            await LogSyncAsync(ScimOperation.Create, ScimResourceType.Group, externalId, groupId, true, ct: ct);

            LogScimGroupCreated(request.DisplayName, groupId);

            return await GetGroupAsync(groupId, ct) ?? throw new InvalidOperationException("Failed to retrieve created group");
        }
        catch (Exception ex)
        {
            LogCreateScimGroupFailed(ex, request.DisplayName);
            await LogSyncAsync(ScimOperation.Create, ScimResourceType.Group, externalId, null, false, ex.Message, ct: ct);
            throw;
        }
    }

    public async Task<ScimGroup> UpdateGroupAsync(string id, ScimGroupRequest request, CancellationToken ct = default)
    {
        string externalId = request.ExternalId ?? id;

        LogUpdatingScimGroup(id);

        try
        {
            var groupRepresentation = new
            {
                name = request.DisplayName,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { externalId }
                }
            };

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/admin/realms/{_realm}/groups/{id}",
                groupRepresentation,
                ct);
            await response.EnsureSuccessOrThrowAsync();

            if (request.Members != null)
            {
                List<string> currentMembers = await GetGroupMembersAsync(id, ct);

                foreach (string member in currentMembers)
                {
                    if (!request.Members.Any(m => m.Value == member))
                    {
                        await RemoveUserFromGroupAsync(member, id, ct);
                    }
                }

                foreach (ScimMember member in request.Members)
                {
                    if (!currentMembers.Contains(member.Value))
                    {
                        await AddUserToGroupAsync(member.Value, id, ct);
                    }
                }
            }

            await LogSyncAsync(ScimOperation.Update, ScimResourceType.Group, externalId, id, true, ct: ct);

            LogScimGroupUpdated(id);

            return await GetGroupAsync(id, ct) ?? throw new InvalidOperationException("Failed to retrieve updated group");
        }
        catch (Exception ex)
        {
            LogUpdateScimGroupFailed(ex, id);
            await LogSyncAsync(ScimOperation.Update, ScimResourceType.Group, externalId, id, false, ex.Message, ct: ct);
            throw;
        }
    }

    public async Task DeleteGroupAsync(string id, CancellationToken ct = default)
    {
        LogDeletingScimGroup(id);

        try
        {
            HttpResponseMessage response = await _httpClient.DeleteAsync($"/admin/realms/{_realm}/groups/{id}", ct);
            await response.EnsureSuccessOrThrowAsync();

            await LogSyncAsync(ScimOperation.Delete, ScimResourceType.Group, id, id, true, ct: ct);

            LogScimGroupDeleted(id);
        }
        catch (Exception ex)
        {
            LogDeleteScimGroupFailed(ex, id);
            await LogSyncAsync(ScimOperation.Delete, ScimResourceType.Group, id, id, false, ex.Message, ct: ct);
            throw;
        }
    }

    public async Task<ScimListResponse<ScimGroup>> ListGroupsAsync(ScimListRequest request, CancellationToken ct = default)
    {
        int first = Math.Max(0, request.StartIndex - 1);
        int max = Math.Min(request.Count, ScimConstants.MaxPageSize);

        HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/groups?first={first}&max={max}", ct);
        await response.EnsureSuccessOrThrowAsync();

        List<ScimKeycloakGroupRepresentation>? groups = await response.Content.ReadFromJsonAsync<List<ScimKeycloakGroupRepresentation>>(ct);

        List<ScimGroup> scimGroups = [];
        foreach (ScimKeycloakGroupRepresentation group in groups ?? [])
        {
            if (group.Id != null)
            {
                ScimGroup? scimGroup = await GetGroupAsync(group.Id, ct);
                if (scimGroup != null)
                {
                    scimGroups.Add(scimGroup);
                }
            }
        }

        return new ScimListResponse<ScimGroup>
        {
            TotalResults = groups?.Count ?? 0,
            StartIndex = request.StartIndex,
            ItemsPerPage = scimGroups.Count,
            Resources = scimGroups
        };
    }

    public async Task<ScimGroup?> GetGroupAsync(string id, CancellationToken ct)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/groups/{id}", ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            ScimKeycloakGroupRepresentation? group = await response.Content.ReadFromJsonAsync<ScimKeycloakGroupRepresentation>(ct);
            if (group == null)
            {
                return null;
            }

            List<string> members = await GetGroupMembersAsync(id, ct);

            string? externalId = group.Attributes?.GetValueOrDefault("scim_external_id")?.FirstOrDefault();

            return new ScimGroup
            {
                Id = id,
                ExternalId = externalId,
                DisplayName = group.Name ?? string.Empty,
                Members = members.Select(m => new ScimMember
                {
                    Value = m,
                    Ref = $"/scim/v2/Users/{m}",
                    Type = "User"
                }).ToList(),
                Meta = new ScimMeta
                {
                    ResourceType = "Group",
                    Location = $"/scim/v2/Groups/{id}"
                }
            };
        }
        catch (Exception ex)
        {
            LogGetScimGroupFailed(ex, id);
            return null;
        }
    }

    private async Task<List<string>> GetGroupMembersAsync(string groupId, CancellationToken ct)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/groups/{groupId}/members", ct);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            List<ScimKeycloakUserRepresentation>? members = await response.Content.ReadFromJsonAsync<List<ScimKeycloakUserRepresentation>>(ct);
            return members?.Select(m => m.Id ?? "").Where(id => !string.IsNullOrEmpty(id)).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task AddUserToGroupAsync(string userId, string groupId, CancellationToken ct)
    {
        HttpResponseMessage response = await _httpClient.PutAsync(
            $"/admin/realms/{_realm}/users/{userId}/groups/{groupId}",
            null,
            ct);
        await response.EnsureSuccessOrThrowAsync();
    }

    private async Task RemoveUserFromGroupAsync(string userId, string groupId, CancellationToken ct)
    {
        HttpResponseMessage response = await _httpClient.DeleteAsync(
            $"/admin/realms/{_realm}/users/{userId}/groups/{groupId}",
            ct);
        await response.EnsureSuccessOrThrowAsync();
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
                tenantContext.TenantId,
                operation,
                resourceType,
                externalId,
                internalId,
                success,
                timeProvider,
                errorMessage,
                requestBody);

            syncLogRepository.Add(log);
            await syncLogRepository.SaveChangesAsync(ct);

            ScimConfiguration? config = await scimRepository.GetAsync(ct);
            if (config != null)
            {
                config.RecordSync(Guid.Empty, timeProvider);
                await scimRepository.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            LogSyncLogFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Creating SCIM group {DisplayName}")]
    private partial void LogCreatingScimGroup(string displayName);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCIM group {DisplayName} created with ID {GroupId}")]
    private partial void LogScimGroupCreated(string displayName, string groupId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create SCIM group {DisplayName}")]
    private partial void LogCreateScimGroupFailed(Exception ex, string displayName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updating SCIM group {GroupId}")]
    private partial void LogUpdatingScimGroup(string groupId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCIM group {GroupId} updated")]
    private partial void LogScimGroupUpdated(string groupId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update SCIM group {GroupId}")]
    private partial void LogUpdateScimGroupFailed(Exception ex, string groupId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleting SCIM group {GroupId}")]
    private partial void LogDeletingScimGroup(string groupId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCIM group {GroupId} deleted")]
    private partial void LogScimGroupDeleted(string groupId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete SCIM group {GroupId}")]
    private partial void LogDeleteScimGroupFailed(Exception ex, string groupId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get SCIM group {GroupId}")]
    private partial void LogGetScimGroupFailed(Exception ex, string groupId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to log SCIM sync operation")]
    private partial void LogSyncLogFailed(Exception ex);
}
