using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class ScimGroupService(
    IOrganizationService organizationService,
    IScimConfigurationRepository scimRepository,
    IScimSyncLogRepository syncLogRepository,
    ITenantContext tenantContext,
    ILogger<ScimGroupService> logger,
    TimeProvider timeProvider)
{
    public async Task<ScimGroup> CreateGroupAsync(ScimGroupRequest request, CancellationToken ct = default)
    {
        string externalId = request.ExternalId ?? Guid.NewGuid().ToString();

        LogCreatingScimGroup(request.DisplayName);

        try
        {
            Guid orgId = await organizationService.CreateOrganizationAsync(request.DisplayName, ct: ct);

            if (request.Members != null)
            {
                foreach (ScimMember member in request.Members)
                {
                    if (Guid.TryParse(member.Value, out Guid userId))
                    {
                        await organizationService.AddMemberAsync(orgId, userId, ct);
                    }
                }
            }

            string groupId = orgId.ToString();
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
            if (!Guid.TryParse(id, out Guid orgId))
            {
                throw new InvalidOperationException($"Invalid group ID '{id}'");
            }

            OrganizationDto? org = await organizationService.GetOrganizationByIdAsync(orgId, ct);
            if (org is null)
            {
                throw new InvalidOperationException($"Group '{id}' not found");
            }

            if (request.Members != null)
            {
                IReadOnlyList<UserDto> currentMembers = await organizationService.GetMembersAsync(orgId, ct);
                HashSet<Guid> currentMemberIds = currentMembers.Select(m => m.Id).ToHashSet();
                HashSet<Guid> requestedMemberIds = request.Members
                    .Where(m => Guid.TryParse(m.Value, out _))
                    .Select(m => Guid.Parse(m.Value))
                    .ToHashSet();

                foreach (Guid memberId in currentMemberIds)
                {
                    if (!requestedMemberIds.Contains(memberId))
                    {
                        await organizationService.RemoveMemberAsync(orgId, memberId, ct);
                    }
                }

                foreach (Guid memberId in requestedMemberIds)
                {
                    if (!currentMemberIds.Contains(memberId))
                    {
                        await organizationService.AddMemberAsync(orgId, memberId, ct);
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
            if (!Guid.TryParse(id, out Guid orgId))
            {
                throw new InvalidOperationException($"Invalid group ID '{id}'");
            }

            // Organization deletion not yet supported via IOrganizationService;
            // for now we remove all members to effectively disable the group
            IReadOnlyList<UserDto> members = await organizationService.GetMembersAsync(orgId, ct);
            foreach (UserDto member in members)
            {
                await organizationService.RemoveMemberAsync(orgId, member.Id, ct);
            }

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

        IReadOnlyList<OrganizationDto> organizations = await organizationService.GetOrganizationsAsync(
            search: null,
            first: first,
            max: max,
            ct: ct);

        List<ScimGroup> scimGroups = [];
        foreach (OrganizationDto org in organizations)
        {
            ScimGroup? scimGroup = await GetGroupAsync(org.Id.ToString(), ct);
            if (scimGroup != null)
            {
                scimGroups.Add(scimGroup);
            }
        }

        return new ScimListResponse<ScimGroup>
        {
            TotalResults = organizations.Count,
            StartIndex = request.StartIndex,
            ItemsPerPage = scimGroups.Count,
            Resources = scimGroups
        };
    }

    public async Task<ScimGroup?> GetGroupAsync(string id, CancellationToken ct)
    {
        try
        {
            if (!Guid.TryParse(id, out Guid orgId))
            {
                return null;
            }

            OrganizationDto? org = await organizationService.GetOrganizationByIdAsync(orgId, ct);
            if (org is null)
            {
                return null;
            }

            IReadOnlyList<UserDto> members = await organizationService.GetMembersAsync(orgId, ct);

            return new ScimGroup
            {
                Id = id,
                ExternalId = id,
                DisplayName = org.Name,
                Members = members.Select(m => new ScimMember
                {
                    Value = m.Id.ToString(),
                    Ref = $"/scim/v2/Users/{m.Id}",
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
