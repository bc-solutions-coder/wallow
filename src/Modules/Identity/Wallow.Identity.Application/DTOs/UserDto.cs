namespace Wallow.Identity.Application.DTOs;

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool Enabled,
    IReadOnlyList<string> Roles);
