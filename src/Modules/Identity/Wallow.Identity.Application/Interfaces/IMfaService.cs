namespace Wallow.Identity.Application.Interfaces;

public interface IMfaService
{
    Task<(string Secret, string QrUri)> GenerateEnrollmentSecretAsync(string userId, CancellationToken ct);

    Task<bool> ValidateTotpAsync(string base32Secret, string code, CancellationToken ct);

    Task<List<string>> GenerateBackupCodesAsync(CancellationToken ct);

    Task<bool> ValidateBackupCodeAsync(string userId, string code, CancellationToken ct);

    string SerializeBackupCodesForStorage(IReadOnlyList<string> plainTextCodes);
}
