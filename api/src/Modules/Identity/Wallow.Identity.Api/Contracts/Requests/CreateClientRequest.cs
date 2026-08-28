namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Scopes is what the client may ever request at the authorize endpoint. Omitting it grants the
/// OIDC sign-in baseline; API scopes are opt-in. FrontchannelLogoutUri is the absolute http(s)
/// address the logout page notifies (in a hidden iframe, with iss + sid) when the SSO session
/// ends; omitting it opts the client out of logout notifications.
/// </summary>
public record CreateClientRequest(
    string Name,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    Guid? TenantId = null,
    IReadOnlyList<string>? Scopes = null,
    string? FrontchannelLogoutUri = null);
