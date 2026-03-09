using JetBrains.Annotations;

namespace Foundry.Shared.Contracts.Metering;

public interface IUsageReportService
{
    Task<IReadOnlyList<UsageReportRow>> GetUsageAsync(
        Guid tenantId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record UsageReportRow(
    DateTime Date,
    string Metric,
    long Quantity,
    string Unit,
    decimal BillableAmount,
    string Currency);
