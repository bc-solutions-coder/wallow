namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Scopes is what the client may ever request at the authorize endpoint. Omitting it grants the
/// OIDC sign-in baseline; API scopes are opt-in. FrontchannelLogoutUri is the absolute http(s)
/// address the logout page notifies (in a hidden iframe, with iss + sid) when the SSO session
/// ends; omitting it opts the client out of logout notifications. BackchannelLogoutUri is the
/// absolute address the server POSTs a signed logout token to over the back channel — plain http
/// is acceptable here because admin-registered clients are confidential.
/// BackchannelLogoutSessionRequired echoes the OIDC registration flag; Wallow always includes
/// <c>sid</c> either way. RefreshTokenLifetime is seconds; unset, the client gets the third-party
/// default of one day. It bounds new refresh tokens only.
/// </summary>
public record CreateClientRequest(
    string Name,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string>? Scopes = null,
    string? FrontchannelLogoutUri = null,
    string? BackchannelLogoutUri = null,
    bool BackchannelLogoutSessionRequired = false,
    int? RefreshTokenLifetime = null);
