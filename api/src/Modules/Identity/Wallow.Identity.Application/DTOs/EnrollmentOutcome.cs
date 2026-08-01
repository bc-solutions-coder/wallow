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
/// Nothing was recorded. <see cref="Reason" /> is the error-screen reason the caller routes on,
/// so it is one of the strings the auth app's error page knows.
/// </summary>
public sealed record Rejected(string Reason) : EnrollmentOutcome;
