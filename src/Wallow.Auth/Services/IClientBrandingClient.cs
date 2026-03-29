namespace Wallow.Auth.Services;

public sealed record ClientBrandingResponse(
    string ClientId,
    string DisplayName,
    string? Tagline,
    string? LogoUrl,
    string? ThemeJson);

public interface IClientBrandingClient
{
    Task<ClientBrandingResponse?> GetBrandingAsync(string clientId, CancellationToken ct = default);
}
