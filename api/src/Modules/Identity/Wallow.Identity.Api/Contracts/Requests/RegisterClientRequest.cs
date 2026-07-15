namespace Wallow.Identity.Api.Contracts.Requests;

public record RegisterClientRequest(
    string ClientId,
    string ClientName,
    IReadOnlyList<string> GrantTypes,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string>? RedirectUris,
    Guid? TenantId = null);
