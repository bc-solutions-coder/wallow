using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Wallow.Identity.Infrastructure.Options;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class PreRegisteredClientSyncService(
    IOpenIddictApplicationManager applicationManager,
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
        OpenIddictApplicationDescriptor descriptor = BuildDescriptor(client);

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

    private static OpenIddictApplicationDescriptor BuildDescriptor(PreRegisteredClientDefinition client)
    {
        string clientType = client.IsPublic ? ClientTypes.Public : ClientTypes.Confidential;

        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = client.ClientId,
            ClientSecret = client.IsPublic ? null : client.Secret,
            DisplayName = client.DisplayName,
            ClientType = clientType,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.EndSession,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange
            },
            Properties =
            {
                [SourcePropertyKey] = JsonSerializer.SerializeToElement(SourcePropertyValue)
            }
        };

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

        return descriptor;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created pre-registered client: {ClientId}")]
    private partial void LogClientCreated(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated pre-registered client: {ClientId}")]
    private partial void LogClientUpdated(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted pre-registered client no longer in config: {ClientId}")]
    private partial void LogClientDeleted(string clientId);
}
