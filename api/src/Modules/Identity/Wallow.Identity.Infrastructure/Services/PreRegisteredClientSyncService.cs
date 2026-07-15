using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Identity.Infrastructure.Options;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class PreRegisteredClientSyncService(
    IOpenIddictApplicationManager applicationManager,
    IOrganizationService organizationService,
    UserManager<WallowUser> userManager,
    IOptions<PreRegisteredClientOptions> options,
    ILogger<PreRegisteredClientSyncService> logger)
{
    private const string SourcePropertyKey = "source";
    private const string SourcePropertyValue = "config";

    public async Task SyncAsync(CancellationToken ct)
    {
        PreRegisteredClientOptions config = options.Value;
        HashSet<string> configuredClientIds = new(config.Clients.Select(c => c.ClientId), StringComparer.OrdinalIgnoreCase);

        foreach (PreRegisteredClientDefinition client in config.Clients)
        {
            await CreateOrUpdateClientAsync(client, ct);
        }

        await DeleteRemovedClientsAsync(configuredClientIds, ct);
    }

    private async Task CreateOrUpdateClientAsync(PreRegisteredClientDefinition client, CancellationToken ct)
    {
        object? existing = await applicationManager.FindByClientIdAsync(client.ClientId, ct);

        if (existing is not null)
        {
            await UpdateClientAsync(existing, client, ct);
        }
        else
        {
            await CreateClientAsync(client, ct);
        }
    }

    private async Task CreateClientAsync(PreRegisteredClientDefinition client, CancellationToken ct)
    {
        OpenIddictApplicationDescriptor descriptor = await BuildDescriptorAsync(client, ct);

        await applicationManager.CreateAsync(descriptor, ct);
        LogClientCreated(client.ClientId);
    }

    private async Task UpdateClientAsync(object existing, PreRegisteredClientDefinition client, CancellationToken ct)
    {
        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, existing, ct);

        bool changed = false;

        if (!string.Equals(descriptor.DisplayName, client.DisplayName, StringComparison.Ordinal))
        {
            descriptor.DisplayName = client.DisplayName;
            changed = true;
        }

        string expectedType = client.IsPublic ? ClientTypes.Public : ClientTypes.Confidential;
        if (!string.Equals(descriptor.ClientType, expectedType, StringComparison.OrdinalIgnoreCase))
        {
            descriptor.ClientType = expectedType;
            if (!client.IsPublic)
            {
                descriptor.ClientSecret = client.Secret;
            }
            changed = true;
        }

        HashSet<Uri> expectedRedirectUris = new(client.RedirectUris.Select(u => new Uri(u)));
        if (!descriptor.RedirectUris.SetEquals(expectedRedirectUris))
        {
            descriptor.RedirectUris.Clear();
            foreach (string uri in client.RedirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(uri));
            }
            changed = true;
        }

        HashSet<Uri> expectedPostLogoutUris = new(client.PostLogoutRedirectUris.Select(u => new Uri(u)));
        if (!descriptor.PostLogoutRedirectUris.SetEquals(expectedPostLogoutUris))
        {
            descriptor.PostLogoutRedirectUris.Clear();
            foreach (string uri in client.PostLogoutRedirectUris)
            {
                descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
            }
            changed = true;
        }

        HashSet<string> expectedScopePermissions = new(client.Scopes.Select(s => Permissions.Prefixes.Scope + s));
        HashSet<string> currentScopePermissions = new(
            descriptor.Permissions.Where(p => p.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal)));

        if (!currentScopePermissions.SetEquals(expectedScopePermissions))
        {
            descriptor.Permissions.RemoveWhere(p => p.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal));
            foreach (string scope in client.Scopes)
            {
                descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
            }
            changed = true;
        }

        // Ensure source tag is set
        if (!descriptor.Properties.ContainsKey(SourcePropertyKey))
        {
            descriptor.Properties[SourcePropertyKey] = JsonSerializer.SerializeToElement(SourcePropertyValue);
            changed = true;
        }

        // Sync tenant_id
        Guid? resolvedTenantId = await ResolveTenantIdAsync(client, ct);
        string? currentTenantId = descriptor.GetTenantId();
        string? expectedTenantId = resolvedTenantId?.ToString();
        if (!string.Equals(currentTenantId, expectedTenantId, StringComparison.OrdinalIgnoreCase))
        {
            if (resolvedTenantId.HasValue)
            {
                descriptor.SetTenantId(resolvedTenantId.Value.ToString());
            }
            changed = true;
        }

        if (changed)
        {
            await applicationManager.UpdateAsync(existing, descriptor, ct);
            LogClientUpdated(client.ClientId);
        }
    }

    private async Task DeleteRemovedClientsAsync(HashSet<string> configuredClientIds, CancellationToken ct)
    {
        await foreach (object application in applicationManager.ListAsync(int.MaxValue, 0, ct))
        {
            OpenIddictApplicationDescriptor descriptor = new();
            await applicationManager.PopulateAsync(descriptor, application, ct);

            if (!descriptor.Properties.TryGetValue(SourcePropertyKey, out JsonElement sourceElement))
            {
                continue;
            }

            string? source = sourceElement.GetString();
            if (!string.Equals(source, SourcePropertyValue, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? clientId = await applicationManager.GetClientIdAsync(application, ct);
            if (clientId is not null && !configuredClientIds.Contains(clientId))
            {
                await applicationManager.DeleteAsync(application, ct);
                LogClientDeleted(clientId);
            }
        }
    }

    private async Task<Guid?> ResolveTenantIdAsync(PreRegisteredClientDefinition client, CancellationToken ct)
    {
        if (client.TenantId.HasValue && client.TenantId.Value != Guid.Empty)
        {
            await EnsureSeedMembersAsync(client.TenantId.Value, client, ct);
            return client.TenantId.Value;
        }

        if (!string.IsNullOrWhiteSpace(client.TenantName))
        {
            IReadOnlyList<OrganizationDto> orgs = await organizationService.GetOrganizationsAsync(client.TenantName, ct: ct);
            OrganizationDto? match = orgs.FirstOrDefault(o => string.Equals(o.Name, client.TenantName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                await EnsureSeedMembersAsync(match.Id, client, ct);
                return match.Id;
            }

            // Auto-create the organization if it doesn't exist
            Guid orgId = await organizationService.CreateOrganizationAsync(client.TenantName, ct: ct);
            LogTenantCreated(client.ClientId, client.TenantName);
            await EnsureSeedMembersAsync(orgId, client, ct);
            return orgId;
        }

        return null;
    }

    private async Task EnsureSeedMembersAsync(Guid orgId, PreRegisteredClientDefinition client, CancellationToken ct)
    {
        if (client.SeedMembers.Count == 0)
        {
            return;
        }

        IReadOnlyList<UserDto> existingMembers = await organizationService.GetMembersAsync(orgId, ct);
        HashSet<Guid> memberIds = new(existingMembers.Select(m => m.Id));

        foreach (string email in client.SeedMembers)
        {
            WallowUser? user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                LogSeedMemberNotFound(client.ClientId, email);
                continue;
            }

            if (memberIds.Contains(user.Id))
            {
                continue;
            }

            await organizationService.AddMemberAsync(orgId, user.Id, ct);
            LogSeedMemberAdded(client.ClientId, email);
        }
    }

    private static bool IsServiceAccount(string clientId)
        => clientId.StartsWith("sa-", StringComparison.Ordinal);

    private async Task<OpenIddictApplicationDescriptor> BuildDescriptorAsync(PreRegisteredClientDefinition client, CancellationToken ct)
    {
        string clientType = client.IsPublic ? ClientTypes.Public : ClientTypes.Confidential;
        bool isServiceAccount = IsServiceAccount(client.ClientId);

        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = client.ClientId,
            ClientSecret = client.IsPublic ? null : client.Secret,
            DisplayName = client.DisplayName,
            ClientType = clientType,
            Properties =
            {
                [SourcePropertyKey] = JsonSerializer.SerializeToElement(SourcePropertyValue)
            }
        };

        if (isServiceAccount)
        {
            descriptor.Permissions.Add(Permissions.Endpoints.Token);
            descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        }
        else
        {
            descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(Permissions.Endpoints.EndSession);
            descriptor.Permissions.Add(Permissions.Endpoints.Token);
            descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
            descriptor.Permissions.Add(Permissions.ResponseTypes.Code);
            descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        }

        foreach (string uri in client.RedirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(uri));
        }

        foreach (string uri in client.PostLogoutRedirectUris)
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
        }

        foreach (string scope in client.Scopes)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        Guid? tenantId = await ResolveTenantIdAsync(client, ct);
        if (tenantId.HasValue)
        {
            descriptor.SetTenantId(tenantId.Value.ToString());
        }

        return descriptor;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created pre-registered client: {ClientId}")]
    private partial void LogClientCreated(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated pre-registered client: {ClientId}")]
    private partial void LogClientUpdated(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted pre-registered client no longer in config: {ClientId}")]
    private partial void LogClientDeleted(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Auto-created organization '{TenantName}' for pre-registered client: {ClientId}")]
    private partial void LogTenantCreated(string clientId, string tenantName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Added seed member '{Email}' to organization for client: {ClientId}")]
    private partial void LogSeedMemberAdded(string clientId, string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seed member '{Email}' not found for client: {ClientId}")]
    private partial void LogSeedMemberNotFound(string clientId, string email);
}
