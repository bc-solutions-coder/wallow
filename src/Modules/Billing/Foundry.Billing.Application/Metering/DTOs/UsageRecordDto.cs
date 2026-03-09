using JetBrains.Annotations;

namespace Foundry.Billing.Application.Metering.DTOs;

/// <summary>
/// DTO for historical usage records.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record UsageRecordDto(
    Guid Id,
    Guid TenantId,
    string MeterCode,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal Value,
    DateTime FlushedAt);
