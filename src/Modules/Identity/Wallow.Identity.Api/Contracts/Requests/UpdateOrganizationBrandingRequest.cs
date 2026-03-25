namespace Wallow.Identity.Api.Contracts.Requests;

public record UpdateOrganizationBrandingRequest(
    string? DisplayName,
    string? LogoUrl,
    string? PrimaryColor);
