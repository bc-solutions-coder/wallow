namespace Wallow.Identity.Application.Interfaces;

public interface ITestSupportService
{
    Task<Guid> CreateIsolatedOrgAsync(Guid userId, bool requireMfa, int gracePeriodDays, CancellationToken ct = default);
}
