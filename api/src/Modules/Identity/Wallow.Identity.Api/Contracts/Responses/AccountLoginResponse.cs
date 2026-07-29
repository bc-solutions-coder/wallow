namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Result of a cookie-based sign-in attempt. A 200 covers four outcomes — signed in, MFA
/// challenge required, MFA enrollment required, and MFA enrollment required within a grace
/// period — so every member beyond <see cref="Succeeded"/> is present only for the outcome it
/// describes.
/// </summary>
public sealed record AccountLoginResponse(
    bool Succeeded,
    bool? MfaRequired = null,
    bool? MfaEnrollmentRequired = null,
    DateTimeOffset? MfaGraceDeadline = null,
    string? SignInTicket = null);
