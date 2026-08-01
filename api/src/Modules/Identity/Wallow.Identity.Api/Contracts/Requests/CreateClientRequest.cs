namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Scopes is what the client may ever request at the authorize endpoint. Omitting it grants the
/// OIDC sign-in baseline; API scopes are opt-in.
/// </summary>
public record CreateClientRequest(
    string Name,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    Guid? TenantId = null,
    IReadOnlyList<string>? Scopes = null);
