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
    /// Stored session-required registration flag. Wallow includes sid in logout tokens regardless of this value.
    /// </summary>
    public bool BackchannelLogoutSessionRequired { get; init; }

    /// <summary>
    /// Per-client refresh-token lifetime in seconds, or null when no parseable setting is present.
    /// </summary>
    public int? RefreshTokenLifetime { get; init; }
}
