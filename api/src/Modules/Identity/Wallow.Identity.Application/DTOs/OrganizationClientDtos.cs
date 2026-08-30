using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Application.DTOs;

/// <summary>A client as its owning organization sees it: OAuth configuration plus Wallow's record.</summary>
public sealed record OrganizationClientDto(
    string ClientId,
    string Name,
    RegisteredClientKind Kind,
    RegisteredClientStatus Status,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    string? BackchannelLogoutUri,
    IReadOnlyList<string> Scopes,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    Guid? LastRotatedByUserId,
    DateTimeOffset? LastRotatedAt);

/// <summary>
/// What a client is given once, at registration and again at rotation: the client secret is
/// never readable again, and the issuer and API base URL are what its environment has to point at.
/// Either URL is <see langword="null"/> when the deployment configures none, in which case the
/// caller substitutes the request origin, as OpenIddict itself does for the issuer.
/// </summary>
public sealed record OrganizationClientRegistrationResult(
    OrganizationClientDto Client,
    string ClientSecret,
    string? Issuer,
    string? ApiBaseUrl);

/// <summary>
/// Everything about a client its organization may set: the redirect URIs, the optional
/// back-channel logout URI and the scopes it may request. Name and client id live outside it
/// because they are fixed at registration. A service account carries only the scopes; its URI
/// lists are always empty because it never takes part in a browser flow.
/// </summary>
public sealed record ClientConfigurationInput(
    IReadOnlyList<Uri> RedirectUris,
    IReadOnlyList<Uri> PostLogoutRedirectUris,
    Uri? BackchannelLogoutUri,
    IReadOnlyList<string> Scopes);

/// <summary>
/// A registration request as the surface has already validated it: which kind of client, its
/// display name and the configuration that kind accepts.
/// </summary>
public sealed record RegisterClientInput(RegisteredClientKind Kind, string Name, ClientConfigurationInput Configuration);
