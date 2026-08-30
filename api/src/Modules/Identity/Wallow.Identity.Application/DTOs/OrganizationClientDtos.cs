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
    DateTimeOffset? LastUsedAt);

/// <summary>
/// What a developer application is given once, at registration: the client secret is never
/// readable again, and the issuer and API base URL are what its environment has to point at.
/// Either URL is <see langword="null"/> when the deployment configures none, in which case the
/// caller substitutes the request origin, as OpenIddict itself does for the issuer.
/// </summary>
public sealed record OrganizationClientRegistrationResult(
    OrganizationClientDto Client,
    string ClientSecret,
    string? Issuer,
    string? ApiBaseUrl);

public sealed record RegisterApplicationInput(
    string Name,
    IReadOnlyList<Uri> RedirectUris,
    IReadOnlyList<Uri> PostLogoutRedirectUris,
    Uri? BackchannelLogoutUri,
    IReadOnlyList<string> Scopes);

public sealed record UpdateOrganizationClientInput(
    IReadOnlyList<Uri> RedirectUris,
    IReadOnlyList<Uri> PostLogoutRedirectUris,
    Uri? BackchannelLogoutUri,
    IReadOnlyList<string> Scopes);
