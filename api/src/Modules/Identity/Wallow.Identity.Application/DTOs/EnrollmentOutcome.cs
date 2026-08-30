namespace Wallow.Identity.Application.DTOs;

/// <summary>
/// What happened when someone tried to join an organization they are not yet a member of.
/// </summary>
/// <remarks>
/// A closed hierarchy rather than a status enum: each arm sends the person somewhere different,
/// and the callers that map an outcome to a redirect sit in two controllers. A new arm has to
/// break every switch that maps one, which is the point.
/// <para>
/// The constructor is <c>private protected</c>, so the three arms below are the only ones that
/// can exist outside this assembly. They are siblings rather than nested types because a nested
/// public type is a build error here (CA1034).
/// </para>
/// </remarks>
public abstract record EnrollmentOutcome
{
    private protected EnrollmentOutcome()
    {
    }
}

/// <summary>The person is now an active member and the caller may continue.</summary>
public sealed record Enrolled : EnrollmentOutcome;

/// <summary>A request was recorded and somebody else has to act on it.</summary>
public sealed record PendingApproval : EnrollmentOutcome;

/// <summary>
/// Nothing was recorded. <see cref="Reason" /> is one of <see cref="EnrollmentReasons" />: the
/// auth app's error page routes on it for a first-party login, and a relying party receives it
/// as the <c>error_description</c> of an <c>access_denied</c> answer.
/// </summary>
public sealed record Rejected(string Reason) : EnrollmentOutcome;

/// <summary>
/// The reasons an organization gives for not signing someone in, spelled the one way every
/// consumer — the auth app's error page, a relying party's <c>error_description</c> — reads them.
/// </summary>
public static class EnrollmentReasons
{
    /// <summary>No membership, and the organization's policy grants none on the spot.</summary>
    public const string NotAMember = "not_a_member";

    /// <summary>
    /// The account's email is unverified. Not an organization's answer at all — a precondition
    /// the auth host resolves — so it never leaves the auth host.
    /// </summary>
    public const string EmailUnverified = "email_unverified";

    /// <summary>The membership exists and is suspended.</summary>
    public const string MembershipSuspended = "membership_suspended";

    /// <summary>The membership was denied and the denial still stands.</summary>
    public const string MembershipDenied = "membership_denied";

    /// <summary>
    /// An access request is recorded and awaits approval. Never a <see cref="Rejected" />
    /// reason — that outcome is <see cref="PendingApproval" /> — but the description a relying
    /// party is given for it.
    /// </summary>
    public const string MembershipPending = "membership_pending";
}
