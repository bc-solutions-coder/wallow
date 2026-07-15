namespace Wallow.Identity.Api.Contracts.Responses;

public record ServiceAccountCreatedResponse
{
    public required string Id { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string TokenEndpoint { get; init; }
    public required IReadOnlyList<string> Scopes { get; init; }
    public string Warning => "Save this secret now. It will not be shown again.";
}
