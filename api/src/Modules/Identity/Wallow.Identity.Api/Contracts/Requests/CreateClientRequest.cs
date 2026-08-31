namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Scopes is what the client may ever request at the authorize endpoint. Omitting it grants the
/// OIDC sign-in baseline; API scopes are opt-in. FrontchannelLogoutUri is the absolute http(s)
/// address the logout page notifies (in a hidden iframe, with iss + sid) when the SSO session
/// ends; omitting it opts the client out of logout notifications. RefreshTokenLifetime is
/// seconds; unset, the client gets the third-party default of one day. It bounds new refresh
/// tokens only.
/// </summary>
public record CreateClientRequest(
    string Name,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string>? Scopes = null,
    string? FrontchannelLogoutUri = null,
    int? RefreshTokenLifetime = null);
