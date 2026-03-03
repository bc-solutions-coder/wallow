using Foundry.Billing.Application.Metering.DTOs;
using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Application.Metering.Services;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Application.Metering.Queries.GetQuotaStatus;

public sealed class GetQuotaStatusHandler(
    IQuotaDefinitionRepository quotaRepository,
    IMeterDefinitionRepository meterRepository,
    IMeteringService meteringService)
{
    public async Task<Result<IReadOnlyList<QuotaStatusDto>>> Handle(
        GetQuotaStatusQuery _,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MeterDefinition> meters = await meterRepository.GetAllAsync(cancellationToken);
        List<QuotaStatusDto> results = new List<QuotaStatusDto>();

        foreach (MeterDefinition meter in meters)
        {
            QuotaCheckResult quotaCheck = await meteringService.CheckQuotaAsync(meter.Code);

            if (quotaCheck.Limit == decimal.MaxValue)
            {
                continue;
            }

            QuotaDefinition? quotaOverride = await quotaRepository.GetTenantOverrideAsync(
                meter.Code,
                cancellationToken);

            results.Add(new QuotaStatusDto(
                MeterCode: meter.Code,
                MeterDisplayName: meter.DisplayName,
                CurrentUsage: quotaCheck.CurrentUsage,
                Limit: quotaCheck.Limit,
                PercentUsed: quotaCheck.PercentUsed,
                Period: "Monthly",
                OnExceeded: quotaCheck.ActionIfExceeded?.ToString() ?? "None",
                IsOverride: quotaOverride is not null));
        }

        return results;
    }
}
