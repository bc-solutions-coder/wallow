using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Helpers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Identity.Infrastructure.Options;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class PreRegisteredClientSyncService(
    IOpenIddictApplicationManager applicationManager,
    IOrganizationService organizationService,
    IRegisteredClientRepository registeredClients,
    UserManager<WallowUser> userManager,
    IOptions<PreRegisteredClientOptions> options,
    TimeProvider timeProvider,
    ILogger<PreRegisteredClientSyncService> logger)
{
    private const string SourcePropertyKey = "source";
    private const string SourcePropertyValue = "config";
    private const string DefaultSeedMemberRole = "user";

    public async Task SyncAsync(CancellationToken ct)
    {
        PreRegisteredClientOptions config = options.Value;

        // Validate the complete configuration before mutating registrations.
        config.Validate();

        HashSet<string> configuredClientIds = new(config.Clients.Select(c => c.ClientId), StringComparer.OrdinalIgnoreCase);

        foreach (PreRegisteredClientDefinition client in config.Clients)
        {
            await CreateOrUpdateClientAsync(client, ct);
        }

        await DeleteRemovedClientsAsync(configuredClientIds, ct);
    }

    private async Task CreateOrUpdateClientAsync(PreRegisteredClientDefinition client, CancellationToken ct)
    {
        Guid? tenantId = await ResolveTenantIdAsync(client, ct);
        object? existing = await applicationManager.FindByClientIdAsync(client.ClientId, ct);

        if (existing is not null)
        {
            await UpdateClientAsync(existing, client, tenantId, ct);
        }
        else
        {
            await CreateClientAsync(client, tenantId, ct);
        }

        await SyncRegistryRowAsync(client, tenantId, ct);
    }

    private async Task CreateClientAsync(PreRegisteredClientDefinition client, Guid? tenantId, CancellationToken ct)
    {
        OpenIddictApplicationDescriptor descriptor = BuildDescriptor(client, tenantId);

        await applicationManager.CreateAsync(descriptor, ct);
        LogClientCreated(client.ClientId);
    }

    /// <summary>
    /// Syncs organization ownership in the <see cref="RegisteredClient"/> registry.
    /// Rows already owned by the configured organization retain their lifecycle state.
    /// </summary>
    private async Task SyncRegistryRowAsync(PreRegisteredClientDefinition client, Guid? tenantId, CancellationToken ct)
    {
        RegisteredClient? record = await registeredClients.GetByClientIdAsync(client.ClientId, ct);

        if (tenantId is null)
        {
            // Platform clients have no organization registry row.
            if (record is not null)
            {
                registeredClients.Remove(record);
                await registeredClients.SaveChangesAsync(ct);
                LogRegistryRowRemoved(client.ClientId);
            }

            return;
        }

        if (record is not null && record.OrganizationId == tenantId.Value)
        {
            return;
        }

        if (record is not null)
        {
            // Ownership changes replace the registry row and reset its lifecycle state to Active.
            registeredClients.Remove(record);
        }

        RegisteredClientKind kind = IsServiceAccount(client.ClientId)
            ? RegisteredClientKind.ServiceAccount
            : RegisteredClientKind.Application;

        // Seed operations have no user actor.
        RegisteredClient row = RegisteredClient.Create(
            client.ClientId, tenantId.Value, client.DisplayName, kind, Guid.Empty, timeProvider);
        registeredClients.Add(row);
        await registeredClients.SaveChangesAsync(ct);
        LogRegistryRowSynced(client.ClientId, tenantId.Value);
    }

    private async Task UpdateClientAsync(
        object existing,
        PreRegisteredClientDefinition client,
        Guid? resolvedTenantId,
        CancellationToken ct)
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


        if (!descriptor.Properties.ContainsKey(SourcePropertyKey))
        {
            descriptor.Properties[SourcePropertyKey] = JsonSerializer.SerializeToElement(SourcePropertyValue);
            changed = true;
        }

        // Apply changes to the configured consent policy.
        string expectedConsentType = ConsentTypeFor(client);
        if (!string.Equals(descriptor.ConsentType, expectedConsentType, StringComparison.Ordinal))
        {
            descriptor.ConsentType = expectedConsentType;
            changed = true;
        }

        // Pin the configured or default lifetime for future tokens; existing tokens retain theirs.
        if (RefreshTokenLifetimeFor(client) is { } expectedLifetime
            && descriptor.GetRefreshTokenLifetimeSeconds() != expectedLifetime)
        {
            descriptor.SetRefreshTokenLifetime(expectedLifetime);
            changed = true;
        }


        Uri? expectedFrontchannelUri = client.FrontchannelLogoutUri is null
            ? null
            : new Uri(client.FrontchannelLogoutUri);
        if (descriptor.GetFrontchannelLogoutUri() != expectedFrontchannelUri)
        {
            descriptor.SetFrontchannelLogoutUri(expectedFrontchannelUri);
            changed = true;
        }


        Uri? expectedBackchannelUri = client.BackchannelLogoutUri is null
            ? null
            : new Uri(client.BackchannelLogoutUri);
        if (descriptor.GetBackchannelLogoutUri() != expectedBackchannelUri)
        {
            descriptor.SetBackchannelLogoutUri(expectedBackchannelUri);
            changed = true;
        }

        if (descriptor.GetBackchannelLogoutSessionRequired() != client.BackchannelLogoutSessionRequired)
        {
            descriptor.SetBackchannelLogoutSessionRequired(client.BackchannelLogoutSessionRequired);
            changed = true;
        }


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
        // Finish streaming applications before deleting; the reader owns the database connection.
        List<(object Application, string ClientId)> removed = [];

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
                removed.Add((application, clientId));
            }
        }

        foreach ((object application, string clientId) in removed)
        {
            await applicationManager.DeleteAsync(application, ct);

            // Remove the management registry entry with its OpenIddict application.
            RegisteredClient? record = await registeredClients.GetByClientIdAsync(clientId, ct);
            if (record is not null)
            {
                registeredClients.Remove(record);
                await registeredClients.SaveChangesAsync(ct);
            }

            LogClientDeleted(clientId);
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

            string roleName = ResolveSeedMemberRole(client, email);
            // Seed operations have no user actor.
            await organizationService.AddMemberAsync(orgId, user.Id, roleName, Guid.Empty, ct);
            LogSeedMemberAdded(client.ClientId, email, roleName);
        }
    }

    private static string ResolveSeedMemberRole(PreRegisteredClientDefinition client, string email)
    {
        return client.SeedMemberRoles.TryGetValue(email, out string? configured)
            && !string.IsNullOrWhiteSpace(configured)
                ? configured
                : DefaultSeedMemberRole;
    }

    private static bool IsServiceAccount(string clientId)
        => clientId.StartsWith("sa-", StringComparison.Ordinal);

    /// <summary>
    /// Uses the explicit first-party flag to choose implicit or explicit consent.
    /// </summary>
    private static string ConsentTypeFor(PreRegisteredClientDefinition client)
        => client.FirstParty ? ConsentTypes.Implicit : ConsentTypes.Explicit;

    /// <summary>
    /// Returns the configured refresh-token lifetime or the client-kind default.
    /// Returns null for service accounts, for which sync does not write a lifetime.
    /// </summary>
    private static int? RefreshTokenLifetimeFor(PreRegisteredClientDefinition client)
        => IsServiceAccount(client.ClientId)
            ? null
            : client.RefreshTokenLifetime ?? (client.FirstParty
                ? ClientRefreshTokenLifetimes.FirstPartyDefaultSeconds
                : ClientRefreshTokenLifetimes.ThirdPartyDefaultSeconds);

    private static OpenIddictApplicationDescriptor BuildDescriptor(PreRegisteredClientDefinition client, Guid? tenantId)
    {
        string clientType = client.IsPublic ? ClientTypes.Public : ClientTypes.Confidential;
        bool isServiceAccount = IsServiceAccount(client.ClientId);

        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = client.ClientId,
            ClientSecret = client.IsPublic ? null : client.Secret,
            DisplayName = client.DisplayName,
            ClientType = clientType,
            ConsentType = ConsentTypeFor(client),
            Properties =
            {
                [SourcePropertyKey] = JsonSerializer.SerializeToElement(SourcePropertyValue)
            }
        };

        if (isServiceAccount)
        {
            descriptor.Permissions.Add(Permissions.Endpoints.Token);
            descriptor.Permissions.Add(Permissions.Endpoints.Revocation);
            descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        }
        else
        {
            descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(Permissions.Endpoints.EndSession);
            descriptor.Permissions.Add(Permissions.Endpoints.Token);
            descriptor.Permissions.Add(Permissions.Endpoints.Revocation);
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

        if (client.FrontchannelLogoutUri is not null)
        {
            descriptor.SetFrontchannelLogoutUri(new Uri(client.FrontchannelLogoutUri));
        }

        if (client.BackchannelLogoutUri is not null)
        {
            descriptor.SetBackchannelLogoutUri(new Uri(client.BackchannelLogoutUri));
        }

        descriptor.SetBackchannelLogoutSessionRequired(client.BackchannelLogoutSessionRequired);

        if (RefreshTokenLifetimeFor(client) is { } refreshTokenLifetime)
        {
            descriptor.SetRefreshTokenLifetime(refreshTokenLifetime);
        }

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Added seed member '{Email}' as '{RoleName}' to organization for client: {ClientId}")]
    private partial void LogSeedMemberAdded(string clientId, string email, string roleName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seed member '{Email}' not found for client: {ClientId}")]
    private partial void LogSeedMemberNotFound(string clientId, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered client row synced for {ClientId} under organization {OrganizationId}")]
    private partial void LogRegistryRowSynced(string clientId, Guid organizationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered client row removed for platform-owned client: {ClientId}")]
    private partial void LogRegistryRowRemoved(string clientId);
}
