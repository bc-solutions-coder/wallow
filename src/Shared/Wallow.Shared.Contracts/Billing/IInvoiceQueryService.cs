namespace Wallow.Shared.Contracts.Billing;

public interface IInvoiceQueryService
{
    Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<int> GetCountAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<int> GetPendingCountAsync(CancellationToken ct = default);
    Task<decimal> GetOutstandingAmountAsync(CancellationToken ct = default);
}
