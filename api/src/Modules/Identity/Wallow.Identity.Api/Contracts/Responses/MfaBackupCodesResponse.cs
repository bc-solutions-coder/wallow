namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Regenerated one-time backup codes. The plaintext codes are returned here and nowhere else —
/// only their hashes are persisted.
/// </summary>
public sealed record MfaBackupCodesResponse(IReadOnlyList<string> Codes);
