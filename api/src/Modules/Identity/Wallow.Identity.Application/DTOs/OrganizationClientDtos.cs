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
    bool BackchannelLogoutSessionRequired,
    IReadOnlyList<string> Scopes,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    Guid? LastRotatedByUserId,
    DateTimeOffset? LastRotatedAt,
    DateTimeOffset? PlatformSuspendedAt = null,
    string? PlatformSuspensionReason = null,
    int? RefreshTokenLifetime = null);

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
/// back-channel logout URI, the scopes it may request and the optional refresh-token lifetime
/// in seconds (<see langword="null"/> keeps the client's current policy — at registration that
/// means the third-party default). Name and client id live outside it because they are fixed at
/// registration. A service account carries only the scopes; its URI lists are always empty
/// because it never takes part in a browser flow.
/// </summary>
public sealed record ClientConfigurationInput(
    IReadOnlyList<Uri> RedirectUris,
    IReadOnlyList<Uri> PostLogoutRedirectUris,
    Uri? BackchannelLogoutUri,
    IReadOnlyList<string> Scopes,
    bool BackchannelLogoutSessionRequired = false,
    int? RefreshTokenLifetime = null);

/// <summary>
/// Who is performing a client operation and from where — carried into the integration event the
/// operation publishes, which is what writes the audit row. The address is <see langword="null"/>
/// when the surface knows none.
/// </summary>
public sealed record ClientActorContext(Guid ActorId, string? IpAddress);

/// <summary>
/// A registration request as the surface has already validated it: which kind of client, its
/// immutable name, the configuration that kind accepts and — for an application — the optional
/// initial branding. When <c>BrandingDisplayName</c> is null the application's end-user-facing
/// display name defaults to <c>Name</c>.
/// </summary>
public sealed record RegisterClientInput(
    RegisteredClientKind Kind,
    string Name,
    ClientConfigurationInput Configuration,
    string? BrandingDisplayName = null,
    string? BrandingTagline = null);
