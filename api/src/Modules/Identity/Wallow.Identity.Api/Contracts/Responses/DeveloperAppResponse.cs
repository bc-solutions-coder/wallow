namespace Wallow.Identity.Api.Contracts.Responses;

public sealed record DeveloperAppResponse(
    string ClientId,
    string DisplayName,
    string ClientType,
    IReadOnlyList<string> RedirectUris,
    DateTimeOffset? CreatedAt);
