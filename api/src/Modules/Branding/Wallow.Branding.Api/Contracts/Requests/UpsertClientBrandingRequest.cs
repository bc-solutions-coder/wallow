namespace Wallow.Branding.Api.Contracts.Requests;

public sealed record UpsertClientBrandingRequest(
    string DisplayName,
    string? Tagline,
    string? ThemeJson);
