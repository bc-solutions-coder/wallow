namespace Wallow.Identity.Api.Contracts.Responses;

public record AuthorizeContextResponse(
    string ClientId,
    string DisplayName,
    string? Tagline,
    string? LogoUrl,
    string? ThemeJson,
    string? OrganizationName,
    bool FirstParty,
    IReadOnlyList<ScopeInfo> Scopes);

public record ScopeInfo(
    string Name,
    string? Description);
