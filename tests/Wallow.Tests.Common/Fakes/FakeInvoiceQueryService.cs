using Wallow.Shared.Contracts.Billing;

namespace Wallow.Tests.Common.Fakes;

public sealed class FakeInvoiceQueryService : IInvoiceQueryService
{
    public Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => Task.FromResult(0m);

    public Task<int> GetCountAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => Task.FromResult(0);

    public Task<int> GetPendingCountAsync(CancellationToken ct = default)
        => Task.FromResult(0);

    public Task<decimal> GetOutstandingAmountAsync(CancellationToken ct = default)
        => Task.FromResult(0m);
}
