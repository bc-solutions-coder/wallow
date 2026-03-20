using System.Security.Cryptography;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
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
        CancellationToken cancellationToken = default)
    {
        LogRegisteringClient(clientId);

        string clientSecret = GenerateClientSecret();

        List<string> permissions =
        [
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.ClientCredentials
        ];

        foreach (string scope in requestedScopes)
        {
            permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = clientName,
            ClientType = ClientTypes.Confidential,
        };

        foreach (string permission in permissions)
        {
            descriptor.Permissions.Add(permission);
        }

        await applicationManager.CreateAsync(descriptor, cancellationToken);

        LogClientRegistered(clientId);

        // RegistrationAccessToken is a Keycloak DCR concept; not applicable for OpenIddict.
        // Return the client secret as the access token for backward compatibility with consumers.
        return new DeveloperAppRegistrationResult(
            clientId,
            clientSecret,
            RegistrationAccessToken: clientSecret);
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
