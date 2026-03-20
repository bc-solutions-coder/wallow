using Wallow.Shared.Contracts.Metering;

namespace Wallow.Tests.Common.Fakes;

public sealed class FakeMeteringQueryService : IMeteringQueryService
{
    public Task<QuotaStatus?> CheckQuotaAsync(Guid tenantId, string meterCode, CancellationToken ct = default)
        => Task.FromResult<QuotaStatus?>(null);
}
