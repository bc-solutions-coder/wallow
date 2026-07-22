using Wallow.Identity.Domain.Enums;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

public sealed class OrganizationMember : ValueObject
{
    public Guid UserId { get; }
    public OrgMemberRole Role { get; }

    private OrganizationMember() // EF Core
    {
        Role = default;
    }

    public OrganizationMember(Guid userId, OrgMemberRole role)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Identity.UserIdRequired",
                "User ID cannot be empty");
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
