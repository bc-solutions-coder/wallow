namespace Wallow.Identity.Api.Contracts.Requests;

public record CreateServiceAccountRequest(
    string Name,
    string? Description,
    IReadOnlyList<string> Scopes);
