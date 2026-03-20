using Wallow.Billing.Application.Metering.DTOs;
using Wallow.Billing.Application.Metering.Interfaces;
using Wallow.Billing.Application.Metering.Services;
using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Enums;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Metering.Queries.GetCurrentUsage;

public sealed class GetCurrentUsageHandler(
    IMeterDefinitionRepository meterRepository,
    IMeteringService meteringService)
{
    public async Task<Result<IReadOnlyList<UsageSummaryDto>>> Handle(
        GetCurrentUsageQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MeterDefinition> meters = await meterRepository.GetAllAsync(cancellationToken);
        List<UsageSummaryDto> results = [];

        foreach (MeterDefinition meter in meters)
        {
            if (query.MeterCode is not null && meter.Code != query.MeterCode)
            {
                continue;
            }

            QuotaPeriod period = query.Period ?? QuotaPeriod.Monthly;
            decimal currentValue = await meteringService.GetCurrentUsageAsync(meter.Code, period);
            QuotaCheckResult quotaCheck = await meteringService.CheckQuotaAsync(meter.Code);

            results.Add(new UsageSummaryDto(
                MeterCode: meter.Code,
                DisplayName: meter.DisplayName,
                Unit: meter.Unit,
                CurrentValue: currentValue,
                Limit: quotaCheck.Limit == decimal.MaxValue ? null : quotaCheck.Limit,
                PercentUsed: quotaCheck.Limit == decimal.MaxValue ? null : quotaCheck.PercentUsed,
                Period: period.ToString()));
        }

        return results;
    }
}
