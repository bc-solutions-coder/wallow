namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// TOTP enrollment confirmation with plaintext backup codes. Only code hashes are stored.
/// </summary>
public sealed record MfaEnrollmentConfirmedResponse(bool Succeeded, IReadOnlyList<string> BackupCodes);
