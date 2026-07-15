namespace Wallow.Identity.Api.Contracts.Requests;

public sealed record RegisterSetupClientRequest(
    string ClientId,
    IReadOnlyList<string> RedirectUris);
