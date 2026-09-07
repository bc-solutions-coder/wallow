namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// New plaintext backup codes. Only hashes are stored for later verification.
/// </summary>
public sealed record MfaBackupCodesResponse(IReadOnlyList<string> Codes);
