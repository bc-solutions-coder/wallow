namespace Wallow.Identity.Application.DTOs;

public record OrganizationDto(Guid Id, string Name, string? Domain, int MemberCount);
