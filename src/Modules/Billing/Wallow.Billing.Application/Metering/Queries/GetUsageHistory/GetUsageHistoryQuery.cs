namespace Wallow.Billing.Application.Metering.Queries.GetUsageHistory;

/// <summary>
/// Gets historical usage records for a meter.
/// </summary>
public sealed record GetUsageHistoryQuery(
    string MeterCode,
    DateTime From,
    DateTime To);
