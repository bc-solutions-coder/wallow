using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Application.DTOs;

public record ApiScopeDto(
    ApiScopeId Id,
    string Code,
    string DisplayName,
    string Category,
    string? Description,
    bool IsDefault);
