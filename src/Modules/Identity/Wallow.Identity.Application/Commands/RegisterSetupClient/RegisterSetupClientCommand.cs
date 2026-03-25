namespace Wallow.Identity.Application.Commands.RegisterSetupClient;

public sealed record RegisterSetupClientCommand(
    string ClientId,
    IReadOnlyList<string> RedirectUris);

public sealed record RegisterSetupClientResult(string ClientSecret);

/// <summary>
/// Handles OpenIddict application creation for setup clients.
/// Implemented in Infrastructure using IOpenIddictApplicationManager.
/// </summary>
public interface ISetupClientService
{
    Task<bool> ClientExistsAsync(string clientId, CancellationToken ct = default);

    Task<string> CreateConfidentialClientAsync(
        string clientId,
        string clientSecret,
        IReadOnlyList<string> redirectUris,
        CancellationToken ct = default);
}
