using System.Net.Http.Json;
using Foundry.Identity.Infrastructure.Extensions;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Contracts.Identity.Events;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Foundry.Identity.Infrastructure.Services;

public sealed partial class KeycloakOrganizationService(
    IHttpClientFactory httpClientFactory,
    IMessageBus messageBus,
    ITenantContext tenantContext,
    IOptions<KeycloakOptions> keycloakOptions,
    ILogger<KeycloakOrganizationService> logger) : IKeycloakOrganizationService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("KeycloakAdminClient");
    private readonly string _realm = keycloakOptions.Value.Realm;

    public async Task<Guid> CreateOrganizationAsync(string name, string? domain = null, string? creatorEmail = null, CancellationToken ct = default)
    {
        LogCreatingOrganization(name);

        CreateOrganizationRequest createRequest = new(
            name,
            string.IsNullOrWhiteSpace(domain) ? null : [new OrgDomain(domain)]);

        HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"/admin/realms/{_realm}/organizations", createRequest, ct);
        await response.EnsureSuccessOrThrowAsync();

        string? locationHeader = response.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(locationHeader))
        {
            throw new InvalidOperationException("Organization created but Location header is missing");
        }

        Guid orgId = Guid.Parse(locationHeader.Split('/').Last());

        await messageBus.PublishAsync(new OrganizationCreatedEvent
        {
            OrganizationId = orgId,
            TenantId = tenantContext.TenantId.Value,
            Name = name,
            Domain = domain,
            CreatorEmail = creatorEmail ?? string.Empty
        });

        LogOrganizationCreated(name, orgId);

        return orgId;
    }

    public async Task<OrganizationDto?> GetOrganizationByIdAsync(Guid orgId, CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/organizations/{orgId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                LogOrganizationNotFound(orgId);
                return null;
            }

            OrgRepresentation? org = await response.Content.ReadFromJsonAsync<OrgRepresentation>(ct);
            if (org == null)
            {
                return null;
            }

            int membersCount = await GetMembersCountAsync(orgId, ct);

            return new OrganizationDto(
                Guid.Parse(org.Id!),
                org.Name ?? string.Empty,
                org.Domains?.FirstOrDefault()?.Name,
                membersCount);
        }
        catch (Exception ex)
        {
            LogGetOrganizationFailed(ex, orgId);
            return null;
        }
    }

    public async Task<IReadOnlyList<OrganizationDto>> GetOrganizationsAsync(
        string? search = null,
        int first = 0,
        int max = 20,
        CancellationToken ct = default)
    {
        try
        {
            List<string> queryParams =
            [
                $"first={first}",
                $"max={max}"
            ];

            if (!string.IsNullOrWhiteSpace(search))
            {
                queryParams.Add($"search={Uri.EscapeDataString(search)}");
            }

            string queryString = string.Join("&", queryParams);
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/organizations?{queryString}", ct);
            await response.EnsureSuccessOrThrowAsync();

            List<OrgRepresentation>? orgs = await response.Content.ReadFromJsonAsync<List<OrgRepresentation>>(ct);
            if (orgs == null || orgs.Count == 0)
            {
                return [];
            }

            List<OrgRepresentation> validOrgs = orgs
                .Where(o => !string.IsNullOrWhiteSpace(o.Id))
                .ToList();

            if (validOrgs.Count == 0)
            {
                return [];
            }

            Task<int>[] countTasks = validOrgs
                .Select(o => GetMembersCountAsync(Guid.Parse(o.Id!), ct))
                .ToArray();

            int[] allCounts = await Task.WhenAll(countTasks);

            List<OrganizationDto> orgDtos = new(validOrgs.Count);
            for (int i = 0; i < validOrgs.Count; i++)
            {
                OrgRepresentation org = validOrgs[i];
                orgDtos.Add(new OrganizationDto(
                    Guid.Parse(org.Id!),
                    org.Name ?? string.Empty,
                    org.Domains?.FirstOrDefault()?.Name,
                    allCounts[i]));
            }

            return orgDtos;
        }
        catch (Exception ex)
        {
            LogGetOrganizationsFailed(ex);
            return [];
        }
    }

    public async Task AddMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default)
    {
        LogAddingMember(userId, orgId);

        var addMemberRequest = new { id = userId.ToString() };

        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            $"/admin/realms/{_realm}/organizations/{orgId}/members",
            addMemberRequest,
            ct);
        await response.EnsureSuccessOrThrowAsync();

        string userEmail = await GetUserEmailAsync(userId, ct);

        await messageBus.PublishAsync(new OrganizationMemberAddedEvent
        {
            OrganizationId = orgId,
            TenantId = tenantContext.TenantId.Value,
            UserId = userId,
            Email = userEmail
        });

        LogMemberAdded(userId, orgId);
    }

    public async Task RemoveMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default)
    {
        LogRemovingMember(userId, orgId);

        string userEmail = await GetUserEmailAsync(userId, ct);

        HttpResponseMessage response = await _httpClient.DeleteAsync(
            $"/admin/realms/{_realm}/organizations/{orgId}/members/{userId}",
            ct);
        await response.EnsureSuccessOrThrowAsync();

        await messageBus.PublishAsync(new OrganizationMemberRemovedEvent
        {
            OrganizationId = orgId,
            TenantId = tenantContext.TenantId.Value,
            UserId = userId,
            Email = userEmail
        });

        LogMemberRemoved(userId, orgId);
    }

    public async Task<IReadOnlyList<UserDto>> GetMembersAsync(Guid orgId, CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/organizations/{orgId}/members", ct);
            await response.EnsureSuccessOrThrowAsync();

            List<OrgUserRepresentation>? users = await response.Content.ReadFromJsonAsync<List<OrgUserRepresentation>>(ct);
            if (users == null || users.Count == 0)
            {
                return [];
            }

            List<OrgUserRepresentation> validUsers = users
                .Where(u => !string.IsNullOrWhiteSpace(u.Id))
                .ToList();

            if (validUsers.Count == 0)
            {
                return [];
            }

            Task<IReadOnlyList<string>>[] roleTasks = validUsers
                .Select(u => GetUserRolesAsync(Guid.Parse(u.Id!), ct))
                .ToArray();

            IReadOnlyList<string>[] allRoles = await Task.WhenAll(roleTasks);

            List<UserDto> userDtos = new(validUsers.Count);
            for (int i = 0; i < validUsers.Count; i++)
            {
                OrgUserRepresentation user = validUsers[i];
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
            LogGetMembersFailed(ex, orgId);
            return [];
        }
    }

    public async Task<IReadOnlyList<OrganizationDto>> GetUserOrganizationsAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/users/{userId}/organizations", ct);
            await response.EnsureSuccessOrThrowAsync();

            List<OrgRepresentation>? orgs = await response.Content.ReadFromJsonAsync<List<OrgRepresentation>>(ct);
            if (orgs == null || orgs.Count == 0)
            {
                return [];
            }

            List<OrgRepresentation> validOrgs = orgs
                .Where(o => !string.IsNullOrWhiteSpace(o.Id))
                .ToList();

            if (validOrgs.Count == 0)
            {
                return [];
            }

            Task<int>[] countTasks = validOrgs
                .Select(o => GetMembersCountAsync(Guid.Parse(o.Id!), ct))
                .ToArray();

            int[] allCounts = await Task.WhenAll(countTasks);

            List<OrganizationDto> orgDtos = new(validOrgs.Count);
            for (int i = 0; i < validOrgs.Count; i++)
            {
                OrgRepresentation org = validOrgs[i];
                orgDtos.Add(new OrganizationDto(
                    Guid.Parse(org.Id!),
                    org.Name ?? string.Empty,
                    org.Domains?.FirstOrDefault()?.Name,
                    allCounts[i]));
            }

            return orgDtos;
        }
        catch (Exception ex)
        {
            LogGetUserOrganizationsFailed(ex, userId);
            return [];
        }
    }

    private async Task<int> GetMembersCountAsync(Guid orgId, CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/organizations/{orgId}/members", ct);
            if (!response.IsSuccessStatusCode)
            {
                return 0;
            }

            List<OrgUserRepresentation>? members = await response.Content.ReadFromJsonAsync<List<OrgUserRepresentation>>(ct);
            return members?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<string> GetUserEmailAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/users/{userId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            OrgUserRepresentation? user = await response.Content.ReadFromJsonAsync<OrgUserRepresentation>(ct);
            return user?.Email ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{_realm}/users/{userId}/role-mappings/realm", ct);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            List<OrgRoleRepresentation>? roles = await response.Content.ReadFromJsonAsync<List<OrgRoleRepresentation>>(ct);
            if (roles == null)
            {
                return [];
            }

            return roles.Select(r => r.Name ?? string.Empty).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        }
        catch
        {
            return [];
        }
    }

}

file sealed record CreateOrganizationRequest(string Name, OrgDomain[]? Domains);

file sealed record OrgDomain(string Name);

file sealed record OrgRepresentation
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public OrgDomain[]? Domains { get; init; }
}

file sealed record OrgUserRepresentation
{
    public string? Id { get; init; }
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public bool? Enabled { get; init; }
}

file sealed record OrgRoleRepresentation(string? Name);

public sealed partial class KeycloakOrganizationService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Creating organization {Name} in Keycloak")]
    private partial void LogCreatingOrganization(string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Organization {Name} created with ID {OrgId}")]
    private partial void LogOrganizationCreated(string name, Guid orgId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Organization {OrgId} not found")]
    private partial void LogOrganizationNotFound(Guid orgId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get organization {OrgId} from Keycloak")]
    private partial void LogGetOrganizationFailed(Exception ex, Guid orgId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get organizations from Keycloak")]
    private partial void LogGetOrganizationsFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Adding user {UserId} to organization {OrgId}")]
    private partial void LogAddingMember(Guid userId, Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} added to organization {OrgId}")]
    private partial void LogMemberAdded(Guid userId, Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removing user {UserId} from organization {OrgId}")]
    private partial void LogRemovingMember(Guid userId, Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} removed from organization {OrgId}")]
    private partial void LogMemberRemoved(Guid userId, Guid orgId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get members for organization {OrgId}")]
    private partial void LogGetMembersFailed(Exception ex, Guid orgId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get organizations for user {UserId}")]
    private partial void LogGetUserOrganizationsFailed(Exception ex, Guid userId);
}
