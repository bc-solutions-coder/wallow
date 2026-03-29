using OpenIddict.Abstractions;
using Wallow.Identity.Application.Commands.RegisterSetupClient;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class SetupClientService(
    IOpenIddictApplicationManager applicationManager) : ISetupClientService
{
    public async Task<bool> ClientExistsAsync(string clientId, CancellationToken ct = default)
    {
        object? application = await applicationManager.FindByClientIdAsync(clientId, ct);
        return application is not null;
    }

    public async Task<string> CreateConfidentialClientAsync(
        string clientId,
        string clientSecret,
        IReadOnlyList<string> redirectUris,
        CancellationToken ct = default)
    {
        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            DisplayName = clientId
        };

        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Scopes.Email);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Scopes.Profile);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Scopes.Roles);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access");

        foreach (string redirectUri in redirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(redirectUri));
            descriptor.PostLogoutRedirectUris.Add(new Uri(redirectUri.TrimEnd('/') + "/signout-callback"));
        }

        await applicationManager.CreateAsync(descriptor, ct);
        return clientSecret;
    }
}
