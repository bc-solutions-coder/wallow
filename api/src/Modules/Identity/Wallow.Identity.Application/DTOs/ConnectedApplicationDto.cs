namespace Wallow.Identity.Application.DTOs;

/// <summary>
/// An application the user has granted durable consent to: one Valid permanent authorization,
/// named by the client it authorizes and the scopes the user agreed to.
/// </summary>
public record ConnectedApplicationDto(
    string Id,
    string ClientId,
    string? DisplayName,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? CreatedAt);
