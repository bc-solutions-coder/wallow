namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Password sign-in result. A successful response may carry a sign-in ticket, an MFA
/// challenge requirement, or an enrollment requirement with an optional grace deadline.
/// </summary>
public sealed record AccountLoginResponse(
    bool Succeeded,
    bool? MfaRequired = null,
    bool? MfaEnrollmentRequired = null,
    DateTimeOffset? MfaGraceDeadline = null,
    string? SignInTicket = null);
