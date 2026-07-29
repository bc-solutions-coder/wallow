namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// The signed-in user's current multi-factor enrollment state.
/// </summary>
public sealed record MfaStatusResponse(bool Enabled, string? Method, int BackupCodeCount);
