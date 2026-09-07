namespace Wallow.Identity.Domain.Enums;

/// <summary>
/// Controls self-service enrollment. Invitation acceptance bypasses this policy.
/// </summary>
public enum EnrollmentPolicy
{
    /// <summary>
    /// Self-service enrollment is disabled. This is the default for a new organization.
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
