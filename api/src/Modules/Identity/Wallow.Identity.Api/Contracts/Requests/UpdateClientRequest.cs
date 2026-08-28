namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// A full replacement of the client's mutable registration — omitting FrontchannelLogoutUri
/// un-registers the client from front-channel logout notifications, matching how the URI lists
/// replace rather than merge.
/// </summary>
public record UpdateClientRequest(
    string Name,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    string? FrontchannelLogoutUri = null);
