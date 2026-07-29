namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Result of answering an MFA challenge, carrying the single-use ticket the auth frontend
/// exchanges for a sign-in cookie.
/// </summary>
public sealed record MfaChallengeResponse(bool Succeeded, string SignInTicket);
