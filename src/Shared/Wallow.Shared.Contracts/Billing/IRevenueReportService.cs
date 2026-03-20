namespace Wallow.Shared.Contracts.Billing;

public interface IRevenueReportService
{
    Task<IReadOnlyList<RevenueReportRow>> GetRevenueAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default);
}

public sealed record RevenueReportRow(
    string Period,
    decimal GrossRevenue,
    decimal NetRevenue,
    decimal Refunds,
    string Currency,
    int InvoiceCount,
    int PaymentCount);
