namespace Wallow.Identity.Domain.Enums;

/// <summary>
/// Membership lifecycle. The role resolver returns roles only for <see cref="Active"/> memberships.
/// </summary>
public enum MembershipStatus
{
    Pending,
    Active,
    Suspended,
    Denied
}
