namespace Wallow.Identity.Application.DTOs;

/// <summary>
/// Everything the auth host needs to dress an authorize transaction's screens: the client's
/// public branding (display name always resolved — branding row, then OpenIddict display name,
/// then the client id), the organization it belongs to, whether it is first-party, and the
/// requested scopes with their descriptions for the consent screen.
/// </summary>
public sealed record AuthorizeContextDto(
    string ClientId,
    string DisplayName,
    string? Tagline,
    string? LogoUrl,
    string? ThemeJson,
    string? OrganizationName,
    bool FirstParty,
    IReadOnlyList<ConsentScopeDto> Scopes);

public sealed record ConsentScopeDto(
    string Name,
    string? Description);
