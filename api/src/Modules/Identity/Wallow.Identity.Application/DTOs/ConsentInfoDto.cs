namespace Wallow.Identity.Application.DTOs;

public sealed record ConsentInfoDto(
    string ClientId,
    string? DisplayName,
    string? LogoUrl,
    IReadOnlyList<ConsentScopeDto> RequestedScopes);

public sealed record ConsentScopeDto(
    string Name,
    string? Description);
