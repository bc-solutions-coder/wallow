namespace Wallow.Identity.Application.DTOs;

/// <summary>
/// Result of evaluating organization enrollment. Subtypes are defined within this assembly;
/// controllers must map each outcome to a continuation or refusal.
/// </summary>
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
/// Enrollment refusal with a protocol reason from <see cref="EnrollmentReasons"/>.
/// The auth app displays first-party refusals; relying parties receive access_denied.
/// Unverified email remains an auth-host error for every client.
/// </summary>
public sealed record Rejected(string Reason) : EnrollmentOutcome;

/// <summary>
/// Protocol reason strings shared by enrollment redirects and OAuth error descriptions.
/// </summary>
public static class EnrollmentReasons
{
    /// <summary>No membership, and the organization's policy grants none on the spot.</summary>
    public const string NotAMember = "not_a_member";

    /// <summary>
    /// Email verification is required; the auth host handles this refusal for every client.
    /// </summary>
    public const string EmailUnverified = "email_unverified";

    /// <summary>The membership exists and is suspended.</summary>
    public const string MembershipSuspended = "membership_suspended";

    /// <summary>The membership was denied and the denial still stands.</summary>
    public const string MembershipDenied = "membership_denied";

    /// <summary>
    /// A recorded request awaits approval. Used as the relying-party error description for
    /// <see cref="PendingApproval"/>, rather than as a <see cref="Rejected"/> reason.
    /// </summary>
    public const string MembershipPending = "membership_pending";
}
