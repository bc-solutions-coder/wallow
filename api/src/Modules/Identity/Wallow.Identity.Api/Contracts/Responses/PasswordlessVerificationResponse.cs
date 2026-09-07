namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Passwordless verification result with the email and ticket for browser sign-in.
/// </summary>
public sealed record PasswordlessVerificationResponse(bool Succeeded, string? Email, string? SignInTicket);
