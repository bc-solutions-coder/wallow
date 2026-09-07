namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Successful MFA challenge result. The endpoint issues a browser sign-in ticket when the user has an email.
/// </summary>
public sealed record MfaChallengeResponse(bool Succeeded, string SignInTicket);
