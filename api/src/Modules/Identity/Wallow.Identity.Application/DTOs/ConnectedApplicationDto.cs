namespace Wallow.Identity.Application.DTOs;

/// <summary>
/// One valid permanent authorization representing a user's consent to an application.
/// </summary>
public record ConnectedApplicationDto(
    string Id,
    string ClientId,
    string? DisplayName,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? CreatedAt);
