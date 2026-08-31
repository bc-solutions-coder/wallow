namespace Wallow.Identity.Api.Contracts.Responses;

public record ClientResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public required IReadOnlyList<string> RedirectUris { get; init; }
    public required IReadOnlyList<string> PostLogoutRedirectUris { get; init; }
    public required IReadOnlyList<string> Scopes { get; init; }
    public string? FrontchannelLogoutUri { get; init; }
    public string? BackchannelLogoutUri { get; init; }

    /// <summary>
    /// The client's declaration that its logout tokens must carry <c>sid</c>. Wallow always
    /// includes <c>sid</c>, so this is registration metadata echoed back, not a delivery switch.
    /// </summary>
    public bool BackchannelLogoutSessionRequired { get; init; }

    /// <summary>
    /// Refresh-token lifetime in seconds, bounding newly issued refresh tokens. Absent on a
    /// client registered before per-client lifetimes existed, where the global configuration
    /// decides.
    /// </summary>
    public int? RefreshTokenLifetime { get; init; }
}
