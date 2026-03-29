using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class OpenIddictDeveloperAppService(
    IOpenIddictApplicationManager applicationManager,
    ILogger<OpenIddictDeveloperAppService> logger) : IDeveloperAppService
{
    public async Task<DeveloperAppRegistrationResult> RegisterClientAsync(
        string clientId,
        string clientName,
        IReadOnlyCollection<string> requestedScopes,
        string? clientType = null,
        IReadOnlyCollection<string>? redirectUris = null,
        string? creatorUserId = null,
        CancellationToken cancellationToken = default)
    {
        LogRegisteringClient(clientId);

        string clientSecret = GenerateClientSecret();

        string resolvedClientType = string.Equals(clientType, ClientTypes.Public, StringComparison.OrdinalIgnoreCase)
            ? ClientTypes.Public
            : ClientTypes.Confidential;

        List<string> permissions =
        [
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.ClientCredentials
        ];

        // Public clients with redirect URIs use authorization code flow
        if (redirectUris is { Count: > 0 })
        {
            permissions.Add(Permissions.Endpoints.Authorization);
            permissions.Add(Permissions.GrantTypes.AuthorizationCode);
            permissions.Add(Permissions.ResponseTypes.Code);
        }

        foreach (string scope in requestedScopes)
        {
            permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = clientId,
            ClientSecret = resolvedClientType == ClientTypes.Confidential ? clientSecret : null,
            DisplayName = clientName,
            ClientType = resolvedClientType,
        };

        foreach (string permission in permissions)
        {
            descriptor.Permissions.Add(permission);
        }

        if (redirectUris is { Count: > 0 })
        {
            foreach (string uri in redirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(uri));
            }
        }

        if (!string.IsNullOrEmpty(creatorUserId))
        {
            descriptor.Properties[nameof(creatorUserId)] = JsonSerializer.SerializeToElement(creatorUserId);
        }

        await applicationManager.CreateAsync(descriptor, cancellationToken);

        LogClientRegistered(clientId);

        return new DeveloperAppRegistrationResult(
            clientId,
            clientSecret,
            RegistrationAccessToken: clientSecret);
    }

    public async Task<IReadOnlyList<DeveloperAppInfo>> GetUserAppsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        List<DeveloperAppInfo> results = [];

        await foreach (object application in applicationManager.ListAsync(int.MaxValue, 0, cancellationToken))
        {
            DeveloperAppInfo? info = await TryBuildAppInfoForUser(application, userId, cancellationToken);
            if (info is not null)
            {
                results.Add(info);
            }
        }

        return results;
    }

    public async Task<DeveloperAppInfo?> GetUserAppAsync(
        string userId,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        object? application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        return await TryBuildAppInfoForUser(application, userId, cancellationToken);
    }

    private async Task<DeveloperAppInfo?> TryBuildAppInfoForUser(
        object application,
        string userId,
        CancellationToken cancellationToken)
    {
        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, cancellationToken);

        if (!descriptor.Properties.TryGetValue("creatorUserId", out JsonElement creatorElement))
        {
            return null;
        }

        string? creatorUserId = creatorElement.Deserialize<string>();
        if (!string.Equals(creatorUserId, userId, StringComparison.Ordinal))
        {
            return null;
        }

        string? clientId = await applicationManager.GetClientIdAsync(application, cancellationToken);
        List<string> redirectUris = descriptor.RedirectUris.Select(u => u.ToString()).ToList();

        return new DeveloperAppInfo(
            clientId ?? string.Empty,
            descriptor.DisplayName ?? string.Empty,
            descriptor.ClientType ?? string.Empty,
            redirectUris,
            CreatedAt: null);
    }

    private static string GenerateClientSecret()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Registering developer app client {ClientId} via OpenIddict")]
    private partial void LogRegisteringClient(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Developer app client {ClientId} registered successfully")]
    private partial void LogClientRegistered(string clientId);
}
