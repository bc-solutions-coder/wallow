namespace Wallow.Identity.Api.Contracts.Requests;

public sealed record MfaLoginVerifyRequest(
    string Email,
    string ChallengeToken,
    string Code,
    bool RememberMe,
    bool UseBackupCode = false);
