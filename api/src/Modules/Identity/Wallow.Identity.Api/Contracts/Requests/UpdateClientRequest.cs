namespace Wallow.Identity.Api.Contracts.Requests;

public record UpdateClientRequest(
    string Name,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris);
