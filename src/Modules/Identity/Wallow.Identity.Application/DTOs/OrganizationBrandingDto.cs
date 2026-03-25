namespace Wallow.Identity.Application.DTOs;

public record OrganizationBrandingDto(
    Guid OrganizationId,
    string? DisplayName,
    string? LogoUrl,
    string? PrimaryColor,
    string? AccentColor);
