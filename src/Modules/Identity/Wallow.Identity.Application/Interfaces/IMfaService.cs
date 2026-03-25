using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Interfaces;

public interface IMfaService
{
    Task<(string Secret, string QrUri)> GenerateEnrollmentSecretAsync(string userId, CancellationToken ct);

    Task<bool> ValidateTotpAsync(string secret, string code, CancellationToken ct);

    Task<string> IssueChallengeAsync(string userId, CancellationToken ct);

    Task<Result> ValidateChallengeAsync(string userId, string code, CancellationToken ct);

    Task<List<string>> GenerateBackupCodesAsync(CancellationToken ct);

    Task<string> IssueMfaChallengeTokenAsync(string userId, CancellationToken ct);

    Task<bool> ValidateBackupCodeAsync(string userId, string code, CancellationToken ct);

    Task<bool> ValidateChallengeAsync(string userId, string challengeToken, string code, CancellationToken ct);
}
