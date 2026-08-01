using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Domain.Entities;

/// <summary>
/// One role assignment on a membership. The membership aggregate owns the collection; rows are
/// keyed (MembershipId, RoleId) so a role can be granted at most once per membership.
/// </summary>
public sealed class MembershipRole
{
    public MembershipId MembershipId { get; private set; }
    public Guid RoleId { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private MembershipRole() { } // EF Core

    public MembershipRole(MembershipId membershipId, Guid roleId)
    {
        MembershipId = membershipId;
        RoleId = roleId;
    }
}
