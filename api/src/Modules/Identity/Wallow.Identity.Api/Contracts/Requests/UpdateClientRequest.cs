namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Replaces name and URI fields. Omitted logout URLs disable those channels.
/// Null RefreshTokenLifetime preserves the current lifetime; seconds apply to future tokens.
/// </summary>
public record UpdateClientRequest(
    string Name,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    string? FrontchannelLogoutUri = null,
    string? BackchannelLogoutUri = null,
    bool BackchannelLogoutSessionRequired = false,
    int? RefreshTokenLifetime = null);
