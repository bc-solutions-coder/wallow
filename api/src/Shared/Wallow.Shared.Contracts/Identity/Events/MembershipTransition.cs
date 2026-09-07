namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Auditable changes to organization membership and access.
/// </summary>
public enum MembershipTransition
{
    /// <summary>Somebody asked to join an organization that reviews requests.</summary>
    AccessRequested,

    /// <summary>Somebody joined an organization that admits anyone, without a review.</summary>
    Enrolled,

    /// <summary>An administrator put somebody into an organization directly.</summary>
    Added,

    /// <summary>A reviewer accepted an outstanding request.</summary>
    Approved,

    /// <summary>A reviewer refused an outstanding request.</summary>
    Denied,

    /// <summary>A reviewer took back a refusal, freeing the person to ask again.</summary>
    DenialCleared,

    /// <summary>An active membership was put on hold.</summary>
    Suspended,

    /// <summary>A suspended membership was made active again.</summary>
    Reinstated,

    /// <summary>A role was granted inside an organization.</summary>
    RoleAssigned,

    /// <summary>A role was taken away inside an organization.</summary>
    RoleRemoved,

    /// <summary>A member removed their own membership.</summary>
    Left,

    /// <summary>An administrator removed somebody else's membership.</summary>
    Removed,

    /// <summary>A membership was made an owner of its organization.</summary>
    OwnerMarked,

    /// <summary>A membership stopped being an owner of its organization.</summary>
    OwnerUnmarked
}
