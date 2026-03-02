using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Domain.Identity;

namespace Foundry.Identity.Application.DTOs;

public record ServiceAccountDto(
    ServiceAccountMetadataId Id,
    string ClientId,
    string Name,
    string? Description,
    ServiceAccountStatus Status,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt);
