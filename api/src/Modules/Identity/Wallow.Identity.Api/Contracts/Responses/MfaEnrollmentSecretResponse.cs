namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// A freshly generated TOTP secret plus the otpauth URI an authenticator app scans.
/// </summary>
public sealed record MfaEnrollmentSecretResponse(string Secret, string QrUri);
