using Wallow.Web.Models;

namespace Wallow.Web.Services;

public interface IMfaApiClient
{
    Task<MfaStatusResponse?> GetMfaStatusAsync(CancellationToken ct = default);
    Task<bool> DisableMfaAsync(string password, CancellationToken ct = default);
    Task<List<string>?> RegenerateBackupCodesAsync(string password, CancellationToken ct = default);
    Task<string?> IssueEnrollmentTokenAsync(CancellationToken ct = default);
}
