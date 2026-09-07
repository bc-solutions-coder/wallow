namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Confidential client registration. Null or empty Scopes selects the OIDC sign-in baseline.
/// Omitted logout URLs disable their respective channels; back-channel HTTP is allowed.
/// BackchannelLogoutSessionRequired is stored metadata; logout tokens always carry sid.
/// RefreshTokenLifetime is seconds for future tokens and defaults to one day.
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
