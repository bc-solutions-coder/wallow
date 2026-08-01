namespace Wallow.Identity.Application.DTOs;

/// <summary>
/// One organization the caller may sign in to. A client is bound to a single organization, so
/// an app can only ever link to the others — the slug is what it links with, and the name is
/// what it shows.
/// </summary>
public record MyOrganizationDto(
    Guid OrganizationId,
    string Name,
    string Slug,
    bool IsOwner);
