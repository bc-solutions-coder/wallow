namespace Wallow.Identity.Application.DTOs;

/// <summary>
/// An active membership's organization, including its slug for navigation.
/// </summary>
public record MyOrganizationDto(
    Guid OrganizationId,
    string Name,
    string Slug,
    bool IsOwner);
