namespace Wallow.Identity.Api.Contracts.Responses;

public record CurrentUserResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>
    /// Whether the caller holds the platform operator's own authority — minted as its own
    /// claim at sign-in, never derived from organization roles. The web app renders the
    /// platform-suspension controls only for callers carrying it.
    /// </summary>
    public bool IsGlobalAdmin { get; init; }
}
