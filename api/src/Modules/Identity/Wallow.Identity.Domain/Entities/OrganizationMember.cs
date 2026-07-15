using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

public sealed class OrganizationMember : ValueObject
{
    public Guid UserId { get; }
    public string Role { get; }

    private OrganizationMember() // EF Core
    {
        Role = string.Empty;
    }

    public OrganizationMember(Guid userId, string role)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Identity.UserIdRequired",
                "User ID cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            throw new BusinessRuleException(
                "Identity.MemberRoleRequired",
                "Organization member role cannot be empty");
        }

        UserId = userId;
        Role = role;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UserId;
        yield return Role;
    }
}
