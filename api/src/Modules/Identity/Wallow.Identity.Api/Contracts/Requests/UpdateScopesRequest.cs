namespace Wallow.Identity.Api.Contracts.Requests;

public record UpdateScopesRequest(IReadOnlyList<string> Scopes);
