namespace Wallow.Identity.Api.Contracts.Responses;

public record OrganizationBrandingResponse(
    string? DisplayName,
    string? LogoUrl,
    string? PrimaryColor,
    string? AccentColor);
