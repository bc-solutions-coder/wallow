namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// A full replacement of the client's mutable registration — omitting FrontchannelLogoutUri or
/// BackchannelLogoutUri un-registers the client from that logout channel, matching how the URI
/// lists replace rather than merge. RefreshTokenLifetime is the one deliberate exception: a
/// <see langword="null"/> keeps the client's current lifetime, because silently resetting a
/// security policy on an unrelated edit is a trap. A value (seconds) applies to newly issued
/// refresh tokens only.
/// </summary>
public record UpdateClientRequest(
    string Name,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    string? FrontchannelLogoutUri = null,
    string? BackchannelLogoutUri = null,
    bool BackchannelLogoutSessionRequired = false,
    int? RefreshTokenLifetime = null);
