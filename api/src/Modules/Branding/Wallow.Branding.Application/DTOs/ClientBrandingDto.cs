namespace Wallow.Branding.Application.DTOs;

public sealed record ClientBrandingDto(
    string ClientId,
    string DisplayName,
    string? Tagline,
    string? LogoUrl,
    string? ThemeJson);
