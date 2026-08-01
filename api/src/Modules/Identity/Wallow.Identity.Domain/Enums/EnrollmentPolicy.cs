namespace Wallow.Identity.Domain.Enums;

/// <summary>
/// How an organization admits someone who is not already a member. Invitation acceptance is not
/// governed by this — being invited by a member holding <c>OrganizationsManageMembers</c> is itself
/// the authorization.
/// </summary>
public enum EnrollmentPolicy
{
    /// <summary>
    /// Nobody joins without an invitation. The default for a new organization, because a policy
    /// nobody has chosen yet must be the one that grants nothing.
    /// </summary>
    InviteOnly,

    /// <summary>
    /// Anyone with a verified email may ask to join; a member who can manage members decides.
    /// </summary>
    RequestApproval,

    /// <summary>
    /// Anyone with a verified email joins immediately, with the organization's default role.
    /// </summary>
    Open
}
