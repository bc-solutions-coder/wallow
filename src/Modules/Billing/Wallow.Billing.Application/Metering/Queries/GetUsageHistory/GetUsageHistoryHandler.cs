using Wallow.Billing.Application.Metering.DTOs;
using Wallow.Billing.Application.Metering.Interfaces;
using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Metering.Queries.GetUsageHistory;

public sealed class GetUsageHistoryHandler(
    IUsageRecordRepository usageRepository)
{
    public async Task<Result<IReadOnlyList<UsageRecordDto>>> Handle(
        GetUsageHistoryQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<UsageRecord> records = await usageRepository.GetHistoryAsync(
            query.MeterCode,
            query.From,
            query.To,
            cancellationToken);

        List<UsageRecordDto> dtos = records.Select(r => new UsageRecordDto(
            Id: r.Id.Value,
            TenantId: r.TenantId.Value,
            MeterCode: r.MeterCode,
            PeriodStart: r.PeriodStart,
            PeriodEnd: r.PeriodEnd,
            Value: r.Value,
            FlushedAt: r.FlushedAt)).ToList();

        return dtos;
    }
}
