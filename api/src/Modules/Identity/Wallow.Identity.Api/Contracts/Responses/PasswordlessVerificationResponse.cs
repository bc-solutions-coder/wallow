namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Result of verifying a magic link or one-time code, carrying the single-use ticket the auth
/// frontend exchanges for a sign-in cookie.
/// </summary>
public sealed record PasswordlessVerificationResponse(bool Succeeded, string? Email, string? SignInTicket);
