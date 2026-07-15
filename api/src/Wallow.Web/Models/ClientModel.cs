namespace Wallow.Web.Models;

public record ClientModel
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public required IReadOnlyList<string> RedirectUris { get; init; }
    public required IReadOnlyList<string> PostLogoutRedirectUris { get; init; }
}
