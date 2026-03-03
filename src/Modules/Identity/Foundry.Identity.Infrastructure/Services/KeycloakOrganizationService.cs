using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Contracts.Identity.Events;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Foundry.Identity.Infrastructure.Services;

public sealed partial class KeycloakOrganizationService : IKeycloakOrganizationService
{
    private readonly HttpClient _httpClient;
    private readonly IMessageBus _messageBus;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<KeycloakOrganizationService> _logger;
    private const string Realm = "foundry";

    public KeycloakOrganizationService(
        IHttpClientFactory httpClientFactory,
        IMessageBus messageBus,
        ITenantContext tenantContext,
        ILogger<KeycloakOrganizationService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("KeycloakAdminClient");
        _messageBus = messageBus;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Guid> CreateOrganizationAsync(string name, string? domain = null, CancellationToken ct = default)
    {
        LogCreatingOrganization(name);

        CreateOrganizationRequest createRequest = new()
        {
            Name = name,
            Domains = string.IsNullOrWhiteSpace(domain) ? null : new[] { new OrgDomain { Name = domain } }
        };

        HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"/admin/realms/{Realm}/organizations", createRequest, ct);
        response.EnsureSuccessStatusCode();

        string? locationHeader = response.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(locationHeader))
        {
            throw new InvalidOperationException("Organization created but Location header is missing");
        }

        Guid orgId = Guid.Parse(locationHeader.Split('/').Last());

        await _messageBus.PublishAsync(new OrganizationCreatedEvent
        {
            OrganizationId = orgId,
            TenantId = _tenantContext.TenantId.Value,
            Name = name,
            Domain = domain
        });

        LogOrganizationCreated(name, orgId);

        return orgId;
    }

    public async Task<OrganizationDto?> GetOrganizationByIdAsync(Guid orgId, CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{Realm}/organizations/{orgId}", ct);
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
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{Realm}/organizations?{queryString}", ct);
            response.EnsureSuccessStatusCode();

            List<OrgRepresentation>? orgs = await response.Content.ReadFromJsonAsync<List<OrgRepresentation>>(ct);
            if (orgs == null || orgs.Count == 0)
            {
                return Array.Empty<OrganizationDto>();
            }

            List<OrganizationDto> orgDtos = [];
            foreach (OrgRepresentation org in orgs)
            {
                if (string.IsNullOrWhiteSpace(org.Id))
                {
                    continue;
                }

                Guid orgId = Guid.Parse(org.Id);
                int membersCount = await GetMembersCountAsync(orgId, ct);

                orgDtos.Add(new OrganizationDto(
                    orgId,
                    org.Name ?? string.Empty,
                    org.Domains?.FirstOrDefault()?.Name,
                    membersCount));
            }

            return orgDtos;
        }
        catch (Exception ex)
        {
            LogGetOrganizationsFailed(ex);
            return Array.Empty<OrganizationDto>();
        }
    }

    public async Task AddMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default)
    {
        LogAddingMember(userId, orgId);

        var addMemberRequest = new { id = userId.ToString() };

        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            $"/admin/realms/{Realm}/organizations/{orgId}/members",
            addMemberRequest,
            ct);
        response.EnsureSuccessStatusCode();

        string userEmail = await GetUserEmailAsync(userId, ct);

        await _messageBus.PublishAsync(new OrganizationMemberAddedEvent
        {
            OrganizationId = orgId,
            TenantId = _tenantContext.TenantId.Value,
            UserId = userId,
            Email = userEmail
        });

        LogMemberAdded(userId, orgId);
    }

    public async Task RemoveMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default)
    {
        LogRemovingMember(userId, orgId);

        HttpResponseMessage response = await _httpClient.DeleteAsync(
            $"/admin/realms/{Realm}/organizations/{orgId}/members/{userId}",
            ct);
        response.EnsureSuccessStatusCode();

        LogMemberRemoved(userId, orgId);
    }

    public async Task<IReadOnlyList<UserDto>> GetMembersAsync(Guid orgId, CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{Realm}/organizations/{orgId}/members", ct);
            response.EnsureSuccessStatusCode();

            List<OrgUserRepresentation>? users = await response.Content.ReadFromJsonAsync<List<OrgUserRepresentation>>(ct);
            if (users == null || users.Count == 0)
            {
                return Array.Empty<UserDto>();
            }

            List<UserDto> userDtos = [];
            foreach (OrgUserRepresentation user in users)
            {
                if (string.IsNullOrWhiteSpace(user.Id))
                {
                    continue;
                }

                Guid userId = Guid.Parse(user.Id);
                IReadOnlyList<string> roles = await GetUserRolesAsync(userId, ct);

                userDtos.Add(new UserDto(
                    userId,
                    user.Email ?? string.Empty,
                    user.FirstName ?? string.Empty,
                    user.LastName ?? string.Empty,
                    user.Enabled ?? false,
                    roles));
            }

            return userDtos;
        }
        catch (Exception ex)
        {
            LogGetMembersFailed(ex, orgId);
            return Array.Empty<UserDto>();
        }
    }

    public async Task<IReadOnlyList<OrganizationDto>> GetUserOrganizationsAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{Realm}/users/{userId}/organizations", ct);
            response.EnsureSuccessStatusCode();

            List<OrgRepresentation>? orgs = await response.Content.ReadFromJsonAsync<List<OrgRepresentation>>(ct);
            if (orgs == null || orgs.Count == 0)
            {
                return Array.Empty<OrganizationDto>();
            }

            List<OrganizationDto> orgDtos = [];
            foreach (OrgRepresentation org in orgs)
            {
                if (string.IsNullOrWhiteSpace(org.Id))
                {
                    continue;
                }

                Guid orgId = Guid.Parse(org.Id);
                int membersCount = await GetMembersCountAsync(orgId, ct);

                orgDtos.Add(new OrganizationDto(
                    orgId,
                    org.Name ?? string.Empty,
                    org.Domains?.FirstOrDefault()?.Name,
                    membersCount));
            }

            return orgDtos;
        }
        catch (Exception ex)
        {
            LogGetUserOrganizationsFailed(ex, userId);
            return Array.Empty<OrganizationDto>();
        }
    }

    private async Task<int> GetMembersCountAsync(Guid orgId, CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{Realm}/organizations/{orgId}/members", ct);
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
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{Realm}/users/{userId}", ct);
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
            HttpResponseMessage response = await _httpClient.GetAsync($"/admin/realms/{Realm}/users/{userId}/role-mappings/realm", ct);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<string>();
            }

            List<OrgRoleRepresentation>? roles = await response.Content.ReadFromJsonAsync<List<OrgRoleRepresentation>>(ct);
            if (roles == null)
            {
                return Array.Empty<string>();
            }

            return roles.Select(r => r.Name ?? string.Empty).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

}

file sealed record CreateOrganizationRequest
{
    public required string Name { get; init; }
    public OrgDomain[]? Domains { get; init; }
}

file sealed record OrgDomain
{
    public required string Name { get; init; }
}

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

file sealed record OrgRoleRepresentation
{
    public string? Name { get; init; }
}

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
