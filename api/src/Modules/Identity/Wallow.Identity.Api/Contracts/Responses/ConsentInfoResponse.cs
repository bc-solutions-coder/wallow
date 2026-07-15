namespace Wallow.Identity.Api.Contracts.Responses;

public record ConsentInfoResponse(
    string ClientId,
    string? DisplayName,
    string? LogoUrl,
    IReadOnlyList<ScopeInfo> RequestedScopes);

public record ScopeInfo(
    string Name,
    string? Description);
