namespace Wallow.Identity.Application.DTOs;

/// <summary>
/// Client branding, organization and requested scope descriptions for authorize screens.
/// DisplayName falls back from branding to the OpenIddict display name, then client ID.
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
