namespace Wallow.Identity.Application.DTOs;

public record SessionDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    DateTimeOffset ExpiresAt);
