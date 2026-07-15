namespace Wallow.Identity.Api.Contracts.Requests;

public record CreateClientRequest(
    string Name,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    Guid? TenantId = null);
