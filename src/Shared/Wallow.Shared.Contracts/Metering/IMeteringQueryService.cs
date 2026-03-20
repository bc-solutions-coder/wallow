namespace Wallow.Shared.Contracts.Metering;

public interface IMeteringQueryService
{
    Task<QuotaStatus?> CheckQuotaAsync(Guid tenantId, string meterCode, CancellationToken ct = default);
}

public sealed record QuotaStatus(
    string MeterCode,
    long Used,
    long Limit,
    decimal PercentUsed,
    bool IsExceeded);
