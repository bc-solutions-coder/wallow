namespace Wallow.Web.Models;

public record OrganizationMemberModel(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool Enabled,
    IReadOnlyList<string> Roles);
