using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Application.DTOs;

public record ServiceAccountDto(
    ServiceAccountMetadataId Id,
    string ClientId,
    string Name,
    string? Description,
    ServiceAccountStatus Status,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt);
