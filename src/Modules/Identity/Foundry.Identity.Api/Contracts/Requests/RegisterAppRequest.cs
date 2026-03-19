namespace Foundry.Identity.Api.Contracts.Requests;

public record RegisterAppRequest(string ClientName, IReadOnlyList<string> RequestedScopes);
