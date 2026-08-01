namespace Wallow.Identity.Domain.Enums;

/// <summary>
/// The lifecycle of a person's relationship with one organization. Only <see cref="Active"/>
/// resolves roles; every other status grants nothing.
/// </summary>
public enum MembershipStatus
{
    Pending,
    Active,
    Suspended,
    Denied
}
