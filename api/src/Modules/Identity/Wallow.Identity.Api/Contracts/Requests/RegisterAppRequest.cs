namespace Wallow.Identity.Api.Contracts.Requests;

public record RegisterAppRequest(
    string ClientName,
    IReadOnlyList<string> RequestedScopes,
    string? ClientType = null,
    IReadOnlyList<string>? RedirectUris = null);
