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
/// Registration or rotation result. The secret is disclosed here and cannot be read back.
/// The API controller substitutes the request origin for a missing issuer or API URL.
/// </summary>
public sealed record OrganizationClientRegistrationResult(
    OrganizationClientDto Client,
    string ClientSecret,
    string? Issuer,
    string? ApiBaseUrl);

/// <summary>
/// Mutable OAuth client configuration. A null refresh-token lifetime preserves current
/// policy on update and selects the third-party default on registration. Service-account
/// callers must supply no browser-flow URIs. Name and client ID are fixed at registration.
/// </summary>
public sealed record ClientConfigurationInput(
    IReadOnlyList<Uri> RedirectUris,
    IReadOnlyList<Uri> PostLogoutRedirectUris,
    Uri? BackchannelLogoutUri,
    IReadOnlyList<string> Scopes,
    bool BackchannelLogoutSessionRequired = false,
    int? RefreshTokenLifetime = null);

/// <summary>
/// Actor and optional IP address carried into client-operation audit events.
/// </summary>
public sealed record ClientActorContext(Guid ActorId, string? IpAddress);

/// <summary>
/// Validated client registration input. A null BrandingDisplayName defaults to Name.
/// </summary>
public sealed record RegisterClientInput(
    RegisteredClientKind Kind,
    string Name,
    ClientConfigurationInput Configuration,
    string? BrandingDisplayName = null,
    string? BrandingTagline = null);
